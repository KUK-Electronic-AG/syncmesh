using KUK.Common;
using KUK.Common.Contexts;
using KUK.Common.DataMigrations;
using KUK.Common.Services;
using KUK.ManagementServices.Services.Interfaces;
using KUK.ManagementServices.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Npgsql;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace KUK.ManagementServices.Services
{
    public class SchemaInitializerService : ISchemaInitializerService
    {
        private readonly IChinook1DataChangesContext _chinook1DataChangesContext;
        private readonly IChinook1RootContext _chinook1RootContext;
        private readonly IChinook2Context _chinook2Context;
        private readonly ILogger<SchemaInitializerService> _logger;
        private readonly AppSettingsConfig _appSettingsConfig;
        private readonly IDockerService _dockerService;
        private readonly IDatabaseManagementService _databaseManagementService;
        private readonly IDatabaseDumpService _databaseDumpService;
        private readonly IUtilitiesService _utilitiesService;
        private readonly IConfiguration _configuration;
        private readonly IDatabaseDirectConnectionService _databaseDirectConnectionService;

        private const int WAITING_TIMEOUT = 60000; // 60 seconds
        private const int WAITING_DELAY = 500; // 0.5 seconds

        public SchemaInitializerService(
            IChinook1DataChangesContext chinook1DataChangesContext,
            IChinook1RootContext chinook1RootContext,
            IChinook2Context chinook2Context,
            ILogger<SchemaInitializerService> logger,
            AppSettingsConfig appSettingsConfig,
            IDockerService dockerService,
            IDatabaseManagementService databaseManagementService,
            IDatabaseDumpService databaseDumpService,
            IUtilitiesService utilitiesService,
            IConfiguration configuration,
            IDatabaseDirectConnectionService databaseDirectConnectionService)
        {
            _chinook1DataChangesContext = chinook1DataChangesContext;
            _chinook1RootContext = chinook1RootContext;
            _chinook2Context = chinook2Context;
            _logger = logger;
            _appSettingsConfig = appSettingsConfig;
            _dockerService = dockerService;
            _databaseManagementService = databaseManagementService;
            _databaseDumpService = databaseDumpService;
            _utilitiesService = utilitiesService;
            _configuration = configuration;
            _databaseDirectConnectionService = databaseDirectConnectionService;
        }

        public ServiceActionStatus InitializeSchema(WhichDatabaseEnum database, bool asRoot = false)
        {
            try
            {
                switch (database)
                {
                    case WhichDatabaseEnum.OldDatabase:
                        CreateSchemaIfNotExists(asRoot ? _chinook1RootContext : _chinook1DataChangesContext);
                        break;
                    case WhichDatabaseEnum.NewDatabase:
                        if (!asRoot)
                        {
                            CreateSchemaIfNotExists(_chinook2Context);
                        }
                        else
                        {
                            throw new NotImplementedException("We don't need to create database context for new database as root so it was not implemented.");
                        }
                        break;
                    default:
                        return new ServiceActionStatus { Success = false, Message = $"Unknown database {database}" };
                }

                return new ServiceActionStatus { Success = true, Message = "Schema initialized successfully." };
            }
            catch (Exception ex)
            {
                return new ServiceActionStatus { Success = false, Message = $"Unhandled exception: {ex.Message}" };
            }
        }

        public ServiceActionStatus InitializeSchemaAndTables(WhichDatabaseEnum database, bool asRoot = false)
        {
            try
            {
                switch (database)
                {
                    case WhichDatabaseEnum.OldDatabase:
                        CreateSchemaIfNotExists(asRoot ? _chinook1RootContext : _chinook1DataChangesContext);
                        CreateEmptyTables(asRoot ? _chinook1RootContext : _chinook1DataChangesContext);
                        break;
                    case WhichDatabaseEnum.NewDatabase:
                        throw new InvalidOperationException("We should initialize new database from KafkaProcessor");
                        break;
                    default:
                        return new ServiceActionStatus { Success = false, Message = $"Unknown database {database}" };
                }

                return new ServiceActionStatus { Success = true, Message = "Schema and tables initialized successfully." };
            }
            catch (Exception ex)
            {
                return new ServiceActionStatus { Success = false, Message = $"Unhandled exception: {ex.Message}" };
            }
        }

        public List<string> SetupTriggers()
        {
            var sqlCommands = new List<string>();

            // Common payload mapping for Customer INSERT and UPDATE triggers.
            var customerPayloadMapping = new Dictionary<string, string>
            {
                { "CustomerId", "CustomerId" },
                { "FirstName", "FirstName" },
                { "LastName", "LastName" },
                { "Company", "Company" },
                { "Address", "Address" },
                { "City", "City" },
                { "State", "State" },
                { "Country", "Country" },
                { "PostalCode", "PostalCode" },
                { "Phone", "Phone" },
                { "Fax", "Fax" },
                { "Email", "Email" },
                { "SupportRepId", "SupportRepId" }
            };

            // Trigger for Customer INSERT
            string customerInsertTrigger = GenerateTrigger(
                triggerName: "trg_customer_insert",
                triggerEvent: "INSERT",
                tableName: "Customer",
                outboxTable: "customer_outbox",
                aggregateColumn: "CustomerId",
                aggregateType: "CUSTOMER",
                eventType: "CREATED",
                payloadMapping: customerPayloadMapping,
                rowAlias: "NEW"
            );
            sqlCommands.Add(customerInsertTrigger);

            // Trigger for Customer UPDATE
            string customerUpdateTrigger = GenerateTrigger(
                triggerName: "trg_customer_update",
                triggerEvent: "UPDATE",
                tableName: "Customer",
                outboxTable: "customer_outbox",
                aggregateColumn: "CustomerId",
                aggregateType: "CUSTOMER",
                eventType: "UPDATED",
                payloadMapping: customerPayloadMapping,
                rowAlias: "NEW"
            );
            sqlCommands.Add(customerUpdateTrigger);

            // For DELETE, wystarczy tylko CustomerId – wyodrębniamy oddzielne mapping.
            var customerDeleteMapping = new Dictionary<string, string>
            {
                { "CustomerId", "CustomerId" }
            };
            string customerDeleteTrigger = GenerateTrigger(
                triggerName: "trg_customer_delete",
                triggerEvent: "DELETE",
                tableName: "Customer",
                outboxTable: "customer_outbox",
                aggregateColumn: "CustomerId",
                aggregateType: "CUSTOMER",
                eventType: "DELETED",
                payloadMapping: customerDeleteMapping,
                rowAlias: "OLD"
            );
            sqlCommands.Add(customerDeleteTrigger);

            // Common payload mapping for Invoice INSERT and UPDATE triggers.
            var invoicePayloadMapping = new Dictionary<string, string>
            {
                { "InvoiceId", "InvoiceId" },
                { "CustomerId", "CustomerId" },
                { "InvoiceDate", "InvoiceDate" },
                { "BillingAddress", "BillingAddress" },
                { "BillingCity", "BillingCity" },
                { "BillingState", "BillingState" },
                { "BillingCountry", "BillingCountry" },
                { "BillingPostalCode", "BillingPostalCode" },
                { "Total", "Total" }
            };

            // Trigger for Invoice INSERT
            string invoiceInsertTrigger = GenerateTrigger(
                triggerName: "trg_invoice_insert",
                triggerEvent: "INSERT",
                tableName: "Invoice",
                outboxTable: "invoice_outbox",
                aggregateColumn: "InvoiceId",
                aggregateType: "INVOICE",
                eventType: "CREATED",
                payloadMapping: invoicePayloadMapping,
                rowAlias: "NEW"
            );
            sqlCommands.Add(invoiceInsertTrigger);

            // Trigger for Invoice UPDATE
            string invoiceUpdateTrigger = GenerateTrigger(
                triggerName: "trg_invoice_update",
                triggerEvent: "UPDATE",
                tableName: "Invoice",
                outboxTable: "invoice_outbox",
                aggregateColumn: "InvoiceId",
                aggregateType: "INVOICE",
                eventType: "UPDATED",
                payloadMapping: invoicePayloadMapping,
                rowAlias: "NEW"
            );
            sqlCommands.Add(invoiceUpdateTrigger);

            // For Invoice DELETE, wystarczy tylko InvoiceId.
            var invoiceDeleteMapping = new Dictionary<string, string>
            {
                { "InvoiceId", "InvoiceId" }
            };
            string invoiceDeleteTrigger = GenerateTrigger(
                triggerName: "trg_invoice_delete",
                triggerEvent: "DELETE",
                tableName: "Invoice",
                outboxTable: "invoice_outbox",
                aggregateColumn: "InvoiceId",
                aggregateType: "INVOICE",
                eventType: "DELETED",
                payloadMapping: invoiceDeleteMapping,
                rowAlias: "OLD"
            );
            sqlCommands.Add(invoiceDeleteTrigger);

            // Common payload mapping for InvoiceLine INSERT and UPDATE triggers.
            var invoiceLinePayloadMapping = new Dictionary<string, string>
            {
                { "InvoiceLineId", "InvoiceLineId" },
                { "InvoiceId", "InvoiceId" },
                { "TrackId", "TrackId" },
                { "UnitPrice", "UnitPrice" },
                { "Quantity", "Quantity" }
            };

            // Trigger for InvoiceLine INSERT
            string invoiceLineInsertTrigger = GenerateTrigger(
                triggerName: "trg_invoiceline_insert",
                triggerEvent: "INSERT",
                tableName: "InvoiceLine",
                outboxTable: "invoiceline_outbox",
                aggregateColumn: "InvoiceId",
                aggregateType: "INVOICELINE",
                eventType: "CREATED",
                payloadMapping: invoiceLinePayloadMapping,
                rowAlias: "NEW"
            );
            sqlCommands.Add(invoiceLineInsertTrigger);

            // Trigger for InvoiceLine UPDATE
            string invoiceLineUpdateTrigger = GenerateTrigger(
                triggerName: "trg_invoiceline_update",
                triggerEvent: "UPDATE",
                tableName: "InvoiceLine",
                outboxTable: "invoiceline_outbox",
                aggregateColumn: "InvoiceId",
                aggregateType: "INVOICELINE",
                eventType: "UPDATED",
                payloadMapping: invoiceLinePayloadMapping,
                rowAlias: "NEW"
            );
            sqlCommands.Add(invoiceLineUpdateTrigger);

            // For InvoiceLine DELETE, wystarczy tylko InvoiceLineId.
            var invoiceLineDeleteMapping = new Dictionary<string, string>
            {
                { "InvoiceLineId", "InvoiceLineId" }
            };
            string invoiceLineDeleteTrigger = GenerateTrigger(
                triggerName: "trg_invoiceline_delete",
                triggerEvent: "DELETE",
                tableName: "InvoiceLine",
                outboxTable: "invoiceline_outbox",
                aggregateColumn: "InvoiceId",
                aggregateType: "INVOICELINE",
                eventType: "DELETED",
                payloadMapping: invoiceLineDeleteMapping,
                rowAlias: "OLD"
            );
            sqlCommands.Add(invoiceLineDeleteTrigger);

            return sqlCommands;
        }

        public async Task<ServiceActionStatus> InitializeSchemaTablesAndData(WhichDatabaseEnum database, string userName = "")
        {
            try
            {
                await WaitForDatabaseConnectionAsync(_appSettingsConfig, database, userName);

                if (database == WhichDatabaseEnum.OldDatabase)
                {
                    return await InitializeSchemaTablesAndDataForOldDatabase();
                }
                else if (database == WhichDatabaseEnum.NewDatabase)
                {
                    throw new InvalidOperationException($"We need to initialize new schema and tables from inside of KafkaProcessor, rather than here");
                }

                throw new UnreachableException("We shouldn't be able to reach this point. Adding so that compiler is happy.");
            }
            catch (Exception ex)
            {
                return new ServiceActionStatus { Success = false, Message = $"Failed to initialize {database}: {ex.Message}" };
            }
        }

        public ServiceActionStatus DeleteSchema(WhichDatabaseEnum database, bool asRoot = false)
        {
            try
            {
                if (asRoot && database == WhichDatabaseEnum.NewDatabase)
                {
                    throw new NotImplementedException(
                        "Deleting new database with asRoot equal to true is not implemented." +
                        "This is possible extension point.");
                }

                switch (database)
                {
                    case WhichDatabaseEnum.OldDatabase:
                        DropSchema(asRoot ? _chinook1RootContext : _chinook1DataChangesContext);
                        break;
                    case WhichDatabaseEnum.NewDatabase:
                        DropSchema(_chinook2Context);
                        break;
                    default:
                        return new ServiceActionStatus { Success = false, Message = $"Unknown database {database}" };
                }

                return new ServiceActionStatus { Success = true, Message = "Schema deleted successfully." };
            }
            catch (Exception ex)
            {
                return new ServiceActionStatus { Success = false, Message = $"Unhandled exception: {ex.Message}" };
            }
        }

        public ServiceActionStatus DeleteTables(WhichDatabaseEnum database, bool asRoot = false)
        {
            try
            {
                if (asRoot && database == WhichDatabaseEnum.NewDatabase)
                {
                    throw new NotImplementedException(
                        "Deleting new database with asRoot equal to true is not implemented." +
                        "This is possible extension point.");
                }

                switch (database)
                {
                    case WhichDatabaseEnum.OldDatabase:
                        DropTables(asRoot ? _chinook1RootContext : _chinook1DataChangesContext);
                        break;
                    case WhichDatabaseEnum.NewDatabase:
                        DropTables(_chinook2Context);
                        break;
                    default:
                        return new ServiceActionStatus { Success = false, Message = $"Unknown database {database}" };
                }

                return new ServiceActionStatus { Success = true, Message = "Tables deleted successfully." };
            }
            catch (Exception ex)
            {
                return new ServiceActionStatus { Success = false, Message = $"Unhandled exception: {ex.Message}" };
            }
        }

        public ServiceActionStatus DeleteData(WhichDatabaseEnum database, bool asRoot = false)
        {
            try
            {
                if (asRoot && database == WhichDatabaseEnum.NewDatabase)
                {
                    throw new NotImplementedException(
                        "Deleting database with asRoot equal to true is not implemented." +
                        "This is possible extension point.");
                }

                switch (database)
                {
                    case WhichDatabaseEnum.OldDatabase:
                        ClearData(asRoot ? _chinook1RootContext : _chinook1DataChangesContext);
                        break;
                    case WhichDatabaseEnum.NewDatabase:
                        ClearData(_chinook2Context);
                        break;
                    default:
                        return new ServiceActionStatus { Success = false, Message = $"Unknown database {database}" };
                }

                return new ServiceActionStatus { Success = true, Message = "Data deleted successfully." };
            }
            catch (Exception ex)
            {
                return new ServiceActionStatus { Success = false, Message = $"Unhandled exception: {ex.Message}" };
            }
        }

        private async Task<ServiceActionStatus> InitializeSchemaTablesAndDataForOldDatabase()
        {
            var database = WhichDatabaseEnum.OldDatabase;

            try
            {
                var mode = _utilitiesService.GetOnlineOrDockerMode();

                // Executing SQL script with Chinook database intialization
                CreateSchemaIfNotExists(_chinook1RootContext);
                ImportDatabaseFromSqlFile(_chinook1RootContext);
                AddTestValueColumn(_chinook1RootContext, 3);
                await WaitForDatabaseConnectionSchemaAndDataAsync(_appSettingsConfig, database);

                if (mode == ApplicationDestinationMode.Docker) // Continue with setup for Docker
                {
                    // Executing SQL script to initialize debezium and datachanges users 
                    // REMARK: We need both debezium user (for Debezium connectors) and datachanges user (for changes via this application)
                    var executeSqlResult = await _dockerService.ExecuteSqlScriptAsync("/home/persistent-initialization-files/init.sql");
                    if (!executeSqlResult.Success) return executeSqlResult;

                    string myCnfFileName = "/etc/mysql/conf.d/mysql.cnf"; // /etc/mysql/my.cnf (MySQL 5.7) or /etc/mysql/mysql.cnf (MySQL 5.6) or /etc/mysql/mysql.conf.d/mysqld.cnf (MySQL 8.0)

                    // Copying cnf file to /etc/mysql
                    var copyCnfResult = await _dockerService.CopyCnfFileAsync("/home/persistent-initialization-files/mysql.cnf", myCnfFileName);
                    if (!copyCnfResult.Success) return copyCnfResult;

                    // Changing permissions of cnf file
                    var chmodCnfResult = await _dockerService.ChangeCnfFilePermissionsAsync(myCnfFileName, "644"); // mysql.cnf
                    if (!chmodCnfResult.Success) return chmodCnfResult;

                    // Restarting MySQL service itself inside the container
                    var restartMySqlResult = await _dockerService.RestartContainerAsync("mysql80"); // REMARK: We may use enum rather then string
                    if (!restartMySqlResult.Success) return restartMySqlResult;

                    await WaitForDatabaseConnectionSchemaAndDataAsync(_appSettingsConfig, database); // wait again for database to startup

                    // REMARK: Here we may check via debezium user if SHOW VARIABLES LIKE 'log_bin'; shows ON or OFF. Throw exception if OFF.
                }
                else if (mode == ApplicationDestinationMode.Online)
                {
                    var assembly = Assembly.Load("KUK.Common");
                    var resourceName = "KUK.Common.Assets.CreateTablesForOldDatabase.sql";

                    using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        string result = reader.ReadToEnd();
                        List<string> triggers = SetupTriggers();

                        MySqlConnectionStringBuilder connectionStringBuilder = _databaseDirectConnectionService.GetOldDatabaseConnection();
                        using (var connection = new MySqlConnection(connectionStringBuilder.ConnectionString))
                        {
                            connection.Open();

                            using (var command = new MySqlCommand(result, connection))
                            {
                                command.ExecuteNonQuery();
                                _logger.LogInformation("CreateTablesForOldDatabase SQL script executed successfully.");
                            }

                            foreach (var sql in triggers)
                            {
                                using (var command = new MySqlCommand(sql, connection))
                                {
                                    command.ExecuteNonQuery();
                                }
                            }
                            _logger.LogInformation("Triggers SQL scripts executed successfully.");
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Unknown mode {mode}");
                }
            }
            catch (Exception ex)
            {
                return new ServiceActionStatus { Success = false, Message = $"{database} initializetion failed due to {ex}" };
            }

            return new ServiceActionStatus { Success = true, Message = $"{database} initialized successfully" };
        }

        private string GenerateTrigger(
            string triggerName,
            string triggerEvent,
            string tableName,
            string outboxTable,
            string aggregateColumn,
            string aggregateType,
            string eventType,
            Dictionary<string, string> payloadMapping,
            string rowAlias)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"CREATE TRIGGER {triggerName}");
            sb.AppendLine($"AFTER {triggerEvent}");
            sb.AppendLine($"ON {tableName} FOR EACH ROW");
            sb.AppendLine("BEGIN");
            sb.AppendLine("    DECLARE unique_identifier VARCHAR(36);");
            sb.AppendLine("    SET unique_identifier = UUID();");
            sb.AppendLine();

            // Build the JSON_OBJECT expression dynamically based on payloadMapping.
            var jsonParts = new List<string>();
            foreach (var kvp in payloadMapping)
            {
                // Each pair produces something like: 'CustomerId', NEW.CustomerId
                jsonParts.Add($"'{kvp.Key}', {rowAlias}.{kvp.Value}");
            }
            var jsonObjectExpression = $"JSON_OBJECT({string.Join(", ", jsonParts)})";

            sb.AppendLine($"    INSERT INTO {outboxTable} (aggregate_id, aggregate_type, event_type, payload, unique_identifier)");
            sb.AppendLine("    VALUES(");
            sb.AppendLine($"        {rowAlias}.{aggregateColumn},");
            sb.AppendLine($"        '{aggregateType}',");
            sb.AppendLine($"        '{eventType}',");
            sb.AppendLine($"        {jsonObjectExpression},");
            sb.AppendLine("        unique_identifier");
            sb.AppendLine("    );");
            sb.AppendLine("END");

            return sb.ToString();
        }

        private void CreateSchemaIfNotExists(IChinook1Context context)
        {
            // REMARK: Here we check if it CANNOT connect because connection string contains schema name
            // so if it cannot connect (and database is available) it means we need to create schema
            if (!((DbContext)context).Database.CanConnect())
            {
                ((DbContext)context).Database.EnsureCreated();
            }
        }

        private void CreateSchemaIfNotExists(IChinook2Context context)
        {
            // REMARK: Here we check if it CANNOT connect because connection string contains schema name
            // so if it cannot connect (and database is available) it means we need to create schema
            if (!((DbContext)context).Database.CanConnect())
            {
                ((DbContext)context).Database.EnsureCreated();
            }
        }

        private void CreateEmptyTables(IChinook1Context chinook1Context)
        {
            throw new NotImplementedException();
        }

        private void CreateEmptyTables(IChinook2Context chinook2Context)
        {
            throw new NotImplementedException();
        }

        private void ImportDatabaseFromSqlFile(IChinook1Context context)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "KUK.ManagementServices.Assets.Chinook_MySql_AutoIncrementPKs.sql";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                var sql = reader.ReadToEnd();
                ((DbContext)context).Database.ExecuteSqlRaw("SET NAMES latin1 COLLATE latin1_swedish_ci;");
                ((DbContext)context).Database.ExecuteSqlRaw(sql);
            }
        }

        private void AddTestValueColumn(IChinook1Context context, int precision)
        {
            // Always add precision as a suffix to the column name
            string columnName = $"TestValue{precision}";

            // Add TestValue column with specified precision
            var sql = $"ALTER TABLE db_chinook1.Invoice ADD COLUMN {columnName} DECIMAL(12,{precision});";
            ((DbContext)context).Database.ExecuteSqlRaw(sql);

            // Update all the records to add random TestValue with specified precision
            var updateSql = $@"
                UPDATE db_chinook1.Invoice
                SET {columnName} = ROUND(RAND() * POWER(10, {precision}), {precision});";
                    ((DbContext)context).Database.ExecuteSqlRaw(updateSql);
        }

        private void AddTestDateTimeColumn(IChinook1Context chinook1Context, int precision, int suffix)
        {
            // Always add suffix as a part of the column name
            string columnName = $"TestDate{suffix}";

            // Add TestDate column with specified precision
            var sql = $"ALTER TABLE db_chinook1.Invoice ADD COLUMN {columnName} DATETIME({precision});";
            ((DbContext)chinook1Context).Database.ExecuteSqlRaw(sql);

            // Update all the records to add random TestDate with specified precision
            var updateSql = $@"
                UPDATE db_chinook1.Invoice
                SET {columnName} = DATE_ADD('1970-01-01', INTERVAL RAND() * 1000000000 SECOND);";
            ((DbContext)chinook1Context).Database.ExecuteSqlRaw(updateSql);
        }

        private void TransformDataFromOldContextToNewContext(IChinook1Context chinook1Context, IChinook2Context chinook2Context)
        {
            var migrateInvoicesAndRelatedTables = new MigrateInvoicesAndRelatedTables();
            migrateInvoicesAndRelatedTables.Up((Chinook1DataChangesContext)chinook1Context, (Chinook2Context)chinook2Context);
        }

        private void DropSchema(IChinook1Context context) // REMARK: We can merge these DropSchema methods for both contexts
        {
            MySqlConnectionStringBuilder connectionStringBuilder = _databaseDirectConnectionService.GetOldDatabaseConnection(true);

            var schemaName = "db_chinook1";
            using (var connection = new MySqlConnection(connectionStringBuilder.ConnectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"DROP DATABASE IF EXISTS {schemaName}";
                    command.ExecuteNonQuery();
                }
            }
        }

        private void DropSchema(IChinook2Context context)
        {
            NpgsqlConnectionStringBuilder connectionStringBuilder = _databaseDirectConnectionService.GetNewDatabaseConnection();

            // Get the destination database from the context (the one that we want to delete)
            var targetDatabase = ((DbContext)context).Database.GetDbConnection().Database;

            using (var connection = new NpgsqlConnection(connectionStringBuilder.ConnectionString))
            {
                connection.Open();

                // First finish active connections to the database that we want to delete
                using (var terminateCmd = new NpgsqlCommand(@"
                    SELECT pg_terminate_backend(pid)
                    FROM pg_stat_activity
                    WHERE datname = @dbname AND pid <> pg_backend_pid()", connection))
                {
                    terminateCmd.Parameters.AddWithValue("dbname", targetDatabase);
                    terminateCmd.ExecuteNonQuery();
                }

                // Then execute DROP DATABASE command on destination database
                using (var dropCmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{targetDatabase}\"", connection))
                {
                    dropCmd.ExecuteNonQuery();
                    Console.WriteLine($"Database '{targetDatabase}' has been dropped.");
                }
            }
        }

        private void DropTables(IChinook1Context context)
        {
            var tables = ((DbContext)context).Model.GetEntityTypes()
                .Select(t => t.GetTableName())
                .Distinct()
                .ToList();

            foreach (var table in tables)
            {
                ((DbContext)context).Database.ExecuteSqlRaw($"DROP TABLE IF EXISTS {table} CASCADE");
            }
        }

        private void DropTables(IChinook2Context context)
        {
            var dbContext = (DbContext)context;

            dbContext.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS InvoiceLines");
            dbContext.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS Invoices");
            dbContext.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS Customers");
            dbContext.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS Addresses");
        }


        private void ClearData(IChinook1Context context)
        {
            var tables = ((DbContext)context).Model.GetEntityTypes()
                .Select(t => t.GetTableName())
                .Distinct()
                .ToList();

            foreach (var table in tables)
            {
                ((DbContext)context).Database.ExecuteSqlRaw($"DELETE FROM {table}");
            }
        }

        private void ClearData(IChinook2Context context)
        {
            var dbContext = (DbContext)context;

            dbContext.Database.ExecuteSqlRaw("DELETE FROM InvoiceLines");
            dbContext.Database.ExecuteSqlRaw("DELETE FROM Invoices");
            dbContext.Database.ExecuteSqlRaw("DELETE FROM Customers");
            dbContext.Database.ExecuteSqlRaw("DELETE FROM Addresses");
        }

        private IChinook1Context CreateNewRootContextForOldDatabase()
        {
            var connectionString = _appSettingsConfig.RootOldDatabaseConnectionString;

            var optionsBuilder = new DbContextOptionsBuilder<Chinook1Context>();
            optionsBuilder.UseMySQL(connectionString);

            return new Chinook1Context(optionsBuilder.Options);
        }

        private async Task<bool> WaitForDatabaseConnectionAsync(AppSettingsConfig appSettingsConfig, WhichDatabaseEnum whichDatabaseEnum, string userName = "")
        {
            int elapsed = 0;

            while (elapsed < WAITING_TIMEOUT)
            {
                bool result = await _databaseManagementService.CheckDatabaseConnectionAsync(appSettingsConfig, whichDatabaseEnum, userName);
                if (result)
                {
                    return true;
                }

                await Task.Delay(WAITING_DELAY);
                elapsed += WAITING_DELAY;
            }

            throw new TimeoutException("Database connection check timed out.");
        }

        private async Task<bool> WaitForDatabaseConnectionSchemaAndDataAsync(
            AppSettingsConfig appSettingsConfig,
            WhichDatabaseEnum whichDatabase,
            bool checkData = true)
        {
            int elapsed = 0;

            // REMARK: We may remove the need for two classes or use AutoMapper
            var dbConfig = new DatabasesBasicConfiguration()
            {
                RootOldDbConnectionString = appSettingsConfig.RootOldDatabaseConnectionString,
                OldDbConnectionString = appSettingsConfig.OldDatabaseConnectionString,
                NewDbConnectionString = appSettingsConfig.NewDatabaseConnectionString,
                OldDbName = appSettingsConfig.OldDatabaseName,
                NewDbName = appSettingsConfig.NewDatabaseName,
                OldDbPort = appSettingsConfig.OldDatabasePort,
                NewDbPort = appSettingsConfig.NewDatabasePort,
            };

            while (elapsed < WAITING_TIMEOUT)
            {
                // REMARK: Here we check statuses of both databases and we need to check only one
                DbStatuses result = await _databaseManagementService.CheckDatabasesStatus(dbConfig);

                // REMARK: We may try to remove duplication in this if-else

                if (checkData)
                {
                    bool connectionSchemaAndDataArePresent = false;

                    switch (whichDatabase)
                    {
                        case WhichDatabaseEnum.OldDatabase:
                            connectionSchemaAndDataArePresent =
                                result.OldDbStatus == DbStatusEnum.Connected
                                && result.OldSchemaExists == CheckStatusEnum.Exists
                                && result.OldSchemaHasData == CheckStatusEnum.Exists;
                            break;
                        case WhichDatabaseEnum.NewDatabase:
                            connectionSchemaAndDataArePresent =
                                result.NewDbStatus == DbStatusEnum.Connected
                                && result.NewSchemaExists == CheckStatusEnum.Exists
                                && result.NewSchemaHasData == CheckStatusEnum.Exists;
                            break;
                        default:
                            throw new InvalidOperationException($"Unknown {nameof(whichDatabase)} == {whichDatabase}");
                    }

                    if (connectionSchemaAndDataArePresent)
                    {
                        return true;
                    }
                }
                else
                {
                    bool connectionAndSchemaArePresent = false;

                    switch (whichDatabase)
                    {
                        case WhichDatabaseEnum.OldDatabase:
                            connectionAndSchemaArePresent =
                                result.OldDbStatus == DbStatusEnum.Connected
                                && result.OldSchemaExists == CheckStatusEnum.Exists;
                            break;
                        case WhichDatabaseEnum.NewDatabase:
                            connectionAndSchemaArePresent =
                                result.NewDbStatus == DbStatusEnum.Connected
                                && result.NewSchemaExists == CheckStatusEnum.Exists;
                            break;
                        default:
                            throw new InvalidOperationException($"Unknown {nameof(whichDatabase)} == {whichDatabase}");
                    }

                    if (connectionAndSchemaArePresent)
                    {
                        return true;
                    }
                }

                await Task.Delay(WAITING_DELAY);
                elapsed += WAITING_DELAY;
            }

            throw new TimeoutException("Database connection check timed out.");
        }
    }
}