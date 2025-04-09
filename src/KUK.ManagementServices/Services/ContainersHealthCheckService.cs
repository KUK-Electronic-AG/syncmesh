using Confluent.Kafka;
using Docker.DotNet;
using Docker.DotNet.Models;
using KUK.Common.Services;
using KUK.Common.Utilities;
using KUK.ManagementServices.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using Npgsql;

namespace KUK.ManagementServices.Services
{
    public class ContainersHealthCheckService : IContainersHealthCheckService
    {
        private readonly DockerClient _dockerClient;
        private readonly HttpClient _httpClient;
        private readonly IUtilitiesService _utilitiesService;
        private readonly IConfiguration _configuration;

        public ContainersHealthCheckService(
            IUtilitiesService utilitiesService,
            IConfiguration configuration)
        {
            _dockerClient = new DockerClientConfiguration(new Uri("npipe://./pipe/docker_engine")).CreateClient();
            _httpClient = new HttpClient();
            _utilitiesService = utilitiesService;
            _configuration = configuration;
        }

        public async Task<ServiceActionStatus> WaitForContainersAsync(List<string> containerNames, TimeSpan timeout)
        {
            var startTime = DateTime.UtcNow;
            var containers = await _dockerClient.Containers.ListContainersAsync(new ContainersListParameters
            {
                All = true
            });
            while (DateTime.UtcNow - startTime < timeout)
            {

                var allRunning = containerNames.All(name => containers.Any(c => c.Names.Contains($"/{name}") && c.State == "running"));

                if (allRunning)
                {
                    return new ServiceActionStatus
                    {
                        Success = true,
                        Message = "All containers are running."
                    };
                }

                await Task.Delay(1000); // Wait for 1 second before retrying
            }

            var finalNotRunningContainers = containerNames.Where(name => !containers.Any(c => c.Names.Contains($"/{name}") && c.State == "running")).ToList();

            return new ServiceActionStatus
            {
                Success = false,
                Message = $"Not all containers became running in the allotted time. The following containers are not running: {string.Join(", ", finalNotRunningContainers)}"
            };
        }

        public async Task<ServiceActionStatus> WaitForServicesInContainersAsync(List<string> containerNames, TimeSpan timeout)
        {
            string environmentDestination = _utilitiesService.GetEnvironmentDestinationString();

            string oldDatabaseRootPasswordEnvironmentVariable = $"DebeziumWorker_{environmentDestination}_OldDatabaseRootPassword";
            var oldDatabaseRootPassword = Environment.GetEnvironmentVariable(oldDatabaseRootPasswordEnvironmentVariable);
            ThrowIfNullOrEmpty(oldDatabaseRootPassword, oldDatabaseRootPasswordEnvironmentVariable);

            string newDatabaseRootPasswordEnvironmentVariable = $"DebeziumWorker_{environmentDestination}_NewDatabaseRootPassword";
            var newDatabaseRootPassword = Environment.GetEnvironmentVariable(newDatabaseRootPasswordEnvironmentVariable);
            ThrowIfNullOrEmpty(newDatabaseRootPassword, newDatabaseRootPasswordEnvironmentVariable);

            string postgresMartenRootUserNameEnvironmentVariable = $"DebeziumWorker_{environmentDestination}_PostgresMartenRootUserName";
            var postgresMartenRootUserName = Environment.GetEnvironmentVariable(postgresMartenRootUserNameEnvironmentVariable);
            ThrowIfNullOrEmpty(postgresMartenRootUserName, postgresMartenRootUserNameEnvironmentVariable);

            string postgresMartenRootPasswordEnvironmentVariable = $"DebeziumWorker_{environmentDestination}_PostgresMartenRootPassword";
            var postgresMartenRootPassword = Environment.GetEnvironmentVariable(postgresMartenRootPasswordEnvironmentVariable);
            ThrowIfNullOrEmpty(postgresMartenRootPassword, postgresMartenRootPasswordEnvironmentVariable);

            string postgresMartenDatabaseNameEnvironmentVariable = $"DebeziumWorker_{environmentDestination}_PostgresMartenDatabaseName";
            var postgresMartenDatabaseName = Environment.GetEnvironmentVariable(postgresMartenDatabaseNameEnvironmentVariable);
            ThrowIfNullOrEmpty(postgresMartenDatabaseName, postgresMartenDatabaseNameEnvironmentVariable);

            bool isKafkaHealthy = false;
            bool isSchemaRegistryHealthy = false;
            bool isKafkaConnectHealthy = false;
            bool isControlCenterHealthy = false;
            bool isMySql80Healthy = false;
            bool isDestinationPostgresHealthy = false;
            bool isMartenPostgresHealthy = false;
            bool isDebeziumHealthy = false;

            var newDbNameFromConfiguration = _configuration["ConnectorDetails:DbNameNew"];
            if (string.IsNullOrEmpty(newDbNameFromConfiguration))
            {
                throw new InvalidOperationException($"Value of ConnectorDetails:DbNameNew is null or empty in the appsettings");
            }

            var startTime = DateTime.UtcNow;
            while (DateTime.UtcNow - startTime < timeout)
            {
                bool areAllHealthy = false;

                foreach (var containerName in containerNames)
                {
                    isKafkaHealthy = await IsKafkaHealthyAsync("localhost:9092");
                    isSchemaRegistryHealthy = await IsSchemaRegistryHealthyAsync("http://localhost:8081/");
                    isKafkaConnectHealthy = await IsKafkaConnectHealthyAsync("http://localhost:8083/");
                    isControlCenterHealthy = await IsControlCenterHealthyAsync("http://localhost:9021/");
                    isMySql80Healthy = await IsMySqlHealthyAsync($"Server=localhost;Port=3308;User Id=root;Password={oldDatabaseRootPassword};");
                    isDestinationPostgresHealthy = await IsPostgresHealthyAsync($"Host=localhost;Port=5440;Username=postgres;Password={newDatabaseRootPasswordEnvironmentVariable};Database={newDbNameFromConfiguration};");
                    isMartenPostgresHealthy = await IsPostgresHealthyAsync($"Host=localhost;Port=5441;Username={postgresMartenRootUserName};Password={postgresMartenRootPassword};Database={postgresMartenDatabaseName};");
                    isDebeziumHealthy = await IsDebeziumHealthyAsync("http://localhost:8084/");

                    areAllHealthy = containerName switch
                    {
                        "kafka" => isKafkaHealthy,
                        "schema-registry" => isSchemaRegistryHealthy,
                        "connect" => isKafkaConnectHealthy,
                        "control-center" => isControlCenterHealthy,
                        "mysql80" => isMySql80Healthy,
                        "destinationpostgres" => isDestinationPostgresHealthy,
                        "martenpostgres" => isMartenPostgresHealthy,
                        "debezium" => isDebeziumHealthy,
                        _ => false
                    };
                }

                if (areAllHealthy)
                {
                    return new ServiceActionStatus
                    {
                        Success = true,
                        Message = "All selected containers are healthy."
                    };
                }

                await Task.Delay(1000); // Wait for 1 second before retrying
            }

            var containerHealthStatuses = new Dictionary<string, bool>
            {
                { "kafka", isKafkaHealthy },
                { "schema-registry", isSchemaRegistryHealthy },
                { "connect", isKafkaConnectHealthy },
                { "control-center", isControlCenterHealthy },
                { "mysql80", isMySql80Healthy },
                { "destinationpostgres", isDestinationPostgresHealthy },
                { "martenpostgres", isMartenPostgresHealthy },
                { "debezium", isDebeziumHealthy }
            };

            var unhealthyContainers = containerHealthStatuses
                .Where(kvp => !kvp.Value)
                .Select(kvp => kvp.Key)
                .ToList();

            return new ServiceActionStatus
            {
                Success = false,
                Message = $"Not all selected containers became healthy in the allotted time. The following containers are unhealthy: {string.Join(", ", unhealthyContainers)}"
            };
        }

        public async Task<bool> IsKafkaHealthyAsync(string bootstrapServers)
        {
            var config = new AdminClientConfig { BootstrapServers = bootstrapServers };

            try
            {
                using (var adminClient = new AdminClientBuilder(config).Build())
                {
                    var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));
                    return metadata.Brokers.Count > 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> IsSchemaRegistryHealthyAsync(string url)
        {
            return await IsServiceHealthyAsync(url);
        }

        public async Task<bool> IsKafkaConnectHealthyAsync(string url)
        {
            return await IsServiceHealthyAsync(url);
        }

        public async Task<bool> IsControlCenterHealthyAsync(string url)
        {
            return await IsServiceHealthyAsync(url);
        }

        public async Task<bool> IsMySqlHealthyAsync(string connectionString)
        {
            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> IsPostgresHealthyAsync(string connectionString)
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> IsDebeziumHealthyAsync(string url)
        {
            return await IsServiceHealthyAsync(url);
        }

        private void ThrowIfNullOrEmpty(string value, string key)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentNullException($"Environment variable {key} is null or empty");
            }
        }

        private async Task<bool> IsServiceHealthyAsync(string url)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}