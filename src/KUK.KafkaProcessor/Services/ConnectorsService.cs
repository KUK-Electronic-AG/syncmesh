using System.Text.Json;
using KUK.Common.Services;
using KUK.Common.Utilities;
using KUK.KafkaProcessor.Models;
using KUK.KafkaProcessor.Services.Interfaces;
using KUK.KafkaProcessor.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KUK.KafkaProcessor.Services
{
    public class ConnectorsService : IConnectorsService
    {
        private readonly IConfiguration _configuration;
        private readonly IConnectorsRegistrationService _connectorsRegistrationService;
        private readonly ILogger<ConnectorsService> _logger;
        private readonly GlobalState _globalState;
        private readonly IUtilitiesService _utilitiesService;
        private readonly IExternalConnectionService _externalConnectionService;
        private readonly IKafkaService _kafkaService;

        public ConnectorsService(
            IConfiguration configuration,
            IConnectorsRegistrationService connectorsRegistrationService,
            ILogger<ConnectorsService> logger,
            GlobalState globalState,
            IUtilitiesService utilitiesService,
            IExternalConnectionService externalConnectionService,
            IKafkaService kafkaService)
        {
            _configuration = configuration;
            _connectorsRegistrationService = connectorsRegistrationService;
            _logger = logger;
            _globalState = globalState;
            _utilitiesService = utilitiesService;
            _externalConnectionService = externalConnectionService;
            _kafkaService = kafkaService;
        }

        public async Task ValidateOldConnectorPrerequisites()
        {
            _logger.LogDebug($"Started method {nameof(ValidateOldConnectorPrerequisites)}");

            var oldToNewTopic = _configuration["Kafka:OldToNewTopic"];
            var schemaChangesOldTopic = _configuration["Kafka:SchemaChangesOldTopic"];
            var dbHistoryOldTopic = _configuration["Kafka:DbHistoryOldTopic"];

            if (string.IsNullOrEmpty(oldToNewTopic) || string.IsNullOrEmpty(schemaChangesOldTopic) || string.IsNullOrEmpty(dbHistoryOldTopic))
            {
                throw new InvalidOperationException($"Missing Kafka parameters in appsettings");
            }

            // REMARK: There is issue creating these topics by old-to-new connector so we create them manually
            await _kafkaService.CreateTopicAsync(oldToNewTopic, 10, 3);
            await _kafkaService.CreateTopicAsync(schemaChangesOldTopic, 1, 3);
            await _kafkaService.CreateTopicAsync(dbHistoryOldTopic, 1, 3);
        }

        public async Task ValidateNewConnectorPrerequisites()
        {
            _logger.LogDebug($"Started method {nameof(ValidateNewConnectorPrerequisites)}");

            var newToOldTopic = _configuration["Kafka:NewToOldTopic"];
            var schemaChangesNewTopic = _configuration["Kafka:SchemaChangesNewTopic"];
            var dbHistoryNewTopic = _configuration["Kafka:DbHistoryNewTopic"];

            if (string.IsNullOrEmpty(newToOldTopic) || string.IsNullOrEmpty(schemaChangesNewTopic) || string.IsNullOrEmpty(dbHistoryNewTopic))
            {
                throw new InvalidOperationException($"Missing Kafka parameters in appsettings");
            }

            // REMARK: There is issue creating these topics by new-to-old connector so we create them manually
            await _kafkaService.CreateTopicAsync(newToOldTopic, 10, 3);
            await _kafkaService.CreateTopicAsync(schemaChangesNewTopic, 1, 3);
            await _kafkaService.CreateTopicAsync(dbHistoryNewTopic, 1, 3);
        }

        public async Task RegisterOldConnector()
        {
            string oldConnectorSuffix = _configuration["ConnectorDetails:OldConnectorSuffix"] ?? string.Empty;
            string connectorName = "old-to-new-connector" + oldConnectorSuffix;
            _logger.LogInformation($"Registering {connectorName}");

            // Start old connector
            bool isOldRegistered = await _connectorsRegistrationService.IsConnectorRegistered(connectorName);
            if (!isOldRegistered)
            {
                _logger.LogInformation($"Registering {connectorName}");
                ConnectorRegistrationResult oldConnectorRegistrationResult = await _connectorsRegistrationService.RegisterConnectorAsync(connectorName);
                if (!oldConnectorRegistrationResult.Success)
                {
                    var errorMessage = $"Failure starting {connectorName}. Going to return. Result: {oldConnectorRegistrationResult.Message}";
                    _logger.LogError(errorMessage);
                    throw new InvalidOperationException(errorMessage);
                }
                await _connectorsRegistrationService.WaitForConnectorInitializationAsync(connectorName);
                _logger.LogInformation($"Registered {connectorName}");
            }
            else
            {
                _logger.LogInformation($"Skipping registration of {connectorName} because it is already registered");
            }
        }

        public async Task<bool> WaitForProcessorToFinishInitializationAsync()
        {
            var timeout = TimeSpan.FromSeconds(300);
            var interval = TimeSpan.FromSeconds(1);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            while (stopwatch.Elapsed < timeout)
            {
                var snapshotLastReceived = _globalState.SnapshotLastReceived;
                if (snapshotLastReceived)
                {
                    return true;
                }
                await Task.Delay(interval);
            }

            _logger.LogError("Service did not initialize within the timeout period.");
            throw new InvalidOperationException($"Cannot check if SnapshotLastReceived due to service not being initialized");
        }

        public async Task RegisterNewConnector()
        {
            string newConnectorSuffix = _configuration["ConnectorDetails:NewConnectorSuffix"] ?? string.Empty;
            string connectorName = "new-to-old-connector" + newConnectorSuffix;
            _logger.LogInformation($"Registering {connectorName}");

            // Start new connector
            bool isNewRegistered = await _connectorsRegistrationService.IsConnectorRegistered(connectorName);
            if (!isNewRegistered)
            {
                var mode = _utilitiesService.GetOnlineOrDockerMode();
                if (mode == ApplicationDestinationMode.Online)
                {
                    _logger.LogInformation($"Creating publication for all tables");
                    await _externalConnectionService.CreatePublicationForAllTables();
                }

                _logger.LogInformation($"Registering {connectorName}");
                ConnectorRegistrationResult newConnectorRegistrationResult = await _connectorsRegistrationService.RegisterConnectorAsync(connectorName);
                if (!newConnectorRegistrationResult.Success)
                {
                    var errorMessage = $"Failure starting {connectorName}. Going to return. Result: {newConnectorRegistrationResult.Message}";
                    _logger.LogError(errorMessage);
                    throw new InvalidOperationException(errorMessage);
                }
                await _connectorsRegistrationService.WaitForConnectorInitializationAsync(connectorName);
                _logger.LogInformation($"Registered {connectorName}");
            }
            else
            {
                _logger.LogInformation($"Skipping registration of {connectorName} because it is already registered");
            }
        }

        public async Task WaitForBothConnectorsToBeOperational(bool waitForOldConnector, bool waitForNewConnector, ApplicationDestinationMode mdoe)
        {
            var mode = _utilitiesService.GetOnlineOrDockerMode();

            if (!waitForOldConnector && !waitForNewConnector)
            {
                _logger.LogWarning("No connectors specified to wait for.");
                return;
            }

            while (true)
            {
                bool oldOperational = true;
                bool newOperational = true;
                string oldConnectorStatus = string.Empty;
                string newConnectorStatus = string.Empty;

                if (waitForOldConnector)
                {
                    oldConnectorStatus = await FetchConnectorStatusAsync("old-to-new-connector", mode);
                    _logger.LogInformation($"[DEBUGLOG] oldConnectorStatus = {oldConnectorStatus}");
                    oldOperational = IsConnectorOperational(oldConnectorStatus, mode);
                }

                if (waitForNewConnector)
                {
                    newConnectorStatus = await FetchConnectorStatusAsync("new-to-old-connector", mode);
                    _logger.LogInformation($"[DEBUGLOG] newConnectorStatus = {newConnectorStatus}");
                    newOperational = IsConnectorOperational(newConnectorStatus, mode);
                }

                if (oldOperational && newOperational)
                {
                    _logger.LogInformation("All required connectors are operational. Old status: {oldStatus}, New status: {newStatus}",
                        oldConnectorStatus, newConnectorStatus);
                    break;
                }
                else
                {
                    _logger.LogInformation(
                        $"Connectors are not operational yet. (Input parameters: waitForOldConnector={waitForOldConnector}, waitForNewConnector={waitForNewConnector}). " +
                        $"Old status: {oldConnectorStatus}, New status: {newConnectorStatus}. Waiting...");
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        private async Task<string> FetchConnectorStatusAsync(string connectorName, ApplicationDestinationMode mode)
        {
            return mode switch
            {
                ApplicationDestinationMode.Docker => await GetLocalDockerConnectorStatusAsync(connectorName),
                ApplicationDestinationMode.Online => await _externalConnectionService.GetConnectorStatusAsync(connectorName),
                _ => throw new InvalidOperationException($"Unknown mode {mode}")
            };
        }

        private bool IsConnectorOperational(string connectorStatusJson, ApplicationDestinationMode mode)
        {
            try
            {
                var connectorInfo = JsonSerializer.Deserialize<ConnectorInfo>(connectorStatusJson);

                if (connectorInfo.Connector.State != "RUNNING")
                {
                    return false;
                }

                if (mode == ApplicationDestinationMode.Docker)
                {
                    if (connectorInfo.Tasks == null || !connectorInfo.Tasks.Any())
                    {
                        _logger.LogInformation("Connector is running but no tasks are present.");
                        return false;
                    }

                    bool anyTaskRunning = connectorInfo.Tasks.Any(task => task.State == "RUNNING");
                    if (!anyTaskRunning)
                    {
                        _logger.LogInformation("Connector is running but no tasks are in state RUNNING.");
                    }

                    return anyTaskRunning;
                }

                return true;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse connector status JSON.");
                return false;
            }
        }

        private async Task<string> GetLocalDockerConnectorStatusAsync(string connectorName)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await client.GetAsync($"http://localhost:8084/connectors/{connectorName}/status");
                    response.EnsureSuccessStatusCode();

                    string content = await response.Content.ReadAsStringAsync();
                    return content;
                }
                catch (Exception ex)
                {
                    return $"Error while getting status: {ex.Message}";
                }
            }
        }
    }
}
