using KUK.ChinookSync.Contexts;
using KUK.Common.MigrationLogic.Interfaces;

namespace KUK.ChinookSync.DataMigrations
{
    public class MigrateInvoicesAndRelatedTables : IDataMigrationBase<Chinook1DataChangesContext, Chinook2Context>
    {
        public string MigrationName => "MigrateInvoicesAndRelatedTables";

        public void Up(Chinook1DataChangesContext oldContext, Chinook2Context newContext)
        {
            var migrateAddresses = new MigrateAddresses();
            migrateAddresses.Up(oldContext, newContext);

            var migrateCustomers = new MigrateCustomers();
            migrateCustomers.Up(oldContext, newContext);

            var migrateInvoices = new MigrateInvoices();
            migrateInvoices.Up(oldContext, newContext);

            var migrateInvoiceLines = new MigrateInvoiceLines();
            migrateInvoiceLines.Up(oldContext, newContext);
        }
    }
}
