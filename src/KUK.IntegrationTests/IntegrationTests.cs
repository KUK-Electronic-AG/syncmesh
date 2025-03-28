using KUK.Common.Services;
using KUK.Common.TestUtilities;
using KUK.ManagementServices.Services.Interfaces;
using KUK.ManagementServices.Utilities;
using Microsoft.Extensions.Logging;

namespace KUK.IntegrationTests
{
    [Collection("SequentialTests")]
    public class IntegrationTests : IClassFixture<TestFixture>
    {
        private readonly IDockerService _dockerService;
        private readonly IDatabaseManagementService _databaseManagementService;
        private readonly ISchemaInitializerService _schemaInitializerService;
        private readonly IConnectorsRegistrationService _connectorsRegistrationService;
        private readonly IProcessorService _processorService;
        private readonly IDatabaseOperationsService _databaseOperationsService;
        private readonly IQuickActionsService _quickActionsService;
        private readonly ILogger<IntegrationTests> _logger;

        private readonly TestFixture _fixture;

        private const int DB_WAITING_MAX_RETIRES = 300;
        private const int DB_WAITING_DELAY = 1000; // 1 second

        public IntegrationTests(
            TestFixture fixture)
        {
            _fixture = fixture;
            _dockerService = fixture.DockerService;
            _databaseManagementService = fixture.DatabaseManagementService;
            _schemaInitializerService = fixture.SchemaInitializerService;
            _connectorsRegistrationService = fixture.ConnectorsRegistrationService;
            _processorService = fixture.ProcessorService;
            _databaseOperationsService = fixture.DatabaseOperationsService;
            _quickActionsService = fixture.QuickActionsService;
            _logger = fixture.Logger; // REMARK: Logging saves to C:\IntegrationTestsLogs
        }

        [Fact]
        public async Task TestDatabaseSynchronization()
        {
            // REMARK: For Online mode, you can check if VPN is on or log additional message
            // REMARK: You can make sure here that certificates are present for Online mode

            // Arrange
            _logger.LogInformation("Performing quick delete...");
            var quickDeleteResult = await _quickActionsService.QuickDelete();
            if (!quickDeleteResult.Success) throw new InvalidOperationException($"Failure doing quick delete. Error: {quickDeleteResult.Message}");
            _logger.LogInformation("Performing quick startup...");
            ServiceActionStatus quickStartupResult = await _quickActionsService.QuickStartup();
            if (!quickStartupResult.Success) throw new InvalidOperationException($"Failure doing quick startup. Error: {quickStartupResult.Message}");

            _logger.LogInformation("Starting Debezium processor...");
            await _processorService.DeleteProcessorLogs();
            await _processorService.StartProcessorAsync(); // starts C# .NET application
            await _processorService.WaitForProcessorToFinishInitializatonAsync();

            // Act
            _logger.LogInformation("Creating customer in old database...");
            OldCustomerOperationResult createdCustomerInOldDatabase = await _databaseOperationsService.CreateCustomerInOldDatabaseAsync();
            _logger.LogInformation("Creating invoice in old database...");
            OldInvoiceOperationResult createdInvoiceInOldDatabase = await _databaseOperationsService.CreateInvoiceInOldDatabaseAsync();
            _logger.LogInformation("Creating customer in new database...");
            NewCustomerOperationResult createdCustomerInNewDatabase = await _databaseOperationsService.CreateCustomerInNewDatabaseAsync();
            _logger.LogInformation("Creating invoice in new database...");
            NewInvoiceOperationResult createdInvoiceInNewDatabase = await _databaseOperationsService.CreateInvoiceInNewDatabaseAsync();

            _logger.LogInformation("Adding new customer using subquery to old database...");
            ServiceActionStatus createdCustomerWithSubqueryInOldDatabase = await _databaseOperationsService.AddNewCustomerUsingSubquery();
            _logger.LogInformation("Adding new invoice using nested query to old database...");
            ServiceActionStatus createdCustomerWithNestedQueryInOldDatabase = await _databaseOperationsService.AddNewInvoiceUsingNestedQuery();

            // Assert
            // REMARK: We check in new database for whatever was added to old database (and vice versa) because that's job of C# processor to migrate.
            await WaitForAndAssertConditionsAsync(
                false,
                new ConditionCheck
                {
                    Condition = () => _databaseOperationsService.CustomerExistsInNewDatabaseAsync(createdCustomerInOldDatabase),
                    Description = "customer in new database"
                },
                new ConditionCheck
                {
                    Condition = () => _databaseOperationsService.InvoiceExistsInNewDatabaseAsync(createdInvoiceInOldDatabase),
                    Description = "invoice in new database"
                },
                new ConditionCheck
                {
                    Condition = () => _databaseOperationsService.CustomerExistsInOldDatabaseAsync(createdCustomerInNewDatabase),
                    Description = "customer in old database"
                },
                new ConditionCheck
                {
                    Condition = () => _databaseOperationsService.InvoiceExistsInOldDatabaseAsync(createdInvoiceInNewDatabase),
                    Description = "invoice in old database"
                },
                new ConditionCheck
                {
                    Condition = () => _databaseOperationsService.CustomerAddedWithSubqueryExistsInNewDatabase(),
                    Description = "customer added with subquery in new database"
                },
                new ConditionCheck
                {
                    Condition = () => _databaseOperationsService.InvoiceAddedWithNestedQueryExistsInNewDatabase(),
                    Description = "address added by adding new invoice with nested query in new database"
                }
            );

            // Act
            _logger.LogInformation("Updating customer city in old database...");
            await _databaseOperationsService.UpdateCustomerCityInOldDatabaseAsync(createdCustomerInOldDatabase);

            // Assert
            await WaitForAndAssertConditionsAsync(
                false,
                new ConditionCheck
                {
                    Condition = () => _databaseOperationsService.CustomerCityUpdatedInOldDatabaseAsync(createdCustomerInOldDatabase),
                    Description = $"customer city updated in old database {createdCustomerInOldDatabase.GuidValueInFirstName}"
                },
                new ConditionCheck
                {
                    Condition = () => _databaseOperationsService.CustomerCityUpdatedInNewDatabaseAsync(createdCustomerInOldDatabase),
                    Description = $"customer city updated in new database {createdCustomerInOldDatabase.GuidValueInFirstName}"
                }
            );

            // Act
            _logger.LogInformation("Updating customer email and address in new database...");
            await _databaseOperationsService.UpdateCustomerEmailAndAddressInNewDatabaseAsync(createdCustomerInOldDatabase);

            // Assert
            await WaitForAndAssertConditionsAsync(
                false,
                new ConditionCheck
                {
                    Condition = () => _databaseOperationsService.CustomerCityEmailAndCountryUpdatedInOldDatabaseAsync(createdCustomerInOldDatabase),
                    Description = "customer city, email and address updated in old database"
                },
                new ConditionCheck
                {
                    Condition = () => _databaseOperationsService.CustomerCityEmailAndCountryUpdatedInNewDatabaseAsync(createdCustomerInOldDatabase),
                    Description = "customer city, email and address updated in new database"
                }
            );

            // Act
            _logger.LogInformation("Deleting customer in old database...");
            await _databaseOperationsService.DeleteCustomerInOldDatabaseAsync(createdCustomerInOldDatabase);

            // Assert
            await WaitForAndAssertConditionsAsync(
                false,
                new ConditionCheck
                {
                    Condition = () => _databaseOperationsService.FirstCustomerDeletedInOldDatabaseAsync(createdCustomerInOldDatabase),
                    Description = "first customer deleted in old database"
                },
                new ConditionCheck
                {
                    Condition = () => _databaseOperationsService.FirstCustomerDeletedInNewDatabaseAsync(createdCustomerInOldDatabase),
                    Description = "first customer deleted in new database"
                }
            );

            // Act
            _logger.LogInformation("Deleting customer in new database...");
            await _databaseOperationsService.DeleteCustomerInNewDatabaseAsync(createdCustomerInNewDatabase);

            // Assert
            await WaitForAndAssertConditionsAsync(
                false,
                new ConditionCheck
                {
                    Condition = () => _databaseOperationsService.SecondCustomerDeletedInOldDatabaseAsync(createdCustomerInNewDatabase),
                    Description = "second customer deleted in old database"
                },
                new ConditionCheck
                {
                    Condition = () => _databaseOperationsService.SecondCustomerDeletedInNewDatabaseAsync(createdCustomerInNewDatabase),
                    Description = "second customer deleted in new database"
                }
            );

            // Act
            _logger.LogInformation("Updating invoice lines in old and new database...");
            await _databaseOperationsService.UpdateInvoiceLineInOldDatabaseAsync(createdInvoiceInOldDatabase);
            await _databaseOperationsService.UpdateInvoiceLineInNewDatabaseAsync(createdInvoiceInNewDatabase);

            // Assert
            await WaitForAndAssertConditionsAsync(
                true,
                new ConditionCheck
                {
                    Condition = () => _databaseOperationsService.FirstInvoiceLineUpdatedInOldDatabaseAsync(createdInvoiceInOldDatabase),
                    Description = "first invoice line updated in old database"
                },
                new ConditionCheck
                {
                    Condition = () => _databaseOperationsService.FirstInvoiceLineUpdatedInNewDatabaseAsync(createdInvoiceInOldDatabase),
                    Description = "first invoice line updated in new database"
                },
                new ConditionCheck
                {
                    Condition = () => _databaseOperationsService.SecondInvoiceLineUpdatedInOldDatabaseAsync(createdInvoiceInNewDatabase),
                    Description = "second invoice line updated in old database"
                },
                new ConditionCheck
                {
                    Condition = () => _databaseOperationsService.SecondInvoiceLineUpdatedInNewDatabaseAsync(createdInvoiceInNewDatabase),
                    Description = "second invoice line updated in new database"
                }
            );
        }

        [Fact]
        public async Task LoadTest()
        {
            _logger.LogInformation($"Started load test");

            // Arrange
            await _quickActionsService.QuickDelete();
            await _quickActionsService.QuickStartup();
            await _processorService.DeleteProcessorLogs();
            await _processorService.StartProcessorAsync();
            await _processorService.WaitForProcessorToFinishInitializatonAsync();

            _logger.LogInformation($"Processor is ready");

            var createdCustomersInOldDatabase = new List<OldCustomerOperationResult>();
            var createdInvoicesInOldDatabase = new List<OldInvoiceOperationResult>();
            var createdCustomersInNewDatabase = new List<NewCustomerOperationResult>();
            var createdInvoicesInNewDatabase = new List<NewInvoiceOperationResult>();

            var startTime = DateTime.UtcNow;
            var endTime = startTime.AddSeconds(5);

            _logger.LogInformation($"Inserting data to the databases");

            // Act
            while (DateTime.UtcNow < endTime)
            {
                createdCustomersInOldDatabase.Add(await _databaseOperationsService.CreateCustomerInOldDatabaseAsync());
                createdInvoicesInOldDatabase.Add(await _databaseOperationsService.CreateInvoiceInOldDatabaseAsync());
                createdCustomersInNewDatabase.Add(await _databaseOperationsService.CreateCustomerInNewDatabaseAsync());
                createdInvoicesInNewDatabase.Add(await _databaseOperationsService.CreateInvoiceInNewDatabaseAsync());
            }

            _logger.LogInformation($"Data inserted to the databases");

            LogCreatedData(createdCustomersInOldDatabase, createdInvoicesInOldDatabase, createdCustomersInNewDatabase, createdInvoicesInNewDatabase);

            // Assert
            var conditions = new List<ConditionCheck>();

            foreach (var customer in createdCustomersInOldDatabase)
            {
                conditions.Add(new ConditionCheck
                {
                    Condition = () => _databaseOperationsService.CustomerExistsInNewDatabaseAsync(customer),
                    Description = "customer in new database"
                });
            }

            foreach (var invoice in createdInvoicesInOldDatabase)
            {
                conditions.Add(new ConditionCheck
                {
                    Condition = () => _databaseOperationsService.InvoiceExistsInNewDatabaseAsync(invoice),
                    Description = "invoice in new database"
                });
            }

            foreach (var customer in createdCustomersInNewDatabase)
            {
                conditions.Add(new ConditionCheck
                {
                    Condition = () => _databaseOperationsService.CustomerExistsInOldDatabaseAsync(customer),
                    Description = "customer in old database"
                });
            }

            foreach (var invoice in createdInvoicesInNewDatabase)
            {
                conditions.Add(new ConditionCheck
                {
                    Condition = () => _databaseOperationsService.InvoiceExistsInOldDatabaseAsync(invoice),
                    Description = "invoice in old database"
                });
            }

            var totalNumberOfInserts = conditions.Count(); // 1348 for 20 seconds on local machine, 132 for 20 seconds in Online mode
            _logger.LogInformation($"Total number of inserts: {totalNumberOfInserts}");

            await WaitForAndAssertConditionsAsync(true, conditions.ToArray());
        }

        [Theory]
        [InlineData("kafka")]
        [InlineData("schema-registry")]
        [InlineData("control-center")]
        [InlineData("mysql57")]
        [InlineData("destinationpostgres")]
        [InlineData("martenpostgres")]
        [InlineData("debezium")]
        public async Task StoppingContainerTest(string containerName)
        {
            // Arrange
            await _quickActionsService.QuickDelete();
            await _quickActionsService.QuickStartup();
            await _processorService.DeleteProcessorLogs();
            await _processorService.StartProcessorAsync();
            await _processorService.WaitForProcessorToFinishInitializatonAsync();

            var createdCustomersInOldDatabase = new List<OldCustomerOperationResult>();
            var createdInvoicesInOldDatabase = new List<OldInvoiceOperationResult>();

            // Send inserts to old database - before stopping container
            var startTime = DateTime.UtcNow;
            var endTime = startTime.AddSeconds(10);

            while (DateTime.UtcNow < endTime)
            {
                createdCustomersInOldDatabase.Add(await _databaseOperationsService.CreateCustomerInOldDatabaseAsync());
                createdInvoicesInOldDatabase.Add(await _databaseOperationsService.CreateInvoiceInOldDatabaseAsync());
            }

            // Stop container
            await _dockerService.StopContainerAsync(containerName);

            // Send inserts to old database - after stopping container
            var startTime2 = DateTime.UtcNow;
            var endTime2 = startTime.AddSeconds(10);

            while (DateTime.UtcNow < endTime2)
            {
                createdCustomersInOldDatabase.Add(await _databaseOperationsService.CreateCustomerInOldDatabaseAsync());
                createdInvoicesInOldDatabase.Add(await _databaseOperationsService.CreateInvoiceInOldDatabaseAsync());
            }

            // Start container
            await _dockerService.StartContainerAsync(containerName);

            // Assert
            var conditions = new List<ConditionCheck>();

            foreach (var customer in createdCustomersInOldDatabase)
            {
                conditions.Add(new ConditionCheck
                {
                    Condition = () => _databaseOperationsService.CustomerExistsInNewDatabaseAsync(customer),
                    Description = "customer in new database"
                });
            }

            foreach (var invoice in createdInvoicesInOldDatabase)
            {
                conditions.Add(new ConditionCheck
                {
                    Condition = () => _databaseOperationsService.InvoiceExistsInNewDatabaseAsync(invoice),
                    Description = "invoice in new database"
                });
            }

            var totalNumberOfInserts = conditions.Count();
            Console.WriteLine($"Total number of inserts: {totalNumberOfInserts}");

            await WaitForAndAssertConditionsAsync(true, conditions.ToArray());
        }

        private void LogCreatedData(
            List<OldCustomerOperationResult> createdCustomersInOldDatabase,
            List<OldInvoiceOperationResult> createdInvoicesInOldDatabase,
            List<NewCustomerOperationResult> createdCustomersInNewDatabase,
            List<NewInvoiceOperationResult> createdInvoicesInNewDatabase)
        {
            foreach (OldCustomerOperationResult customer in createdCustomersInOldDatabase)
            {
                _logger.LogInformation($"Created: {customer}");
            }
            foreach (OldInvoiceOperationResult invoice in createdInvoicesInOldDatabase)
            {
                _logger.LogInformation($"Created: {invoice}");
            }
            foreach (NewCustomerOperationResult customer in createdCustomersInNewDatabase)
            {
                _logger.LogInformation($"Created: {customer}");
            }
            foreach (NewInvoiceOperationResult invoice in createdInvoicesInNewDatabase)
            {
                _logger.LogInformation($"Created: {invoice}");
            }
        }

        private async Task WaitForAndAssertConditionsAsync(bool killDebeziumProcessor, params ConditionCheck[] conditions)
        {
            for (int i = 0; i < DB_WAITING_MAX_RETIRES; i++)
            {
                bool allConditionsMet = true;

                foreach (var condition in conditions)
                {
                    try
                    {
                        var conditionResult = await condition.Condition();
                        if (conditionResult != string.Empty)
                        {
                            _logger.LogWarning($"Condition failure: {conditionResult}");
                            allConditionsMet = false;
                            break;
                        }
                    }
                    catch (Exception)
                    {
                        allConditionsMet = false;
                        break;
                    }
                }

                if (allConditionsMet)
                {
                    break;
                }

                await Task.Delay(DB_WAITING_DELAY);
            }

            if (killDebeziumProcessor)
            {
                _logger.LogInformation("Killing Debezium processor...");
                await _processorService.KillProcessorAsync(); // required so that logs that we assert are not used by this process

                // Checking for errors in processor logs
                Assert.True(await _processorService.AreProcessorLogsFreeFromErrors(), "There are errors or failures in the processor logs");
            }

            // Checking conditions after finishing the loop
            foreach (var condition in conditions)
            {
                _logger.LogInformation($"Verifying {condition.Description}...");
                Assert.True(await condition.Condition() == string.Empty, condition.Description);
            }

            _logger.LogInformation($"Verified all the conditions");
        }
    }
}