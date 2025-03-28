using KUK.Common.Contexts;
using KUK.Common.MigrationLogic.Interfaces;
using KUK.KafkaProcessor.Services.Interfaces;
using Npgsql;

namespace KUK.KafkaProcessor.Services
{
    public class InitializationService : IInitializationService
    {
        private readonly Chinook1DataChangesContext _oldContext;
        private readonly Chinook2Context _newContext;
        private readonly ILogger<InitializationService> _logger;
        private readonly IDatabaseMigrator _databaseMigrator;
        private readonly IConfiguration _configuration;

        public InitializationService(
            Chinook1DataChangesContext oldContext,
            Chinook2Context newContext,
            ILogger<InitializationService> logger,
            IDatabaseMigrator databaseMigrator,
            IConfiguration configuration)
        {
            _oldContext = oldContext;
            _newContext = newContext;
            _logger = logger;
            _databaseMigrator = databaseMigrator;
            _configuration = configuration;
        }

        public async Task<bool> InitializeNewDatabase()
        {
            try
            {
                _logger.LogInformation($"Creating database if not exists");
                string newDatabaseName = GetNewDatabaseName();
                await _databaseMigrator.CreateNewDatabaseIfNotExists(newDatabaseName);
                _logger.LogInformation($"Initializing new database");
                await _databaseMigrator.Migrate();
                _logger.LogInformation($"New database initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception while initializing new database: {ex}");
                return false;
            }
        }

        public async Task<bool> CreateTriggersInNewDatabase()
        {
            try
            {
                _logger.LogInformation($"Creating triggers in new database");
                await _databaseMigrator.CreateTriggersInNewDatabase();
                _logger.LogInformation($"Triggers in new database created successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception while creating triggers in new database: {ex}");
                return false;
            }
        }

        private string GetNewDatabaseName()
        {
            string connectionString = _configuration["Databases:NewDatabase:ConnectionString"];
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException($"Missing Databases:NewDatabase:ConnectionString entry in appsettings.json");
            }
            var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(connectionStringBuilder.Database))
            {
                throw new InvalidOperationException($"Databases:NewDatabase:ConnectionString entry in appsettings.json does not contain database name");
            }
            return connectionStringBuilder.Database;
        }
    }
}
