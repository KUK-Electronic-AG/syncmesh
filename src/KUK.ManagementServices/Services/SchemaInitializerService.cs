using System.Diagnostics;
using System.Reflection;
using KUK.Common;
using KUK.Common.Contexts;
using KUK.Common.MigrationLogic.Interfaces;
using KUK.Common.Services;
using KUK.Common.Utilities;
using KUK.ManagementServices.Services.Interfaces;
using KUK.ManagementServices.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Npgsql;

namespace KUK.ManagementServices.Services
{
    public class SchemaInitializerService : ISchemaInitializerService
    {
        private readonly OldDbContext _oldDbDataChangesContext;
        private readonly OldDbContext _oldDbRootContext;
        private readonly NewDbContext _newDbContext;
        private readonly ILogger<SchemaInitializerService> _logger;
        private readonly AppSettingsConfig _appSettingsConfig;
        private readonly IDockerService _dockerService;
        private readonly IDatabaseManagementService _databaseManagementService;
        private readonly IUtilitiesService _utilitiesService;
        private readonly IDatabaseDirectConnectionService _databaseDirectConnectionService;
        private readonly IConfiguration _configuration;
        private readonly ICustomTriggersCreationService<OldDbContext, NewDbContext> _customTriggersCreationService;
        private readonly ICustomSchemaInitializerService _customSchemaInitializerService;

        private const int WAITING_TIMEOUT = 60000; // 60 seconds
        private const int WAITING_DELAY = 500; // 0.5 seconds

        public SchemaInitializerService(
            OldDbContext oldDbDataChangesContext,
            OldDbContext oldDbRootContext,
            NewDbContext newDbContext,
            ILogger<SchemaInitializerService> logger,
            AppSettingsConfig appSettingsConfig,
            IDockerService dockerService,
            IDatabaseManagementService databaseManagementService,
            IUtilitiesService utilitiesService,
            IDatabaseDirectConnectionService databaseDirectConnectionService,
            IConfiguration configuration,
            ICustomTriggersCreationService<OldDbContext, NewDbContext> customTriggersCreationService,
            ICustomSchemaInitializerService customSchemaInitializerService)
        {
            _oldDbDataChangesContext = oldDbDataChangesContext;
            _oldDbRootContext = oldDbRootContext;
            _newDbContext = newDbContext;
            _logger = logger;
            _appSettingsConfig = appSettingsConfig;
            _dockerService = dockerService;
            _databaseManagementService = databaseManagementService;
            _utilitiesService = utilitiesService;
            _databaseDirectConnectionService = databaseDirectConnectionService;
            _configuration = configuration;
            _customTriggersCreationService = customTriggersCreationService;
            _customSchemaInitializerService = customSchemaInitializerService;
        }

        public ServiceActionStatus InitializeSchema(WhichDatabaseEnum database, bool asRoot = false)
        {
            try
            {
                switch (database)
                {
                    case WhichDatabaseEnum.OldDatabase:
                        CreateSchemaIfNotExists(asRoot ? _oldDbRootContext : _oldDbDataChangesContext);
                        break;
                    case WhichDatabaseEnum.NewDatabase:
                        if (!asRoot)
                        {
                            CreateSchemaIfNotExists(_newDbContext);
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
                        CreateSchemaIfNotExists(asRoot ? _oldDbRootContext : _oldDbDataChangesContext);
                        CreateEmptyTables(asRoot ? _oldDbRootContext : _oldDbDataChangesContext);
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

        public async Task<ServiceActionStatus> InitializeSchemaTablesAndData(WhichDatabaseEnum database, string userName)
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
                        DropSchema(asRoot ? _oldDbRootContext : _oldDbDataChangesContext);
                        break;
                    case WhichDatabaseEnum.NewDatabase:
                        DropSchema(_newDbContext);
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
                        DropTables(asRoot ? _oldDbRootContext : _oldDbDataChangesContext);
                        break;
                    case WhichDatabaseEnum.NewDatabase:
                        DropTables(_newDbContext);
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
                        ClearData(asRoot ? _oldDbRootContext : _oldDbDataChangesContext);
                        break;
                    case WhichDatabaseEnum.NewDatabase:
                        ClearData(_newDbContext);
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

                // Executing SQL script with database intialization
                CreateSchemaIfNotExists(_oldDbRootContext);
                ImportDatabaseFromSqlFile(_oldDbRootContext);
                _customSchemaInitializerService.AddTestValueColumn(_oldDbRootContext, 3);
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
                    var assemblyName = _configuration["OnlineMode:AssemblyForCreateTablesForOldDatabase"];
                    var resourceName = _configuration["OnlineMode:ResourceNameForCreateTablesForOldDatabase"];

                    if (string.IsNullOrEmpty(assemblyName) || string.IsNullOrEmpty(resourceName))
                    {
                        throw new InvalidOperationException($"Either AssemblyForCreateTablesForOldDatabase or ResourceNameForCreateTablesForOldDatabase is empty.");
                    }

                    var assembly = Assembly.Load(assemblyName);

                    using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        string result = reader.ReadToEnd();
                        List<string> triggers = _customTriggersCreationService.SetupTriggers();

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

        private void CreateSchemaIfNotExists(IOldRootContext context)
        {
            // REMARK: Here we check if it CANNOT connect because connection string contains schema name
            // so if it cannot connect (and database is available) it means we need to create schema
            if (!((DbContext)context).Database.CanConnect())
            {
                ((DbContext)context).Database.EnsureCreated();
            }
        }

        private void CreateSchemaIfNotExists(NewDbContext context)
        {
            // REMARK: Here we check if it CANNOT connect because connection string contains schema name
            // so if it cannot connect (and database is available) it means we need to create schema
            if (!((DbContext)context).Database.CanConnect())
            {
                ((DbContext)context).Database.EnsureCreated();
            }
        }

        private void CreateEmptyTables(OldDbContext oldDbContext)
        {
            throw new NotImplementedException();
        }

        private void CreateEmptyTables(NewDbContext newDbContext)
        {
            throw new NotImplementedException();
        }

        private void ImportDatabaseFromSqlFile(OldDbContext context)
        {
            var assemblyName = _configuration["InitialCreationOfOldDatabase:AssemblyName"];
            var fileName = _configuration["InitialCreationOfOldDatabase:FileName"];
            var collationQuery = _configuration["InitialCreationOfOldDatabase:CollationQuery"];

            if (string.IsNullOrEmpty(assemblyName) || string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(collationQuery))
            {
                throw new InvalidOperationException($"You need to correctly provide InitialCreationOfOldDatabase section in appsettings");
            }

            var assembly = Assembly.Load(assemblyName);

            using (Stream stream = assembly.GetManifestResourceStream(fileName))
            using (StreamReader reader = new StreamReader(stream))
            {
                var sql = reader.ReadToEnd();
                ((DbContext)context).Database.ExecuteSqlRaw(collationQuery);
                ((DbContext)context).Database.ExecuteSqlRaw(sql);
            }
        }

        private void DropSchema(OldDbContext context) // REMARK: We can merge these DropSchema methods for both contexts
        {
            MySqlConnectionStringBuilder connectionStringBuilder = _databaseDirectConnectionService.GetOldDatabaseConnection(true);

            var schemaName = _configuration["Databases:SchemaNameOld"];
            if (string.IsNullOrEmpty(schemaName))
            {
                throw new InvalidOperationException($"Value of Databases:SchemaNameOld is missing in appsettings");
            }

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

        private void DropSchema(NewDbContext context)
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

        private void DropTables(OldDbContext context)
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

        private void DropTables(NewDbContext context)
        {
            var dbContext = (DbContext)context;

            dbContext.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS InvoiceLines");
            dbContext.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS Invoices");
            dbContext.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS Customers");
            dbContext.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS Addresses");
        }


        private void ClearData(OldDbContext context)
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

        private void ClearData(NewDbContext context)
        {
            var dbContext = (DbContext)context;

            dbContext.Database.ExecuteSqlRaw("DELETE FROM InvoiceLines");
            dbContext.Database.ExecuteSqlRaw("DELETE FROM Invoices");
            dbContext.Database.ExecuteSqlRaw("DELETE FROM Customers");
            dbContext.Database.ExecuteSqlRaw("DELETE FROM Addresses");
        }

        private OldDbContext CreateNewRootContextForOldDatabase()
        {
            var connectionString = _appSettingsConfig.RootOldDatabaseConnectionString;

            var optionsBuilder = new DbContextOptionsBuilder<OldDbContext>();
            optionsBuilder.UseMySQL(connectionString);

            return new OldDbContext(optionsBuilder.Options);
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