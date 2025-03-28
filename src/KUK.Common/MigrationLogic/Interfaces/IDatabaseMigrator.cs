namespace KUK.Common.MigrationLogic.Interfaces
{
    public interface IDatabaseMigrator
    {
        public Task CreateNewDatabaseIfNotExists(string newDatabaseName);
        public Task Migrate();
        public Task CreateTriggersInNewDatabase();
    }
}
