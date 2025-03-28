using KUK.Common.Contexts;
using KUK.Common.DataMigrations;
using KUK.Common.MigrationLogic.Interfaces;
using Microsoft.Extensions.Logging;

namespace KUK.Common.MigrationLogic
{
    public class DataMigrationRunner : IDataMigrationRunner
    {
        private readonly Chinook1DataChangesContext _oldContext;
        private readonly Chinook2Context _newContext;
        private readonly ILogger<DataMigrationRunner> _logger;

        public DataMigrationRunner(
            Chinook1DataChangesContext oldContext,
            Chinook2Context newContext,
            ILogger<DataMigrationRunner> logger)
        {
            _oldContext = oldContext;
            _newContext = newContext;
            _logger = logger;
        }

        public async Task RunMigrations()
        {
            var migrations = new List<DataMigrationBase>
            {
                new MigrateInvoicesAndRelatedTables()
            };

            foreach (var migration in migrations)
            {
                if (!_newContext.DataMigrations.Any(m => m.MigrationName == migration.MigrationName))
                {
                    _logger.LogInformation($"Applying {migration.MigrationName}.");
                    migration.Up(_oldContext, _newContext);
                    _newContext.DataMigrations.Add(new DataMigration
                    {
                        MigrationName = migration.MigrationName,
                        AppliedOn = DateTime.UtcNow
                    });
                    await _newContext.SaveChangesAsync();
                    _logger.LogInformation($"Applied {migration.MigrationName}.");
                }
                else
                {
                    _logger.LogInformation($"Skipping migration {migration.MigrationName} because it was already applied.");
                }
            }
        }
    }
}
