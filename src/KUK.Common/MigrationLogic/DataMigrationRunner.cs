using KUK.Common.Contexts;
using KUK.Common.MigrationLogic.Interfaces;
using Microsoft.Extensions.Logging;

namespace KUK.Common.MigrationLogic
{
    public class DataMigrationRunner<TOld, TNew> : IDataMigrationRunner<TOld, TNew> where TNew : NewDbContext
    {
        private readonly TOld _oldContext;
        private readonly TNew _newContext;
        private readonly ILogger<DataMigrationRunner<TOld, TNew>> _logger;

        public DataMigrationRunner(
            TOld oldContext,
            TNew newContext,
            ILogger<DataMigrationRunner<TOld, TNew>> logger)
        {
            _oldContext = oldContext;
            _newContext = newContext;
            _logger = logger;
        }

        public async Task RunMigrations(List<IDataMigrationBase<TOld, TNew>> migrations)
        {
            

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
