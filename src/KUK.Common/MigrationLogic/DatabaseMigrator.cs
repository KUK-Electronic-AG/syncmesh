using KUK.Common.Contexts;
using KUK.Common.MigrationLogic.Interfaces;
using KUK.Common.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data.Common;

namespace KUK.Common.MigrationLogic
{
    public class DatabaseMigrator : IDatabaseMigrator
    {
        private readonly Chinook1DataChangesContext _chinook1Context;
        private readonly Chinook2Context _chinook2Context;
        private readonly DataMigrationRunner _dataMigrationRunner;
        private readonly ILogger<DatabaseMigrator> _logger;
        private readonly ITriggersCreationService _triggersCreationService;
        private readonly IConfiguration _configuration;
        private readonly IUtilitiesService _utilitiesService;

        public DatabaseMigrator(
            Chinook1DataChangesContext chinook1Context,
            Chinook2Context chinook2Context,
            DataMigrationRunner dataMigrationRunner,
            ILogger<DatabaseMigrator> logger,
            ITriggersCreationService triggersCreationService,
            IConfiguration configuration,
            IUtilitiesService utilitiesService)
        {
            _chinook1Context = chinook1Context;
            _chinook2Context = chinook2Context;
            _dataMigrationRunner = dataMigrationRunner;
            _logger = logger;
            _triggersCreationService = triggersCreationService;
            _configuration = configuration;
            _utilitiesService = utilitiesService;
        }

        public async Task CreateNewDatabaseIfNotExists(string newDatabaseName)
        {
            string environmentDestination = Environment.GetEnvironmentVariable("DebeziumWorker_EnvironmentDestination");
            string envVariableNewMySqlDebeziumPassword = $"DebeziumWorker_{environmentDestination}_NewDatabaseDebeziumPassword";
            var newDatabasePassword = Environment.GetEnvironmentVariable(envVariableNewMySqlDebeziumPassword);
            var newDatabaseConnectionString = _configuration["Databases:NewDatabase:ConnectionString"].Replace("{password}", newDatabasePassword);
            var connectionStringBuilder = new NpgsqlConnectionStringBuilder(newDatabaseConnectionString);

            var mode = _utilitiesService.GetOnlineOrDockerMode();
            if (mode == ApplicationDestinationMode.Online)
            {
                connectionStringBuilder.Database = "defaultdb"; // By default Postgres uses username as database name, hence we need different one
            }

            using (var connection = new NpgsqlConnection(connectionStringBuilder.ConnectionString))
            {
                connection.Open();
                using (var checkCmd = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @dbname", connection))
                {
                    checkCmd.Parameters.AddWithValue("dbname", newDatabaseName);
                    var exists = await checkCmd.ExecuteScalarAsync();

                    if (exists == null)
                    {
                        using (var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{newDatabaseName}\"", connection))
                        {
                            await createCmd.ExecuteNonQueryAsync();
                            _logger.LogInformation($"Database '{newDatabaseName}' has been created.");
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"Database '{newDatabaseName}' already exists.");
                    }
                }
            }
        }

        public async Task Migrate()
        {
            var connection = _chinook2Context.Database.GetDbConnection();
            ValidateSslForNewDatabase(connection);

            try
            {
                await connection.OpenAsync();

                _logger.LogInformation("Ensuring that schema is created for new database");
                await ((DbContext)_chinook2Context).Database.EnsureCreatedAsync();

                _logger.LogInformation("Applying schema migrations to db_chinook2");
                await _chinook2Context.Database.MigrateAsync();

                _logger.LogInformation("Running data migrations");
                await _dataMigrationRunner.RunMigrations();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Cannot migrate due to exception: {ex}");
            }
            finally
            {
                await CloseConnection(connection);
            }
        }

        public async Task CreateTriggersInNewDatabase()
        {
            var connection = _chinook2Context.Database.GetDbConnection();

            ValidateSslForNewDatabase(connection);

            try
            {
                await connection.OpenAsync();

                _logger.LogInformation("Ensuring that triggers are created");
                await _triggersCreationService.CreateTriggers();
            }
            finally
            {
                await CloseConnection(connection);
            }
        }

        private void ValidateSslForNewDatabase(DbConnection connection)
        {
            var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
            if (connectionStringBuilder.SslMode == SslMode.Disable)
            {
                _logger.LogWarning($"You have disabled SSL mode for new database. It is very dangerous for production scenario but is fine for local testing with Docker.");
            }
        }

        private async Task CloseConnection(DbConnection connection)
        {
            if (connection != null)
            {
                if (connection.State == System.Data.ConnectionState.Closed)
                {
                    _logger.LogWarning("Connection is already closed at finally after db initialization");
                }
                _logger.LogInformation($"Connection state: {connection.State}");
                await connection.CloseAsync();
            }
            else
            {
                _logger.LogWarning("Connection is null in finally block after db initialization");
            }
        }
    }
}
