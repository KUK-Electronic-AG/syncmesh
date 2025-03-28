using KUK.Common.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;

namespace KUK.Common
{
    public static class AppSettingsConfigExtensions
    {
        public static IServiceCollection AddAppSettingsConfig(this IServiceCollection services, IConfiguration configuration)
        {
            var appSettingsConfig = new AppSettingsConfig
            {
                RootOldDatabaseConnectionString = GetConnectionString(configuration, "OldDatabase", useRoot: true),
                OldDatabaseConnectionString = GetConnectionString(configuration, "OldDatabase"),
                NewDatabaseConnectionString = GetConnectionString(configuration, "NewDatabase"),
                DockerComposeDirectory = configuration["DockerComposeDirectory"] ?? string.Empty,
                ProcessorExePath = AssignProcessorPath(configuration["ProcessorExePath"], "ProcessorExePath", ProcessorPathEnum.ProcessorExePath),
                ProcessorLogsPath = AssignProcessorPath(configuration["ProcessorLogsPath"], "ProcessorLogsPath", ProcessorPathEnum.ProcessorLogsPath),
                FirstConnectorJsonPath = AssignConnectorPath(configuration["FirstConnectorJsonPath"], "FirstConnectorJsonPath", ConnectorFullPathEnum.FirstConnector),
                SecondConnectorJsonPath = AssignConnectorPath(configuration["SecondConnectorJsonPath"], "SecondConnectorJsonPath", ConnectorFullPathEnum.SecondConnector),
                ProcessorAppSettingsPath = AssignProcessorAppSettingsPath(configuration["ProcessorAppSettingsPath"], "ProcessorAppSettingsPath"),
                OldDatabaseName = configuration["Databases:OldDatabase:ServerName"],
                OldDatabasePort = Convert.ToInt32(configuration["Databases:OldDatabase:ServerPort"]),
                NewDatabaseName = configuration["Databases:NewDatabase:ServerName"],
                NewDatabasePort = Convert.ToInt32(configuration["Databases:NewDatabase:ServerPort"]),
                KafkaBootstrapServers = configuration["Kafka:BootstrapServers"],
                RootAccountName = configuration["Databases:RootAccountName"],
                PostgresAccountName = configuration["Databases:PostgresAccountName"],
            };

            services.AddSingleton(appSettingsConfig);
            return services;
        }

        public static IServiceCollection AddDatabaseContexts(this IServiceCollection services, AppSettingsConfig config)
        {
            services.AddDbContext<Chinook1DataChangesContext>(options => options.UseMySQL(config.OldDatabaseConnectionString));
            services.AddScoped<IChinook1DataChangesContext>(provider => provider.GetService<Chinook1DataChangesContext>());

            services.AddDbContext<Chinook1RootContext>(options => options.UseMySQL(config.RootOldDatabaseConnectionString));
            services.AddScoped<IChinook1RootContext>(provider => provider.GetService<Chinook1RootContext>());

            services.AddDbContext<Chinook2Context>(options => options.UseNpgsql(config.NewDatabaseConnectionString));
            services.AddScoped<IChinook2Context>(provider => provider.GetService<Chinook2Context>());

            return services;
        }

        private static string AssignProcessorPath(string path, string keyName, ProcessorPathEnum processorPathEnum)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                var baseDirectory = new DirectoryInfo(AppContext.BaseDirectory);
                switch (processorPathEnum)
                {
                    case ProcessorPathEnum.ProcessorExePath:
                        return Path.Combine(baseDirectory.Parent.Parent.Parent.Parent.FullName, "KUK.KafkaProcessor", "bin", "Debug", "net8.0");
                    case ProcessorPathEnum.ProcessorLogsPath:
                        return Path.Combine(baseDirectory.Parent.Parent.Parent.Parent.FullName, "KUK.KafkaProcessor", "bin", "Debug", "net8.0", "Logs");
                    default:
                        throw new InvalidOperationException($"Path to '{path}' in {keyName} does not exist and no default path is available");
                }
            }
            return path;
        }

        private static string AssignConnectorPath(string filePath, string keyName, ConnectorFullPathEnum connectorPathEnum)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                var baseDirectory = new DirectoryInfo(AppContext.BaseDirectory);
                var targetAssetsDirectory = Path.Combine(baseDirectory.Parent.Parent.Parent.Parent.FullName, "KUK.Common", "Assets");
                if (!Directory.Exists(targetAssetsDirectory))
                {
                    throw new InvalidOperationException($"Directory {targetAssetsDirectory} does not exist");
                }
                var finalPathToFile = string.Empty;
                switch (connectorPathEnum)
                {
                    case ConnectorFullPathEnum.FirstConnector:
                        finalPathToFile = Path.Combine(targetAssetsDirectory, "debezium-connector-config-1.json");
                        break;
                    case ConnectorFullPathEnum.SecondConnector:
                        finalPathToFile = Path.Combine(targetAssetsDirectory, "debezium-connector-config-2.json");
                        break;
                    default:
                        throw new InvalidOperationException($"File '{filePath}' in {keyName} does not exist and no default file path is available");
                }
                if (!File.Exists(finalPathToFile))
                {
                    throw new InvalidOperationException($"File {finalPathToFile} does not exist");
                }
                return finalPathToFile;
            }
            if (!File.Exists(filePath))
            {
                throw new InvalidOperationException($"File {filePath} does not exist");
            }
            return filePath;
        }

        private static string AssignProcessorAppSettingsPath(string path, string keyName)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                var baseDirectory = new DirectoryInfo(AppContext.BaseDirectory);
                return Path.Combine(baseDirectory.Parent.Parent.Parent.Parent.FullName, "KUK.KafkaProcessor", "bin", "Debug", "net8.0");
            }
            return path;
        }

        private static string GetConnectionString(IConfiguration configuration, string databaseKey, bool useRoot = false)
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

        private static string GetEnvironmentDestinationString()
        {
            // REMARK: This is code duplication from UtilitiesService.GetEnvironmentDestinationString
            var environmentVariableName = "DebeziumWorker_EnvironmentDestination";
            string value = Environment.GetEnvironmentVariable(environmentVariableName);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Environment variable {environmentVariableName} is not set. Suggested values are Docker or Online.");
            }
            return value;
        }
    }
}