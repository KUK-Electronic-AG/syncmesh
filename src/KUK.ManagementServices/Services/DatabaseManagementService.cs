using System.Net.Sockets;
using KUK.Common;
using KUK.ManagementServices.Services.Interfaces;
using KUK.ManagementServices.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Npgsql;

namespace KUK.ManagementServices.Services
{
    public class DatabaseManagementService : IDatabaseManagementService
    {
        private readonly AppSettingsConfig _config;
        private readonly ILogger<DatabaseManagementService> _logger;
        private readonly IConfiguration _configuration;

        private readonly string oldSchemaName;
        private readonly string newSchemaName;
        private readonly string oldSchemaExemplaryTable;
        private readonly string newSchemaExemplaryTable;

        public DatabaseManagementService(
            AppSettingsConfig config,
            ILogger<DatabaseManagementService> logger,
            IConfiguration configuration)
        {
            _config = config;
            _logger = logger;
            _configuration = configuration;

            oldSchemaName = _configuration["Databases:SchemaNameOld"];
            newSchemaName = _configuration["Databases:SchemaNameNew"];
            oldSchemaExemplaryTable = _configuration["Databases:OldSchemaExemplaryTable"];
            newSchemaExemplaryTable = _configuration["Databases:NewSchemaExemplaryTable"];

            if (string.IsNullOrEmpty(oldSchemaName) || string.IsNullOrEmpty(newSchemaName) ||
                string.IsNullOrEmpty(oldSchemaExemplaryTable) || string.IsNullOrEmpty(newSchemaExemplaryTable))
            {
                throw new InvalidOperationException($"Missing values in Databases (SchemaName[Old|New] or [Old|New]SchemaExemplaryTable) in appsettings");
            }
        }

        public async Task<DbStatuses> CheckDatabasesStatus(DatabasesBasicConfiguration dbConfig)
        {
            bool oldDbAccessible = await CheckDatabaseConnectionAsync(dbConfig, WhichDatabaseEnum.OldDatabase);
            bool newDbAccessible = await CheckDatabaseConnectionAsync(dbConfig, WhichDatabaseEnum.NewDatabase);

            CheckStatusEnum oldSchemaExists = CheckStatusEnum.NotChecked;
            CheckStatusEnum newSchemaExists = CheckStatusEnum.NotChecked;
            CheckStatusEnum oldSchemaHasData = CheckStatusEnum.NotChecked;
            CheckStatusEnum newSchemaHasData = CheckStatusEnum.NotChecked;

            if (oldDbAccessible)
            {
                oldSchemaExists =
                    await CheckSchemaExistsAsync(dbConfig.RootOldDbConnectionString, oldSchemaName, WhichDatabaseEnum.OldDatabase)
                    ? CheckStatusEnum.Exists : CheckStatusEnum.NotExists;
                if (oldSchemaExists == CheckStatusEnum.Exists)
                {
                    oldSchemaHasData =
                        await CheckSchemaHasDataAsync(dbConfig.RootOldDbConnectionString, oldSchemaName, oldSchemaExemplaryTable, WhichDatabaseEnum.OldDatabase)
                        ? CheckStatusEnum.Exists : CheckStatusEnum.NotExists;
                }
            }

            if (newDbAccessible)
            {
                newSchemaExists = 
                    await CheckSchemaExistsAsync(dbConfig.NewDbConnectionString, newSchemaName, WhichDatabaseEnum.NewDatabase)
                    ? CheckStatusEnum.Exists : CheckStatusEnum.NotExists;
                if (newSchemaExists == CheckStatusEnum.Exists)
                {
                    newSchemaHasData = 
                        await CheckSchemaHasDataAsync(dbConfig.NewDbConnectionString, newSchemaName, newSchemaExemplaryTable, WhichDatabaseEnum.NewDatabase)
                        ? CheckStatusEnum.Exists : CheckStatusEnum.NotExists;
                }
            }

            var result = new DbStatuses()
            {
                OldDbStatus = oldDbAccessible ? DbStatusEnum.Connected : DbStatusEnum.Disconnected,
                NewDbStatus = newDbAccessible ? DbStatusEnum.Connected : DbStatusEnum.Disconnected,
                OldSchemaExists = oldSchemaExists,
                NewSchemaExists = newSchemaExists,
                OldSchemaHasData = oldSchemaHasData,
                NewSchemaHasData = newSchemaHasData,
            };

            return result;
        }

        public Task<bool> CheckDatabaseConnectionAsync(AppSettingsConfig appSettingsConfig, WhichDatabaseEnum whichDatabaseEnum, string userName = "")
        {
            // REMARK: Here we can remove the need to use two classes or use AutoMapper
            var databasesBasicConfiguration = new DatabasesBasicConfiguration()
            {
                RootOldDbConnectionString = appSettingsConfig.RootOldDatabaseConnectionString,
                OldDbConnectionString = appSettingsConfig.OldDatabaseConnectionString,
                NewDbConnectionString = appSettingsConfig.NewDatabaseConnectionString,
                OldDbName = appSettingsConfig.OldDatabaseName,
                NewDbName = appSettingsConfig.NewDatabaseName,
                OldDbPort = appSettingsConfig.OldDatabasePort,
                NewDbPort = appSettingsConfig.NewDatabasePort,
            };

            var result = CheckDatabaseConnectionAsync(databasesBasicConfiguration, whichDatabaseEnum, userName);
            return result;
        }

        /// <remarks>
        /// We do it in such non-standard way (with socket) to make debugging easier.
        /// Catching exceptions would lead to exceptions being caught if all CLR exceptions are set to true in exception settings.
        /// In this way we won't have exception (unless it is really exceptional situation).
        /// </remarks>
        public async Task<bool> CheckDatabaseConnectionAsync(
            DatabasesBasicConfiguration databasesBasicConfiguration,
            WhichDatabaseEnum whichDatabaseEnum,
            string userName = "")
        {
            string connectionString = string.Empty;
            string server = string.Empty;
            int port = -1;

            switch (whichDatabaseEnum)
            {
                case WhichDatabaseEnum.OldDatabase:
                    connectionString = databasesBasicConfiguration.RootOldDbConnectionString;
                    server = databasesBasicConfiguration.OldDbName;
                    port = databasesBasicConfiguration.OldDbPort;
                    break;
                case WhichDatabaseEnum.NewDatabase:
                    connectionString = databasesBasicConfiguration.NewDbConnectionString;
                    server = databasesBasicConfiguration.NewDbName;
                    port = databasesBasicConfiguration.NewDbPort;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown database {whichDatabaseEnum}");
            }

            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                var connectTask = Task.Factory.FromAsync(
                    socket.BeginConnect(server, port, null, null),
                    socket.EndConnect
                );

                bool success = await Task.WhenAny(connectTask, Task.Delay(TimeSpan.FromSeconds(1))) == connectTask && socket.Connected;
                if (!success)
                {
                    return false;
                }

                // If the connection with the server is successful, try to open connection with the modified connection string
                switch (whichDatabaseEnum)
                {
                    case WhichDatabaseEnum.OldDatabase:
                        var mysqlBuilder = new MySqlConnectionStringBuilder(connectionString)
                        {
                            Database = null
                        };
                        if (!string.IsNullOrWhiteSpace(userName))
                        {
                            mysqlBuilder.UserID = userName;
                        }
                        using (var connection = new MySqlConnection(mysqlBuilder.ConnectionString))
                        {
                            await connection.OpenAsync();
                        }
                        break;
                    case WhichDatabaseEnum.NewDatabase:
                        var npgsqlBuilder = new NpgsqlConnectionStringBuilder(connectionString)
                        {
                            Username = _config.PostgresAccountName
                        };
                        using (var connection = new NpgsqlConnection(npgsqlBuilder.ConnectionString))
                        {
                            await connection.OpenAsync();
                        }
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown database {whichDatabaseEnum}");
                }
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> CheckSchemaExistsAsync(string connectionString, string schemaName, WhichDatabaseEnum whichDatabaseEnum)
        {
            try
            {
                switch (whichDatabaseEnum)
                {
                    case WhichDatabaseEnum.OldDatabase:
                        // Modify the connection string to remove the database name
                        var mysqlBuilder = new MySqlConnectionStringBuilder(connectionString)
                        {
                            Database = string.Empty
                        };
                        using (var connection = new MySqlConnection(mysqlBuilder.ConnectionString))
                        {
                            await connection.OpenAsync();
                            using var command = new MySqlCommand($"SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = @schemaName", connection);
                            command.Parameters.AddWithValue("@schemaName", schemaName);
                            var result = await command.ExecuteScalarAsync();
                            return result != null;
                        }
                    case WhichDatabaseEnum.NewDatabase:
                        var npgsqlBuilder = new NpgsqlConnectionStringBuilder(connectionString)
                        {
                            Username = _config.PostgresAccountName
                        };
                        using (var connection = new Npgsql.NpgsqlConnection(npgsqlBuilder.ConnectionString))
                        {
                            await connection.OpenAsync();
                            using var command = new Npgsql.NpgsqlCommand(
                                "SELECT schema_name FROM information_schema.schemata WHERE schema_name = @schemaName",
                                connection);
                            command.Parameters.AddWithValue("@schemaName", schemaName);
                            var result = await command.ExecuteScalarAsync();
                            return result != null;
                        }
                    default:
                        throw new InvalidOperationException($"Unknown database {whichDatabaseEnum}");
                }
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> CheckSchemaHasDataAsync(string connectionString, string schemaName, string tableName, WhichDatabaseEnum whichDatabaseEnum)
        {
            try
            {
                switch (whichDatabaseEnum)
                {
                    case WhichDatabaseEnum.OldDatabase:
                        using (var connection = new MySqlConnection(connectionString))
                        {
                            await connection.OpenAsync();

                            // Check if table exists
                            using var tableCheckCommand = new MySqlCommand($"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{schemaName}' AND TABLE_NAME = '{tableName}'", connection);
                            var tableExists = Convert.ToInt32(await tableCheckCommand.ExecuteScalarAsync()) > 0;
                            if (!tableExists) return false;

                            // Check if table contains data
                            using var dataCommand = new MySqlCommand($"SELECT COUNT(*) FROM {schemaName}.{tableName}", connection);
                            var dataCount = Convert.ToInt32(await dataCommand.ExecuteScalarAsync());
                            return dataCount > 0;
                        }
                    case WhichDatabaseEnum.NewDatabase:
                        using (var connection = new Npgsql.NpgsqlConnection(connectionString))
                        {
                            await connection.OpenAsync();
                            using (var tableCheckCommand = new Npgsql.NpgsqlCommand(
                                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = @schemaName AND table_name = @tableName",
                                connection))
                            {
                                tableCheckCommand.Parameters.AddWithValue("@schemaName", schemaName);
                                tableCheckCommand.Parameters.AddWithValue("@tableName", tableName);
                                var tableExists = Convert.ToInt32(await tableCheckCommand.ExecuteScalarAsync()) > 0;
                                if (!tableExists)
                                    return false;
                            }
                            using (var dataCommand = new Npgsql.NpgsqlCommand($"SELECT COUNT(*) FROM {schemaName}.\"{tableName}\"", connection))
                            {
                                var dataCount = Convert.ToInt32(await dataCommand.ExecuteScalarAsync());
                                return dataCount > 0;
                            }
                        }
                    default:
                        throw new InvalidOperationException($"Unknown database {whichDatabaseEnum}");
                }
                
            }
            catch
            {
                return false;
            }
        }
    }
}
