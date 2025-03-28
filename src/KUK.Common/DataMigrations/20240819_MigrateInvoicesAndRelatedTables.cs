using KUK.Common.Contexts;
using KUK.Common.MigrationLogic;

namespace KUK.Common.DataMigrations
{
    public class MigrateInvoicesAndRelatedTables : DataMigrationBase
    {
        public override string MigrationName => "MigrateInvoicesAndRelatedTables";

        public override void Up(Chinook1DataChangesContext oldContext, Chinook2Context newContext)
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
