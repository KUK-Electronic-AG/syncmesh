using KUK.Common.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KUK.Common
{
    public static class AppSettingsConfigExtensions
    {
        public static IServiceCollection AddAppSettingsConfig(this IServiceCollection services, IConfiguration configuration)
        {
            var utilitiesService = new UtilitiesService();

            var appSettingsConfig = new AppSettingsConfig
            {
                RootOldDatabaseConnectionString = utilitiesService.GetConnectionString(configuration, "OldDatabase", useRoot: true),
                OldDatabaseConnectionString = utilitiesService.GetConnectionString(configuration, "OldDatabase"),
                NewDatabaseConnectionString = utilitiesService.GetConnectionString(configuration, "NewDatabase"),
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
    }
}