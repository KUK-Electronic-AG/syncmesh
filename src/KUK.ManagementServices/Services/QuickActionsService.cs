using KUK.Common.Services;
using KUK.Common.Utilities;
using KUK.ManagementServices.Services.Interfaces;
using KUK.ManagementServices.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KUK.ManagementServices.Services
{
    public class QuickActionsService : IQuickActionsService
    {
        private readonly ILogger<QuickActionsService> _logger;
        private readonly IConnectorsRegistrationService _connectorsRegistrationService;
        private readonly IDockerService _dockerService;
        private readonly ISchemaInitializerService _schemaInitializerService;
        private readonly IContainersHealthCheckService _containersHealthCheckService;
        private readonly IUtilitiesService _utilitiesService;
        private readonly IExternalConnectionService _externalConnectionService;
        private readonly IConfiguration _configuration;
        private readonly IKafkaService _kafkaService;

        public QuickActionsService(
            ILogger<QuickActionsService> logger,
            IConnectorsRegistrationService connectorsRegistrationService,
            IDockerService dockerService,
            ISchemaInitializerService schemaInitializerService,
            IContainersHealthCheckService containersHealthCheckService,
            IUtilitiesService utilitiesService,
            IExternalConnectionService externalConnectionService,
            IConfiguration configuration,
            IKafkaService kafkaService)
        {
            _logger = logger;
            _connectorsRegistrationService = connectorsRegistrationService;
            _dockerService = dockerService;
            _schemaInitializerService = schemaInitializerService;
            _containersHealthCheckService = containersHealthCheckService;
            _utilitiesService = utilitiesService;
            _externalConnectionService = externalConnectionService;
            _configuration = configuration;
            _kafkaService = kafkaService;
        }

        public async Task<ServiceActionStatus> QuickDelete()
        {
            var mode = _utilitiesService.GetOnlineOrDockerMode();

            if (mode == ApplicationDestinationMode.Docker)
            {
                var actionGroups = new List<(string logMessage, List<Func<Task<ServiceActionStatus>>> actions)>
                {
                    ("Removing Kafka-related containers...", new List<Func<Task<ServiceActionStatus>>>
                    {
                        () => RemoveContainerAsync("control-center"),
                        () => RemoveContainerAsync("connect"),
                        () => RemoveContainerAsync("schema-registry"),
                        () => RemoveContainerAsync("kafka")
                    }),
                    ("Removing other containers...", new List<Func<Task<ServiceActionStatus>>>
                    {
                        () => RemoveContainerAsync("mysql80"),
                        () => RemoveContainerAsync("destinationpostgres"),
                        () => RemoveContainerAsync("martenpostgres"),
                        () => RemoveContainerAsync("debezium")
                    })
                };

                foreach (var (logMessage, actions) in actionGroups)
                {
                    _logger.LogInformation(logMessage);
                    foreach (var action in actions)
                    {
                        var status = await action();
                        if (!status.Success)
                        {
                            return status;
                        }
                    }
                }
            }
            else if (mode == ApplicationDestinationMode.Online)
            {
                try
                {
                    // Remove both connectors
                    await _externalConnectionService.DeleteConnectorsAsync("old-to-new-connector");
                    await _externalConnectionService.DeleteConnectorsAsync("new-to-old-connector");
                    // Remove content of both databases
                    ServiceActionStatus resultDeleteOldDatabase = _schemaInitializerService.DeleteSchema(WhichDatabaseEnum.OldDatabase);
                    if (!resultDeleteOldDatabase.Success)
                    {
                        throw new InvalidOperationException($"Could not delete old database. Error: {resultDeleteOldDatabase.Message}");
                    }
                    ServiceActionStatus resultDeleteNewDatabase = _schemaInitializerService.DeleteSchema(WhichDatabaseEnum.NewDatabase);
                    if (!resultDeleteNewDatabase.Success)
                    {
                        throw new InvalidOperationException($"Could not delete new database. Error: {resultDeleteOldDatabase.Message}");
                    }
                    // Remove content from topics
                    await DeleteTopicAsync("Kafka:OldToNewTopic");
                    await DeleteTopicAsync("Kafka:NewToOldTopic");
                    await DeleteTopicAsync("Kafka:EventQueueTopic");
                    await DeleteTopicAsync("Kafka:BufferTopic");
                    await DeleteTopicAsync("Kafka:SchemaChangesOldTopic");
                    await DeleteTopicAsync("Kafka:SchemaChangesNewTopic");
                    await DeleteTopicAsync("Kafka:DbHistoryOldTopic");
                    await DeleteTopicAsync("Kafka:DbHistoryNewTopic");
                }
                catch (Exception ex)
                {
                    return new ServiceActionStatus { Success = false, Message = $"Cannot perform quick delete for Online mode. Exception: {ex}" };
                }
            }

            return new ServiceActionStatus { Success = true, Message = "Successfully performed QuickDelete" };
        }

        public async Task<ServiceActionStatus> QuickStartup()
        {
            var mode = _utilitiesService.GetOnlineOrDockerMode();

            if (mode == ApplicationDestinationMode.Docker)
            {
                var containerNames = new List<string> { "kafka", "schema-registry", "control-center", "mysql80", "destinationpostgres", "martenpostgres", "debezium" };

                var actionGroups = new List<(string logMessage, List<Func<Task<ServiceActionStatus>>> actions)>
                {
                    ("Starting Kafka-related containers...", new List<Func<Task<ServiceActionStatus>>>
                    {
                        () => StartContainerAsync("kafka"),
                        () => StartContainerAsync("schema-registry"),
                        () => StartContainerAsync("control-center")
                    }),
                    ("Starting other containers (with debezium)...", new List<Func<Task<ServiceActionStatus>>>
                    {
                        () => StartContainerAsync("mysql80"),
                        () => StartContainerAsync("destinationpostgres"),
                        () => StartContainerAsync("martenpostgres"),
                        () => StartContainerAsync("debezium")
                    }),
                    ("Waiting for Debezium to be up...", new List<Func<Task<ServiceActionStatus>>>
                    {
                        () => _containersHealthCheckService.WaitForContainersAsync(containerNames, TimeSpan.FromMinutes(1)),
                        () => _containersHealthCheckService.WaitForServicesInContainersAsync(containerNames, TimeSpan.FromMinutes(1))
                    }),
                    ("Initializing schema, tables, and data for old database...", new List<Func<Task<ServiceActionStatus>>>
                    {
                        () => _schemaInitializerService.InitializeSchemaTablesAndData(WhichDatabaseEnum.OldDatabase, "root"),
                    })
                };

                foreach (var (logMessage, actions) in actionGroups)
                {
                    _logger.LogInformation(logMessage);
                    foreach (var action in actions)
                    {
                        var status = await action();
                        if (!status.Success)
                        {
                            return status;
                        }
                    }
                }
            }
            else if (mode == ApplicationDestinationMode.Online)
            {
                await _schemaInitializerService.InitializeSchemaTablesAndData(WhichDatabaseEnum.OldDatabase, string.Empty);
            }

            return new ServiceActionStatus { Success = true, Message = $"Successfully performed {nameof(QuickStartup)}" };
        }

        // REMARK: We may consider using for quick create methods those methods that are in _databaseOperationsService
        // in order to avoid code duplication. However, then we may have issues with disposed context instance.

        

        private async Task DeleteTopicAsync(string topicKey)
        {
            var topic = _configuration[topicKey];
            if (string.IsNullOrWhiteSpace(topic))
            {
                throw new InvalidOperationException($"Value of {topicKey} is not set in appsettings.json");
            }
            await _kafkaService.DeleteKafkaTopic(topic);
        }

        private async Task<ServiceActionStatus> StartContainerAsync(string containerName)
        {
            try
            {
                await _dockerService.StartContainerAsync(containerName);
                return new ServiceActionStatus { Success = true, Message = $"{containerName} started successfully" };
            }
            catch (Exception ex)
            {
                return new ServiceActionStatus { Success = false, Message = $"Failed to start {containerName}: {ex.Message}" };
            }
        }

        private async Task<ServiceActionStatus> RegisterConnectorAsync(string connectorName)
        {
            try
            {
                ConnectorRegistrationResult connectorRegistrationResult = await _connectorsRegistrationService.RegisterConnectorAsync(connectorName);
                if (connectorRegistrationResult.Success)
                {
                    return new ServiceActionStatus { Success = true, Message = $"{connectorName} registered successfully" };
                }
                else
                {
                    return new ServiceActionStatus { Success = false, Message = $"Failed to register {connectorName}: {connectorRegistrationResult.Message}" };
                }
            }
            catch (Exception ex)
            {
                return new ServiceActionStatus { Success = false, Message = $"Failed to register {connectorName}: {ex.Message}" };
            }
        }

        private async Task<ServiceActionStatus> RemoveContainerAsync(string containerName)
        {
            try
            {
                await _dockerService.RemoveContainerAsync(containerName);
                return new ServiceActionStatus { Success = true, Message = $"{containerName} removed successfully" };
            }
            catch (Exception ex)
            {
                return new ServiceActionStatus { Success = false, Message = $"Failed to remove {containerName}: {ex.Message}" };
            }
        }
    }
}
