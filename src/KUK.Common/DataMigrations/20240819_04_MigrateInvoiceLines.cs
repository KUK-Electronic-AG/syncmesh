using KUK.Common.Contexts;
using KUK.Common.MigrationLogic;
using KUK.Common.ModelsNewSchema.Mapping;

namespace KUK.Common.DataMigrations
{
    public class MigrateInvoiceLines : DataMigrationBase
    {
        public override string MigrationName => "MigrateInvoiceLines";

        public override void Up(Chinook1DataChangesContext oldContext, Chinook2Context newContext)
        {
            var invoiceLineMappings = new List<InvoiceLineMapping>();
            var invoiceLines = new List<ModelsNewSchema.InvoiceLine>();

            foreach (var oldInvoiceLine in oldContext.InvoiceLines)
            {
                var newInvoiceLineId = Guid.NewGuid();

                // Get invoice mapping to get NewInvoiceId based on OldInvoiceId
                var invoiceMapping = newContext.InvoiceMappings.FirstOrDefault(im => im.OldInvoiceId == oldInvoiceLine.InvoiceId);
                if (invoiceMapping == null)
                {
                    throw new InvalidOperationException(
                        $"InvoiceMapping not found for OldInvoiceId: {oldInvoiceLine.InvoiceId}. " +
                        $"Ensure Invoice migration is run before InvoiceLine migration.");
                }
                Guid newInvoiceId = invoiceMapping.NewInvoiceId;

                var newInvoiceLine = new ModelsNewSchema.InvoiceLine
                {
                    InvoiceLineId = newInvoiceLineId,
                    InvoiceId = newInvoiceId,
                    TrackId = oldInvoiceLine.TrackId,
                    UnitPrice = oldInvoiceLine.UnitPrice,
                    Quantity = oldInvoiceLine.Quantity
                };
                invoiceLines.Add(newInvoiceLine);

                invoiceLineMappings.Add(new InvoiceLineMapping
                {
                    OldInvoiceLineId = oldInvoiceLine.InvoiceLineId,
                    NewInvoiceLineId = newInvoiceLineId,
                    MappingTimestamp = DateTime.UtcNow
                });
            }

            newContext.InvoiceLines.AddRange(invoiceLines);
            newContext.InvoiceLineMappings.AddRange(invoiceLineMappings);
            newContext.SaveChanges();
        }
    }
}