using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KUK.Common.Services
{
    public class KafkaService : IKafkaService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<KafkaService> _logger;
        private readonly IUtilitiesService _utilitiesService;

        public KafkaService(
            IConfiguration configuration,
            ILogger<KafkaService> logger,
            IUtilitiesService utilitiesService)
        {
            _configuration = configuration;
            _logger = logger;
            _utilitiesService = utilitiesService;
        }

        /// <summary>
        /// We wait specified time for topic to become available and then check its replication factor.
        /// </summary>
        public async Task<short?> GetReplicationFactorAsync(string topicName)
        {
            if (string.IsNullOrWhiteSpace(_configuration["Kafka:BootstrapServers"]))
            {
                throw new InvalidOperationException($"Cannot check replication factor for {topicName} because Kafka:BootstrapServers is empty");
            }

            var adminConfig = new AdminClientConfig
            {
                BootstrapServers = _configuration["Kafka:BootstrapServers"]
            };

            var mode = _utilitiesService.GetOnlineOrDockerMode();
            if (mode == ApplicationDestinationMode.Online)
            {
                await UpdateAdminConfig(adminConfig);
            }

            using var adminClient = new AdminClientBuilder(adminConfig).Build();
            var topicExists = false;
            var startTime = DateTime.UtcNow;

            while (true) // We don't wait specified time, we wait indefinitely for Debezium to be started
            {
                try
                {
                    var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));
                    var topicNames = metadata.Topics.Select(t => t.Topic).ToList();

                    if (topicNames.Contains(topicName))
                    {
                        topicExists = true;
                        break;
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError($"An error occurred while checking topic {topicName}: {e.Message}");
                }

                if ((DateTime.UtcNow - startTime).TotalSeconds % 60 == 0)
                {
                    _logger.LogInformation($"Waiting for topic {topicName} to become available...");
                }

                await Task.Delay(TimeSpan.FromSeconds(10));
            }

            if (!topicExists)
            {
                throw new InvalidOperationException($"Topic {topicName} not found.");
            }

            try
            {
                var metadata = adminClient.GetMetadata(topicName, TimeSpan.FromSeconds(10));
                var topicMetadata = metadata.Topics.FirstOrDefault(t => t.Topic == topicName);

                if (topicMetadata != null)
                {
                    var replicationFactor = topicMetadata.Partitions.First().Replicas.Length;
                    _logger.LogInformation($"Replication factor for topic {topicName} is {replicationFactor}.");
                    return (short)replicationFactor;
                }
                else
                {
                    _logger.LogWarning($"Topic {topicName} not found.");
                    return null;
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"An error occurred while fetching replication factor for topic {topicName}: {e.Message}");
                return null;
            }
        }

        public async Task CreateTopicAsync(string topicName, int numPartitions, short replicationFactor)
        {
            if (string.IsNullOrWhiteSpace(_configuration["Kafka:BootstrapServers"]))
            {
                throw new InvalidOperationException($"Cannot create new topic {topicName} because Kafka:BootstrapServers is empty");
            }

            string environmentDestination = _utilitiesService.GetEnvironmentDestinationString();
            string environmentEnvVariable = $"DebeziumWorker_{environmentDestination}_Environment";
            var environment = Environment.GetEnvironmentVariable(environmentEnvVariable);
            if (environment == "Development")
            {
                _logger.LogWarning($"Changing replicationFactor in {nameof(CreateTopicAsync)} from {replicationFactor} to one " +
                    $"because we are running with {environmentEnvVariable} set to Development.");
                replicationFactor = 1;

            }

            var adminConfig = new AdminClientConfig
            {
                BootstrapServers = _configuration["Kafka:BootstrapServers"]
            };

            var mode = _utilitiesService.GetOnlineOrDockerMode();
            if (mode == ApplicationDestinationMode.Online)
            {
                await UpdateAdminConfig(adminConfig);
            }

            using var adminClient = new AdminClientBuilder(adminConfig).Build();
            try
            {
                // Check if topic exists
                var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));
                var topicExists = metadata.Topics.Any(t => t.Topic == topicName);

                if (topicExists)
                {
                    _logger.LogInformation($"Topic {topicName} already exists.");
                }
                else
                {
                    // Create topic if it does not exist
                    await adminClient.CreateTopicsAsync(new TopicSpecification[]
                    {
                        new TopicSpecification { Name = topicName, NumPartitions = numPartitions, ReplicationFactor = replicationFactor }
                    });
                    _logger.LogInformation($"Topic {topicName} created successfully.");
                }
            }
            catch (CreateTopicsException e)
            {
                foreach (var result in e.Results)
                {
                    _logger.LogWarning($"An error occurred creating topic {result.Topic}: {result.Error.Reason}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"An unexpected error occurred: {ex.Message}");
            }
        }

        public async Task DeleteKafkaTopic(string topicName)
        {
            if (string.IsNullOrWhiteSpace(_configuration["Kafka:BootstrapServers"]))
            {
                throw new InvalidOperationException($"Cannot delete topic {topicName} because Kafka:BootstrapServers is empty");
            }

            var adminConfig = new AdminClientConfig
            {
                BootstrapServers = _configuration["Kafka:BootstrapServers"]
            };

            var mode = _utilitiesService.GetOnlineOrDockerMode();
            if (mode == ApplicationDestinationMode.Online)
            {
                await UpdateAdminConfig(adminConfig);
            }

            using var adminClient = new AdminClientBuilder(adminConfig).Build();
            try
            {
                // We check if topic exists
                var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));
                var topicExists = metadata.Topics.Any(t => t.Topic == topicName);

                if (!topicExists)
                {
                    _logger.LogInformation($"Topic {topicName} does not exist.");
                }
                else
                {
                    // We remove topoic if it exists
                    await adminClient.DeleteTopicsAsync(new List<string> { topicName });
                    _logger.LogInformation($"Topic {topicName} deleted successfully.");
                }
            }
            catch (DeleteTopicsException e)
            {
                foreach (var result in e.Results)
                {
                    _logger.LogWarning($"An error occurred deleting topic {result.Topic}: {result.Error.Reason}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"An unexpected error occurred: {ex.Message}");
            }
        }

        private async Task UpdateAdminConfig(AdminClientConfig adminConfig)
        {
            await ThrowIfConfigurationKeyNotPresent("OnlineMode:SslCaLocation");
            await ThrowIfConfigurationKeyNotPresent("OnlineMode:SslCertificateLocation");
            await ThrowIfConfigurationKeyNotPresent("OnlineMode:SslKeyLocation");

            adminConfig.SecurityProtocol = SecurityProtocol.Ssl;
            adminConfig.SslCaLocation = _configuration["OnlineMode:SslCaLocation"];
            adminConfig.SslCertificateLocation = _configuration["OnlineMode:SslCertificateLocation"];
            adminConfig.SslKeyLocation = _configuration["OnlineMode:SslKeyLocation"];
        }

        private async Task ThrowIfConfigurationKeyNotPresent(string topicKey)
        {
            var topic = _configuration[topicKey];
            if (string.IsNullOrWhiteSpace(topic))
            {
                throw new InvalidOperationException($"Value of {topicKey} is not set in appsettings.json");
            }
        }
    }
}
