using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Npgsql;

namespace KUK.Common.Services
{
    public class ExternalConnectionService : IExternalConnectionService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ExternalConnectionService> _logger;
        private readonly IDatabaseDirectConnectionService _databaseDirectConnectionService;

        public ExternalConnectionService(
            IConfiguration configuration,
            ILogger<ExternalConnectionService> logger,
            IDatabaseDirectConnectionService databaseDirectConnectionService)
        {
            _configuration = configuration;
            _logger = logger;
            _databaseDirectConnectionService = databaseDirectConnectionService;
        }

        public async Task<List<string>> GetListOfRegisteredConnectors()
        {
            try
            {
                string url;

                ThrowIfConfigurationValueIsMissing("OnlineMode:ApiBaseAddress");
                ThrowIfConfigurationValueIsMissing("OnlineMode:BearerTokenEnvironmentVariableKey");

                using (var client = new HttpClient { BaseAddress = new Uri(_configuration["OnlineMode:ApiBaseAddress"]) })
                {
                    // Retrieve Bearer token from environment variables
                    var bearerToken = Environment.GetEnvironmentVariable(_configuration["OnlineMode:BearerTokenEnvironmentVariableKey"]);
                    if (string.IsNullOrWhiteSpace(bearerToken))
                    {
                        throw new Exception("Bearer token not found in environment variables.");
                    }
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

                    // Retrieve project and service names from configuration
                    var project = _configuration["OnlineMode:Project"];
                    if (string.IsNullOrWhiteSpace(project))
                    {
                        throw new Exception("Project name is not configured in appsettings.");
                    }
                    var serviceName = _configuration["OnlineMode:KafkaConnectService"];
                    if (string.IsNullOrWhiteSpace(serviceName))
                    {
                        throw new Exception("Service name is not configured in appsettings.");
                    }

                    url = $"/v1/project/{Uri.EscapeDataString(project)}/service/{Uri.EscapeDataString(serviceName)}/connectors";

                    var response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    var responseBody = await response.Content.ReadAsStringAsync();

                    dynamic jsonObj = JsonConvert.DeserializeObject(responseBody);
                    var connectors = new List<string>();

                    foreach (var connector in jsonObj.connectors)
                    {
                        // Convert dynamically acquired property "name" to string
                        connectors.Add((string)connector.name);
                    }

                    return connectors;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking if connector is registered: {ex.Message}");
                throw new InvalidOperationException($"Error checking if connector is registered: {ex}");
            }
        }

        public async Task CreatePublicationForAllTables()
        {
            var newDbName = _configuration["ConnectorDetails:DbNameNew"];
            if (string.IsNullOrEmpty(newDbName))
            {
                throw new InvalidOperationException($"Value of ConnectorDetails:DbNameNew is not set in appsettings.");
            }

            NpgsqlConnectionStringBuilder newDatabaseStringBuilder = _databaseDirectConnectionService.GetNewDatabaseConnection(newDbName);

            using (var connection = new NpgsqlConnection(newDatabaseStringBuilder.ConnectionString))
            {
                await connection.OpenAsync();

                ThrowIfConfigurationValueIsMissing("OnlineMode:ExtrasExtension");

                // Create extras extension if it does not exist
                string extensionName = _configuration["OnlineMode:ExtrasExtension"];
                if (!Regex.IsMatch(extensionName, @"^[A-Za-z0-9_]+$"))
                {
                    throw new InvalidOperationException("Invalid extension name.");
                }
                var commandBuilder = new NpgsqlCommandBuilder();
                string safeExtensionName = commandBuilder.QuoteIdentifier(extensionName);
                string createExtensionQuery = $"CREATE EXTENSION IF NOT EXISTS {safeExtensionName} CASCADE;";
                using (var createExtensionCmd = new NpgsqlCommand(createExtensionQuery, connection))
                {
                    await createExtensionCmd.ExecuteNonQueryAsync();
                }

                // Check if the publication 'debezium_publication' already exists
                string checkPublicationQuery = "SELECT 1 FROM pg_publication WHERE pubname = 'debezium_publication';";
                using (var checkPublicationCmd = new NpgsqlCommand(checkPublicationQuery, connection))
                {
                    var result = await checkPublicationCmd.ExecuteScalarAsync();
                    if (result != null)
                    {
                        // Publication already exists, so skip creation
                        return;
                    }
                }

                // Create publication for all tables using extras function.
                // It must correspond to new connector's configuration: "publication.name": "debezium_publication"
                string createPublicationQuery = @$"
                    SELECT * FROM {safeExtensionName}.pg_create_publication_for_all_tables(
                        'debezium_publication',
                        'INSERT,UPDATE,DELETE'
                    );";

                using (var createPublicationCmd = new NpgsqlCommand(createPublicationQuery, connection))
                {
                    await createPublicationCmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task RegisterConnectorAsync(string configPath)
        {
            string connectorJsonContentWithPlaceholders = string.Empty;
            string jsonPayload = string.Empty;

            if (configPath.StartsWith("old-to-new-connector") || configPath.Contains("debezium-connector-config-1"))
            {
                ThrowIfConfigurationValueIsMissing("ConnectorDetails:OldConnectorSuffix");
                ThrowIfConfigurationValueIsMissing("Databases:OldDatabase:ServerName");
                ThrowIfConfigurationValueIsMissing("Databases:OldDatabase:ServerPort");
                ThrowIfEnvironmentVariableMissing("DebeziumWorker_Online_OldDatabaseDataChangesUserName");
                ThrowIfEnvironmentVariableMissing("DebeziumWorker_Online_OldDatabaseDataChangesPassword");

                connectorJsonContentWithPlaceholders = GetEmbeddedResource(1);

                var suffix = _configuration["ConnectorDetails:OldConnectorSuffix"];
                string oldDatabaseHostName = _configuration["Databases:OldDatabase:ServerName"];
                string oldDatabasePort = _configuration["Databases:OldDatabase:ServerPort"];
                string oldDatabaseUserName = Environment.GetEnvironmentVariable("DebeziumWorker_Online_OldDatabaseDataChangesUserName");
                string oldDatabasePassword = Environment.GetEnvironmentVariable("DebeziumWorker_Online_OldDatabaseDataChangesPassword");

                jsonPayload = connectorJsonContentWithPlaceholders
                    .Replace("{connectorNameSuffix}", suffix)
                    .Replace("{oldDatabaseHostName}", oldDatabaseHostName)
                    .Replace("{oldDatabasePort}", oldDatabasePort)
                    .Replace("{oldDatabaseUserName}", oldDatabaseUserName)
                    .Replace("{oldDatabasePassword}", oldDatabasePassword);
            }
            else if (configPath.StartsWith("new-to-old-connector") || configPath.Contains("debezium-connector-config-2"))
            {
                ThrowIfConfigurationValueIsMissing("ConnectorDetails:NewConnectorSuffix");
                ThrowIfConfigurationValueIsMissing("Databases:NewDatabase:ServerName");
                ThrowIfConfigurationValueIsMissing("Databases:NewDatabase:ServerPort");
                ThrowIfEnvironmentVariableMissing("DebeziumWorker_Online_NewDatabaseDataChangesUserName");
                ThrowIfEnvironmentVariableMissing("DebeziumWorker_Online_NewDatabaseDataChangesPassword");

                connectorJsonContentWithPlaceholders = GetEmbeddedResource(2);

                var suffix = _configuration["ConnectorDetails:NewConnectorSuffix"];
                string newDatabaseHostName = _configuration["Databases:NewDatabase:ServerName"];
                string newDatabasePort = _configuration["Databases:NewDatabase:ServerPort"];
                string newDatabaseUserName = Environment.GetEnvironmentVariable("DebeziumWorker_Online_NewDatabaseDataChangesUserName");
                string newDatabasePassword = Environment.GetEnvironmentVariable("DebeziumWorker_Online_NewDatabaseDataChangesPassword");

                jsonPayload = connectorJsonContentWithPlaceholders
                    .Replace("{connectorNameSuffix}", suffix)
                    .Replace("{newDatabaseHostName}", newDatabaseHostName)
                    .Replace("{newDatabasePort}", newDatabasePort)
                    .Replace("{newDatabaseUserName}", newDatabaseUserName)
                    .Replace("{newDatabasePassword}", newDatabasePassword);
            }
            else
            {
                throw new InvalidOperationException($"Unknown configPath {configPath}");
            }

            ThrowIfConfigurationValueIsMissing("Kafka:SchemaRegistryUrlAndPort");
            ThrowIfConfigurationValueIsMissing("Kafka:KeystoreLocation");
            ThrowIfConfigurationValueIsMissing("Kafka:TruststoreLocation");
            ThrowIfConfigurationValueIsMissing("Kafka:BootstrapServers");
            ThrowIfEnvironmentVariableMissing("DebeziumWorker_Online_SchemaRegistryLoginAndPassword");
            ThrowIfEnvironmentVariableMissing("DebeziumWorker_Online_SslKeyPassword");
            ThrowIfEnvironmentVariableMissing("DebeziumWorker_Online_KeystorePassword");
            ThrowIfEnvironmentVariableMissing("DebeziumWorker_Online_TruststorePassword");

            string schemaRegistryLoginAndPassword = Environment.GetEnvironmentVariable("DebeziumWorker_Online_SchemaRegistryLoginAndPassword");
            string schemaRegistryUrlAndPort = _configuration["Kafka:SchemaRegistryUrlAndPort"];
            string sslKeyPassword = Environment.GetEnvironmentVariable("DebeziumWorker_Online_SslKeyPassword");
            string keystoreLocation = _configuration["Kafka:KeystoreLocation"];
            string keystorePassword = Environment.GetEnvironmentVariable("DebeziumWorker_Online_KeystorePassword");
            string truststoreLocation = _configuration["Kafka:TruststoreLocation"];
            string truststorePassword = Environment.GetEnvironmentVariable("DebeziumWorker_Online_TruststorePassword");
            string bootstrapServerAndPort = _configuration["Kafka:BootstrapServers"];

            jsonPayload = jsonPayload
                    .Replace("{schemaRegistryLoginAndPassword}", schemaRegistryLoginAndPassword)
                    .Replace("{schemaRegistryUrlAndPort}", $"https://{schemaRegistryUrlAndPort}")
                    .Replace("{sslKeyPassword}", sslKeyPassword)
                    .Replace("{keystoreLocation}", keystoreLocation)
                    .Replace("{keystorePassword}", keystorePassword)
                    .Replace("{truststoreLocation}", truststoreLocation)
                    .Replace("{truststorePassword}", truststorePassword)
                    .Replace("{bootstrapServerAndPort}", $"{bootstrapServerAndPort}");

            ExternalConnectionServiceInfo connectionInformation = GetConnectionInformation();

            // Create URL - ensure to encode the values if they contain special characters
            var url = $"/v1/project/{Uri.EscapeDataString(connectionInformation.ProjectName)}" +
                $"/service/{Uri.EscapeDataString(connectionInformation.KafkaConnectServiceName)}/connectors";

            ThrowIfConfigurationValueIsMissing("OnlineMode:ApiBaseAddress");

            using (var client = new HttpClient { BaseAddress = new Uri(_configuration["OnlineMode:ApiBaseAddress"]) })
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", connectionInformation.BearerToken);

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                _logger.LogInformation("Sending POST request to {Url}", url);

                var response = await client.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("Response received.");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("RegisterConnectorAsync Error: {StatusCode}. Details: {ErrorContent}", response.StatusCode, errorContent);
                    throw new Exception($"Error during connector registration: {response.StatusCode}. ErrorContent: {errorContent}");
                }
            }
        }

        [Obsolete("We use DeleteConnectorsAsync - plural version")]
        public async Task DeleteConnectorAsync(string connectorName)
        {
            if (string.IsNullOrWhiteSpace(connectorName))
            {
                throw new ArgumentException("Connector name must be provided.", nameof(connectorName));
            }

            connectorName = GetConnectorNameWithSuffix(connectorName);

            ExternalConnectionServiceInfo connectionInformation = GetConnectionInformation();

            // Build URL for deletion (connector name must be URL-encoded)
            var url = $"/v1/project/{Uri.EscapeDataString(connectionInformation.ProjectName)}" +
                $"/service/{Uri.EscapeDataString(connectionInformation.KafkaConnectServiceName)}/connectors/{Uri.EscapeDataString(connectorName)}";

            ThrowIfConfigurationValueIsMissing("OnlineMode:ApiBaseAddress");

            using (var client = new HttpClient { BaseAddress = new Uri(_configuration["OnlineMode:ApiBaseAddress"]) })
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", connectionInformation.BearerToken);

                _logger.LogInformation("Sending DELETE request to {Url}", url);

                var response = await client.DeleteAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Connector '{ConnectorName}' deleted successfully.", connectorName);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("DeleteConnectorAsync Error: {StatusCode}. Details: {ErrorContent}", response.StatusCode, errorContent);
                    throw new Exception($"Error during connector deletion: {response.StatusCode}");
                }
            }
        }

        public async Task DeleteConnectorsAsync(string connectorNamePrefix)
        {
            if (string.IsNullOrWhiteSpace(connectorNamePrefix))
            {
                throw new ArgumentException("Connector prefix must be provided.", nameof(connectorNamePrefix));
            }

            // Get list of all registered connectors
            List<string> allConnectors = await GetListOfRegisteredConnectors();
            if (allConnectors == null || !allConnectors.Any())
            {
                _logger.LogInformation("No connectors found.");
                return;
            }

            // Filter connectors based on prefix
            var connectorsToDelete = allConnectors
                .Where(c => c.StartsWith(connectorNamePrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!connectorsToDelete.Any())
            {
                _logger.LogInformation("No connectors starting with '{Prefix}' were found.", connectorNamePrefix);
                return;
            }

            _logger.LogInformation("Found {Count} connectors to delete: {Connectors}",
                connectorsToDelete.Count, string.Join(", ", connectorsToDelete));

            ExternalConnectionServiceInfo connectionInformation = GetConnectionInformation();

            ThrowIfConfigurationValueIsMissing("OnlineMode:ApiBaseAddress");

            using var client = new HttpClient { BaseAddress = new Uri(_configuration["OnlineMode:ApiBaseAddress"]) };
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", connectionInformation.BearerToken);

            // Remove every connector found
            foreach (var connector in connectorsToDelete)
            {
                var deleteUrl = $"/v1/project/{Uri.EscapeDataString(connectionInformation.ProjectName)}" +
                                $"/service/{Uri.EscapeDataString(connectionInformation.KafkaConnectServiceName)}/connectors/{Uri.EscapeDataString(connector)}";

                _logger.LogInformation("Sending DELETE request to {Url} for connector {Connector}", deleteUrl, connector);

                var deleteResponse = await client.DeleteAsync(deleteUrl);
                if (deleteResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Connector '{Connector}' deleted successfully.", connector);
                }
                else
                {
                    var deleteError = await deleteResponse.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Error deleting connector {connector}: {deleteResponse.StatusCode}. Details: {deleteError}");
                }
            }
        }

        public async Task RestartConnectorAsync(string connectorName)
        {
            if (string.IsNullOrWhiteSpace(connectorName))
            {
                throw new ArgumentException("Connector name must be provided.", nameof(connectorName));
            }

            connectorName = GetConnectorNameWithSuffix(connectorName);

            ExternalConnectionServiceInfo connectionInformation = GetConnectionInformation();

            // Build URL for restarting the connector (connector name must be URL-encoded)
            var url = $"/v1/project/{Uri.EscapeDataString(connectionInformation.ProjectName)}" +
                      $"/service/{Uri.EscapeDataString(connectionInformation.KafkaConnectServiceName)}/connectors/{Uri.EscapeDataString(connectorName)}/restart";

            ThrowIfConfigurationValueIsMissing("OnlineMode:ApiBaseAddress");

            using (var client = new HttpClient { BaseAddress = new Uri(_configuration["OnlineMode:ApiBaseAddress"]) })
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", connectionInformation.BearerToken);

                _logger.LogInformation("Sending POST request to restart connector at {Url}", url);

                // Sending POST request with no content (empty payload)
                var response = await client.PostAsync(url, null);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Connector '{ConnectorName}' restarted successfully.", connectorName);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("RestartConnectorAsync Error: {StatusCode}. Details: {ErrorContent}", response.StatusCode, errorContent);
                    throw new Exception($"Error during connector restart: {response.StatusCode}");
                }
            }
        }

        public async Task<string> GetConnectorStatusAsync(string connectorName)
        {
            connectorName = GetConnectorNameWithSuffix(connectorName);

            ExternalConnectionServiceInfo connectionInformation = GetConnectionInformation();

            // Build URL for retrieving connector status (connector name must be URL-encoded)
            var url = $"/v1/project/{Uri.EscapeDataString(connectionInformation.ProjectName)}" +
                      $"/service/{Uri.EscapeDataString(connectionInformation.KafkaConnectServiceName)}/connectors/{Uri.EscapeDataString(connectorName)}/status";

            ThrowIfConfigurationValueIsMissing("OnlineMode:ApiBaseAddress");

            using (var client = new HttpClient { BaseAddress = new Uri(_configuration["OnlineMode:ApiBaseAddress"]) })
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", connectionInformation.BearerToken);

                _logger.LogInformation("Sending GET request for connector status to {Url}", url);

                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string statusContent = await response.Content.ReadAsStringAsync();
                    dynamic jsonObject = JsonConvert.DeserializeObject(statusContent);

                    // Get the global state from the status object
                    string globalState = jsonObject.status.state;

                    // Iterate through the tasks array to check for any FAILED task
                    bool anyFailed = false;
                    foreach (var task in jsonObject.status.tasks)
                    {
                        string taskState = task.state;
                        if (taskState == "FAILED")
                        {
                            anyFailed = true;
                            break;
                        }
                    }

                    // If any task is FAILED, overall state is FAILED; otherwise, use the global state
                    string overallState = anyFailed ? "FAILED" : globalState;
                    _logger.LogInformation("Connector '{ConnectorName}' status: {StatusContent}", connectorName, overallState);

                    return $"{{ \"connector\": {{ \"state\": \"{overallState}\" }} }}";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("GetConnectorStatusAsync Error: {StatusCode}. Details: {ErrorContent}", response.StatusCode, errorContent);
                    throw new Exception($"Error retrieving connector status: {response.StatusCode}");
                }
            }
        }

        public async Task StopConnectorAsync(string connectorName)
        {
            if (string.IsNullOrWhiteSpace(connectorName))
            {
                throw new ArgumentException("Connector name must be provided.", nameof(connectorName));
            }

            connectorName = GetConnectorNameWithSuffix(connectorName);
            ExternalConnectionServiceInfo connectionInformation = GetConnectionInformation();

            // Build URL for retrieving connector status
            var statusUrl = $"/v1/project/{Uri.EscapeDataString(connectionInformation.ProjectName)}" +
                            $"/service/{Uri.EscapeDataString(connectionInformation.KafkaConnectServiceName)}/connectors/{Uri.EscapeDataString(connectorName)}/status";

            ThrowIfConfigurationValueIsMissing("OnlineMode:ApiBaseAddress");

            using (var client = new HttpClient { BaseAddress = new Uri(_configuration["OnlineMode:ApiBaseAddress"]) })
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", connectionInformation.BearerToken);

                _logger.LogInformation("Sending GET request for connector status to {Url}", statusUrl);
                var response = await client.GetAsync(statusUrl);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Error retrieving connector status: {StatusCode}. Details: {ErrorContent}", response.StatusCode, errorContent);
                    throw new Exception($"Error retrieving connector status: {response.StatusCode}");
                }

                string statusContent = await response.Content.ReadAsStringAsync();
                dynamic jsonObject = JsonConvert.DeserializeObject(statusContent);

                // Assume that global connector state is stored in jsonObject.status.state
                string globalState = jsonObject.status.state;
                _logger.LogInformation("Connector '{ConnectorName}' status: {GlobalState}", connectorName, globalState);

                if (globalState.Equals("RUNNING", StringComparison.OrdinalIgnoreCase))
                {
                    // Build URL for pausing the connector
                    var pauseUrl = $"/v1/project/{Uri.EscapeDataString(connectionInformation.ProjectName)}" +
                                   $"/service/{Uri.EscapeDataString(connectionInformation.KafkaConnectServiceName)}/connectors/{Uri.EscapeDataString(connectorName)}/pause";

                    _logger.LogInformation("Connector '{ConnectorName}' is running. Sending pause request to {Url}", connectorName, pauseUrl);
                    var pauseResponse = await client.PutAsync(pauseUrl, null);
                    if (pauseResponse.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Connector '{ConnectorName}' has been paused successfully.", connectorName);
                    }
                    else
                    {
                        var pauseError = await pauseResponse.Content.ReadAsStringAsync();
                        _logger.LogError("Error pausing connector '{ConnectorName}': {StatusCode}. Details: {ErrorContent}", connectorName, pauseResponse.StatusCode, pauseError);
                        throw new Exception($"Error pausing connector '{connectorName}': {pauseResponse.StatusCode}");
                    }
                }
                else
                {
                    _logger.LogInformation("Connector '{ConnectorName}' is not running (state: {GlobalState}). No action taken.", connectorName, globalState);
                }
            }
        }

        public async Task<bool> IsConnectorRegistered(string connectorName)
        {
            try
            {
                // We add suffix to connector name if it's required
                connectorName = GetConnectorNameWithSuffix(connectorName);

                // We get the list of all registered connectors
                List<string> connectors = await GetListOfRegisteredConnectors();

                // We check if connector with the given name exists on the list (ignoring letters case)
                return connectors.Any(c => c.Equals(connectorName, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking if connector '{connectorName}' is registered: {ex.Message}");
                throw new InvalidOperationException($"Error checking if connector '{connectorName}' is registered: {ex}");
            }
        }

        private string GetEmbeddedResource(int connector)
        {
            var assembly = Assembly.Load("KUK.Common");
            var resourceName = $"KUK.Common.Assets.DebeziumConnectorConfigOnline{connector}.json";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                string result = reader.ReadToEnd();

                string dbNameFromConfiguration = string.Empty;
                string tableIncludeListFromConfiguration = string.Empty;

                switch (connector)
                {
                    case 1:
                        dbNameFromConfiguration = _configuration["ConnectorDetails:DbNameOld"];
                        tableIncludeListFromConfiguration = _configuration["ConnectorDetails:TableIncludeListOld"];
                        break;
                    case 2:
                        dbNameFromConfiguration = _configuration["ConnectorDetails:DbNameNew"];
                        tableIncludeListFromConfiguration = _configuration["ConnectorDetails:TableIncludeListNew"];
                        break;
                    default:
                        throw new InvalidOperationException($"Invalid number of connector: {connector}");
                }

                if (string.IsNullOrEmpty(dbNameFromConfiguration) || string.IsNullOrEmpty(tableIncludeListFromConfiguration))
                {
                    throw new InvalidOperationException($"Value of DbName or TableIncludeList is empty for connector {connector}");
                }

                return result
                    .Replace("{dbName}", dbNameFromConfiguration)
                    .Replace("{tableIncludeList}", tableIncludeListFromConfiguration);
            }
        }

        private void ThrowIfEnvironmentVariableMissing(string environmentVariableKey)
        {
            var value = Environment.GetEnvironmentVariable(environmentVariableKey);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Value of {environmentVariableKey} is missing in environment variables");
            }
        }

        private ExternalConnectionServiceInfo GetConnectionInformation()
        {
            // Get the Bearer token from environment variables
            ThrowIfConfigurationValueIsMissing("OnlineMode:BearerTokenEnvironmentVariableKey");
            string bearerToken = Environment.GetEnvironmentVariable(_configuration["OnlineMode:BearerTokenEnvironmentVariableKey"]);
            if (string.IsNullOrEmpty(bearerToken))
            {
                throw new Exception("Bearer token not found in environment variables.");
            }

            // Get project and service names from configuration
            string project = _configuration["OnlineMode:Project"];
            if (string.IsNullOrEmpty(project))
            {
                throw new Exception("Project name is not configured in appsettings.");
            }

            string serviceName = _configuration["OnlineMode:KafkaConnectService"];
            if (string.IsNullOrEmpty(serviceName))
            {
                throw new Exception("Service name is not configured in appsettings.");
            }

            var connectionInformation = new ExternalConnectionServiceInfo()
            {
                BearerToken = bearerToken,
                ProjectName = project,
                KafkaConnectServiceName = serviceName
            };

            return connectionInformation;
        }

        private string GetConnectorNameWithSuffix(string connectorName)
        {
            if (string.IsNullOrWhiteSpace(connectorName))
            {
                throw new ArgumentException("Connector name must be provided.", nameof(connectorName));
            }

            if (connectorName.StartsWith("old-to-new"))
            {
                string suffix = _configuration["ConnectorDetails:OldConnectorSuffix"];
                connectorName += suffix;
            }
            else if (connectorName.StartsWith("new-to-old"))
            {
                string suffix = _configuration["ConnectorDetails:NewConnectorSuffix"];
                connectorName += suffix;
            }
            else
            {
                throw new InvalidOperationException($"Unknown connector name {connectorName}");
            }

            return connectorName;
        }

        private void ThrowIfConfigurationValueIsMissing(string configurationKey)
        {
            var topic = _configuration[configurationKey];
            if (string.IsNullOrWhiteSpace(topic))
            {
                throw new InvalidOperationException($"Value of {configurationKey} is not set in appsettings.json");
            }
        }
    }
}
