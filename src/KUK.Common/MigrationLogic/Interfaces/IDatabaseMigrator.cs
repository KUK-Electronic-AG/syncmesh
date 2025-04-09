using Microsoft.EntityFrameworkCore;
using System;

namespace KUK.Common.MigrationLogic.Interfaces
{
    public interface IDatabaseMigrator<TOld, TNew>
    {
        public Task CreateNewDatabaseIfNotExists(string newDatabaseName);
        public Task Migrate(List<IDataMigrationBase<TOld, TNew>> migrations, Type migrationsContextType = null);
        public Task CreateTriggersInNewDatabase();
    }
}
