using System.Data.Common;
using KUK.Common.Contexts;
using KUK.Common.MigrationLogic.Interfaces;
using KUK.Common.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace KUK.Common.MigrationLogic
{
    public class DatabaseMigrator<TOld, TNew> : IDatabaseMigrator<TOld, TNew>
    {
        private readonly NewDbContext _newContext;
        private readonly IDataMigrationRunner<TOld, TNew> _dataMigrationRunner;
        private readonly ILogger<DatabaseMigrator<TOld, TNew>> _logger;
        private readonly ITriggersCreationService<TOld, TNew> _triggersCreationService;
        private readonly IConfiguration _configuration;
        private readonly IUtilitiesService _utilitiesService;

        public DatabaseMigrator(
            NewDbContext newContext,
            IDataMigrationRunner<TOld, TNew> dataMigrationRunner,
            ILogger<DatabaseMigrator<TOld, TNew>> logger,
            ITriggersCreationService<TOld, TNew> triggersCreationService,
            IConfiguration configuration,
            IUtilitiesService utilitiesService)
        {
            _newContext = newContext;
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
            var newDatabaseConnectionString = _utilitiesService.GetConnectionString(_configuration, "NewDatabase", false);
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

        public async Task Migrate(List<IDataMigrationBase<TOld, TNew>> migrations, Type migrationsContextType = null)
        {
            var connection = _newContext.Database.GetDbConnection();
            ValidateSslForNewDatabase(connection);

            try
            {
                await connection.OpenAsync();

                // Use ServiceProvider to create context that contains migrations
                var serviceProvider = _utilitiesService.GetServiceProvider();
                
                _logger.LogInformation("Applying schema migrations to new database");
                
                try 
                {
                    if (migrationsContextType != null && typeof(DbContext).IsAssignableFrom(migrationsContextType))
                    {
                        using (var scope = serviceProvider.CreateScope())
                        {
                            // Use passed type of the context
                            var dbContext = scope.ServiceProvider.GetRequiredService(migrationsContextType) as DbContext;
                            if (dbContext != null)
                            {
                                _logger.LogInformation($"Running MigrateAsync on context of type {migrationsContextType.Name}");
                                await dbContext.Database.MigrateAsync();
                                _logger.LogInformation("Migration completed successfully");
                            }
                            else
                            {
                                _logger.LogError($"Failed to get context of type {migrationsContextType.Name}");
                            }
                        }
                    }
                    else
                    {
                        // If context has not been passed, use default NewDbContext
                        _logger.LogInformation("Using default NewDbContext for migration (may not contain migrations)");
                        await _newContext.Database.MigrateAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Exception during migration: {ex}");
                    throw;
                }

                _logger.LogInformation("Running data migrations");
                await _dataMigrationRunner.RunMigrations(migrations);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Cannot migrate due to exception: {ex}");
                throw; // Let propagation of th exception to diagnose the problem
            }
            finally
            {
                await CloseConnection(connection);
            }
        }

        public async Task CreateTriggersInNewDatabase()
        {
            var connection = _newContext.Database.GetDbConnection();

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
