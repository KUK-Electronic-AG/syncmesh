namespace KUK.Common.MigrationLogic.Interfaces
{
    public interface IDataMigrationRunner<TOld, TNew>
    {
        public Task RunMigrations(List<IDataMigrationBase<TOld, TNew>> migrations);
    }
}
