using KUK.Common.Contexts;

namespace KUK.Common.MigrationLogic
{
    public abstract class DataMigrationBase
    {
        public abstract string MigrationName { get; }
        public abstract void Up(Chinook1DataChangesContext oldContext, Chinook2Context newContext);
    }

}
