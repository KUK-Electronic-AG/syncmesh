using KUK.ChinookSync.Contexts;
using KUK.ChinookSync.DataMigrations;
using KUK.Common.MigrationLogic.Interfaces;
using KUK.Common.Services;
using KUK.KafkaProcessor.Services.Interfaces;
using Npgsql;

namespace KUK.KafkaProcessor.Services
{
    public class InitializationService : IInitializationService
    {
        private readonly ILogger<InitializationService> _logger;
        private readonly IDatabaseMigrator<Chinook1DataChangesContext, Chinook2Context> _databaseMigrator;
        private readonly IConfiguration _configuration;
        private readonly IUtilitiesService _utilitiesService;

        public InitializationService(
            ILogger<InitializationService> logger,
            IDatabaseMigrator<Chinook1DataChangesContext, Chinook2Context> databaseMigrator,
            IConfiguration configuration,
            IUtilitiesService utilitiesService)
        {
            _logger = logger;
            _databaseMigrator = databaseMigrator;
            _configuration = configuration;
            _utilitiesService = utilitiesService;
        }

        public async Task<bool> InitializeNewDatabase()
        {
            try
            {
                _logger.LogInformation($"Creating database if not exists");
                string newDatabaseName = GetNewDatabaseName(_configuration);
                await _databaseMigrator.CreateNewDatabaseIfNotExists(newDatabaseName);
                _logger.LogInformation($"Initializing new database");
                var migrations = new List<IDataMigrationBase<Chinook1DataChangesContext, Chinook2Context>>
                {
                    new MigrateInvoicesAndRelatedTables()
                };
                
                await _databaseMigrator.Migrate(migrations, typeof(Chinook2Context));
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

        private string GetNewDatabaseName(IConfiguration configuration)
        {
            string connectionString = _utilitiesService.GetConnectionString(configuration, "NewDatabase", true);
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
