using KUK.Common;
using KUK.Common.Contexts;
using KUK.Common.Services;
using KUK.Common.Utilities;
using KUK.ManagementServices.Services.Interfaces;
using KUK.ManagementServices.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KUK.ManagementServices.Services
{
    public class QuickActionsService : IQuickActionsService
    {
        private readonly Chinook1DataChangesContext _oldSchemaContext;
        private readonly Chinook2Context _newSchemaContext;

        private readonly ILogger<QuickActionsService> _logger;
        private readonly IConnectorsRegistrationService _connectorsRegistrationService;
        private readonly IDatabaseManagementService _databaseManagementService;
        private readonly IDatabaseOperationsService _databaseOperationsService;
        private readonly IDockerService _dockerService;
        private readonly IProcessorService _processorService;
        private readonly ISchemaInitializerService _schemaInitializerService;
        private readonly AppSettingsConfig _appSettingsConfig;
        private readonly IContainersHealthCheckService _containersHealthCheckService;
        private readonly IUtilitiesService _utilitiesService;
        private readonly IExternalConnectionService _externalConnectionService;
        private readonly IConfiguration _configuration;
        private readonly IKafkaService _kafkaService;

        public QuickActionsService(
            Chinook1DataChangesContext oldSchemaContext,
            Chinook2Context newSchemaContext,
            ILogger<QuickActionsService> logger,
            IConnectorsRegistrationService connectorsRegistrationService,
            IDatabaseManagementService databaseManagementService,
            IDatabaseOperationsService databaseOperationsService,
            IDockerService dockerService,
            IProcessorService processorService,
            ISchemaInitializerService schemaInitializerService,
            AppSettingsConfig appSettingsConfig,
            IContainersHealthCheckService containersHealthCheckService,
            IUtilitiesService utilitiesService,
            IExternalConnectionService externalConnectionService,
            IConfiguration configuration,
            IKafkaService kafkaService)
        {
            _oldSchemaContext = oldSchemaContext;
            _newSchemaContext = newSchemaContext;
            _logger = logger;
            _connectorsRegistrationService = connectorsRegistrationService;
            _databaseManagementService = databaseManagementService;
            _databaseOperationsService = databaseOperationsService;
            _dockerService = dockerService;
            _processorService = processorService;
            _schemaInitializerService = schemaInitializerService;
            _appSettingsConfig = appSettingsConfig;
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
                await _schemaInitializerService.InitializeSchemaTablesAndData(WhichDatabaseEnum.OldDatabase);
            }

            return new ServiceActionStatus { Success = true, Message = $"Successfully performed {nameof(QuickStartup)}" };
        }

        // REMARK: We may consider using for quick create methods those methods that are in _databaseOperationsService
        // in order to avoid code duplication. However, then we may have issues with disposed context instance.

        public async Task<ServiceActionStatus> QuickCreateOldCustomer()
        {
            var newCustomer = new Common.ModelsOldSchema.Customer
            {
                FirstName = "John (From old database - quick create)",
                LastName = "Doe",
                Address = "123 Main St",
                City = "Anytown",
                State = "Anystate",
                Country = "Anycountry",
                PostalCode = "12345",
                Phone = "123-456-7890",
                Email = "john.doe@example.com"
            };

            return await CreateEntityAsync(_oldSchemaContext, newCustomer, "Customer created successfully.");
        }

        public async Task<ServiceActionStatus> QuickCreateOldInvoice()
        {
            var newInvoice = new Common.ModelsOldSchema.Invoice
            {
                CustomerId = 1,
                InvoiceDate = DateTime.UtcNow,
                BillingAddress = "123 Main St",
                BillingCity = "Anytown",
                BillingState = "Anystate",
                BillingCountry = "Anycountry",
                BillingPostalCode = "12345",
                Total = 100.00m,
                TestValue3 = 123.456m,
            };

            return await CreateEntityAsync(_oldSchemaContext, newInvoice, "Old Invoice created successfully.");
        }

        public async Task<ServiceActionStatus> QuickCreateNewCustomer()
        {
            Common.ModelsNewSchema.Address address = _newSchemaContext.Addresses.FirstOrDefault();
            if (address == null)
            {
                throw new InvalidOperationException("There are no addresses so we cannot quickly create new customer");
            }

            var newCustomer = new Common.ModelsNewSchema.Customer
            {
                FirstName = "Jane (from new database - quick create)",
                LastName = "Doe",
                AddressId = address.AddressId,
                Phone = "987-654-3210",
                Email = "jane.doe@example.com"
            };

            return await CreateEntityAsync(_newSchemaContext, newCustomer, "New Customer created successfully.");
        }

        public async Task<ServiceActionStatus> QuickCreateNewInvoice()
        {
            Common.ModelsNewSchema.Address address = _newSchemaContext.Addresses.FirstOrDefault();
            if (address == null)
            {
                throw new InvalidOperationException("There are no addresses so we cannot quickly create new invoice");
            }

            Common.ModelsNewSchema.Customer customer = _newSchemaContext.Customers.FirstOrDefault();
            if (customer == null)
            {
                throw new InvalidOperationException("There are no customers so we cannot quickly create new invoice");
            }

            var newInvoice = new Common.ModelsNewSchema.Invoice
            {
                CustomerId = customer.CustomerId,
                InvoiceDate = DateTime.UtcNow,
                BillingAddressId = address.AddressId,
                Total = 200.00m
            };

            return await CreateEntityAsync(_newSchemaContext, newInvoice, "New Invoice created successfully.");
        }

        public async Task<ServiceActionStatus> QuickCreateNewAddress()
        {
            var newAddress = new Common.ModelsNewSchema.Address
            {
                Street = "456 Main St",
                City = "Newtown",
                State = "Newstate",
                Country = "Newcountry",
                PostalCode = "67890"
            };

            return await CreateEntityAsync(_newSchemaContext, newAddress, "New Address created successfully.");
        }

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

        private async Task<ServiceActionStatus> CreateEntityAsync<T>(DbContext context, T entity, string successMessage) where T : class
        {
            try
            {
                context.Set<T>().Add(entity);
                await context.SaveChangesAsync();
                return new ServiceActionStatus { Success = true, Message = successMessage };
            }
            catch (DbUpdateException dbEx)
            {
                return new ServiceActionStatus { Success = false, Message = $"Database update error: {dbEx.Message}" };
            }
            catch (Exception ex)
            {
                return new ServiceActionStatus { Success = false, Message = $"Unhandled exception: {ex.Message}" };
            }
        }
    }
}
