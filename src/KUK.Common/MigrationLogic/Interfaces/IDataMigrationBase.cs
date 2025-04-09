using KUK.Common.Contexts;

namespace KUK.Common.MigrationLogic.Interfaces
{
    public interface IDataMigrationBase<TOld, TNew>
    {
        public abstract string MigrationName { get; }
        public abstract void Up(TOld oldContext, TNew newContext);
    }

}
