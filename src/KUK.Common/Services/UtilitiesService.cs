using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;

namespace KUK.Common.Services
{
    public class UtilitiesService : IUtilitiesService
    {
        private readonly IServiceProvider _serviceProvider;

        public UtilitiesService(IServiceProvider serviceProvider = null)
        {
            _serviceProvider = serviceProvider;
        }

        public string GetEnvironmentDestinationString()
        {
            var environmentVariableName = "DebeziumWorker_EnvironmentDestination";
            string value = Environment.GetEnvironmentVariable(environmentVariableName);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Environment variable {environmentVariableName} is not set. Suggested values are Docker or Online.");
            }
            return value;
        }

        public ApplicationDestinationMode GetOnlineOrDockerMode()
        {
            // Check if this is for Docker or Online run
            var environmentDestinationVariableName = "DebeziumWorker_EnvironmentDestination";
            string environmentDestination = Environment.GetEnvironmentVariable(environmentDestinationVariableName);
            if (string.IsNullOrWhiteSpace(environmentDestination))
                throw new ArgumentException($"Variable {environmentDestination} is not set in environment variables.");
            switch (environmentDestination.ToLower())
            {
                case "docker":
                    return ApplicationDestinationMode.Docker;
                case "online":
                    return ApplicationDestinationMode.Online;
                default:
                    throw new InvalidOperationException(
                        $"Unknown environment variable value for {environmentDestination}. " +
                        $"Found {environmentDestination}. Expected: Docker or Online");
            }
        }

        public IServiceProvider GetServiceProvider()
        {
            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("ServiceProvider is not initialized. Make sure UtilitiesService is constructed with an IServiceProvider instance.");
            }
            return _serviceProvider;
        }

        public string GetConnectionString(IConfiguration configuration, string databaseKey, bool useRoot = false)
        {
            var connectionStringWithPlaceholders = configuration[$"Databases:{databaseKey}:ConnectionString"];
            string password = string.Empty;
            string environmentDestination = GetEnvironmentDestinationString();
            if (!useRoot)
            {
                password = Environment.GetEnvironmentVariable($"DebeziumWorker_{environmentDestination}_{databaseKey}DataChangesPassword");
                if (string.IsNullOrEmpty(password))
                {
                    throw new InvalidOperationException(
                        $"Database DataChanges password is not set for {databaseKey}. " +
                        $"Expected DebeziumWorker_{databaseKey}DataChangesPassword environment variable to be present.");
                }
            }
            else
            {
                password = Environment.GetEnvironmentVariable($"DebeziumWorker_{environmentDestination}_{databaseKey}RootPassword");
                if (string.IsNullOrEmpty(password))
                {
                    throw new InvalidOperationException(
                        $"Database Root password is not set for {databaseKey}. " +
                        $"Expected DebeziumWorker_{databaseKey}RootPassword environment variable to be present.");
                }
            }
            var serverName = configuration[$"Databases:{databaseKey}:ServerName"];
            var serverPort = configuration[$"Databases:{databaseKey}:ServerPort"];
            var connectionString = connectionStringWithPlaceholders
                .Replace("{serverName}", serverName)
                .Replace("{serverPort}", serverPort)
                .Replace("{password}", password);
            if (useRoot)
            {
                // REMARK: Because we initialize MySQL80 in non-standard way (as per InitializeDatabaseAsync in QuickActionsService),
                // we need to use root account for checking old database status.
                connectionString = ModifyConnectionString(connectionString, configuration["Databases:RootAccountName"]);
            }
            return connectionString;
        }

        private static string ModifyConnectionString(string connectionString, string newUserId)
        {
            var builder = new MySqlConnectionStringBuilder(connectionString);
            builder.UserID = newUserId;
            return builder.ToString();
        }
    }
}
