using KUK.Common.MigrationLogic.Interfaces;

namespace KUK.ChinookSync.Services
{
    public class CustomTriggersCreationService<TOld, TNew> : ICustomTriggersCreationService<TOld, TNew>
    {
        private readonly ITriggersCreationService<TOld, TNew> _triggersCreationService;

        public CustomTriggersCreationService(
            ITriggersCreationService<TOld, TNew> triggersCreationService)
        {
            _triggersCreationService = triggersCreationService;
        }

        public List<string> SetupTriggers()
        {
            var sqlCommands = new List<string>();

            // Common payload mapping for Customer INSERT and UPDATE triggers.
            var customerPayloadMapping = new Dictionary<string, string>
            {
                { "CustomerId", "CustomerId" },
                { "FirstName", "FirstName" },
                { "LastName", "LastName" },
                { "Company", "Company" },
                { "Address", "Address" },
                { "City", "City" },
                { "State", "State" },
                { "Country", "Country" },
                { "PostalCode", "PostalCode" },
                { "Phone", "Phone" },
                { "Fax", "Fax" },
                { "Email", "Email" },
                { "SupportRepId", "SupportRepId" }
            };

            // Trigger for Customer INSERT
            string customerInsertTrigger = _triggersCreationService.GenerateTrigger(
                triggerName: "trg_customer_insert",
                triggerEvent: "INSERT",
                tableName: "Customer",
                outboxTable: "customer_outbox",
                aggregateColumn: "CustomerId",
                aggregateType: "CUSTOMER",
                eventType: "CREATED",
                payloadMapping: customerPayloadMapping,
                rowAlias: "NEW"
            );
            sqlCommands.Add(customerInsertTrigger);

            // Trigger for Customer UPDATE
            string customerUpdateTrigger = _triggersCreationService.GenerateTrigger(
                triggerName: "trg_customer_update",
                triggerEvent: "UPDATE",
                tableName: "Customer",
                outboxTable: "customer_outbox",
                aggregateColumn: "CustomerId",
                aggregateType: "CUSTOMER",
                eventType: "UPDATED",
                payloadMapping: customerPayloadMapping,
                rowAlias: "NEW"
            );
            sqlCommands.Add(customerUpdateTrigger);

            // For DELETE, only CustomerId is needed - we extract a separate mapping.
            var customerDeleteMapping = new Dictionary<string, string>
            {
                { "CustomerId", "CustomerId" }
            };
            string customerDeleteTrigger = _triggersCreationService.GenerateTrigger(
                triggerName: "trg_customer_delete",
                triggerEvent: "DELETE",
                tableName: "Customer",
                outboxTable: "customer_outbox",
                aggregateColumn: "CustomerId",
                aggregateType: "CUSTOMER",
                eventType: "DELETED",
                payloadMapping: customerDeleteMapping,
                rowAlias: "OLD"
            );
            sqlCommands.Add(customerDeleteTrigger);

            // Common payload mapping for Invoice INSERT and UPDATE triggers.
            var invoicePayloadMapping = new Dictionary<string, string>
            {
                { "InvoiceId", "InvoiceId" },
                { "CustomerId", "CustomerId" },
                { "InvoiceDate", "InvoiceDate" },
                { "BillingAddress", "BillingAddress" },
                { "BillingCity", "BillingCity" },
                { "BillingState", "BillingState" },
                { "BillingCountry", "BillingCountry" },
                { "BillingPostalCode", "BillingPostalCode" },
                { "Total", "Total" }
            };

            // Trigger for Invoice INSERT
            string invoiceInsertTrigger = _triggersCreationService.GenerateTrigger(
                triggerName: "trg_invoice_insert",
                triggerEvent: "INSERT",
                tableName: "Invoice",
                outboxTable: "invoice_outbox",
                aggregateColumn: "InvoiceId",
                aggregateType: "INVOICE",
                eventType: "CREATED",
                payloadMapping: invoicePayloadMapping,
                rowAlias: "NEW"
            );
            sqlCommands.Add(invoiceInsertTrigger);

            // Trigger for Invoice UPDATE
            string invoiceUpdateTrigger = _triggersCreationService.GenerateTrigger(
                triggerName: "trg_invoice_update",
                triggerEvent: "UPDATE",
                tableName: "Invoice",
                outboxTable: "invoice_outbox",
                aggregateColumn: "InvoiceId",
                aggregateType: "INVOICE",
                eventType: "UPDATED",
                payloadMapping: invoicePayloadMapping,
                rowAlias: "NEW"
            );
            sqlCommands.Add(invoiceUpdateTrigger);

            // For Invoice DELETE, only InvoiceId is needed.
            var invoiceDeleteMapping = new Dictionary<string, string>
            {
                { "InvoiceId", "InvoiceId" }
            };
            string invoiceDeleteTrigger = _triggersCreationService.GenerateTrigger(
                triggerName: "trg_invoice_delete",
                triggerEvent: "DELETE",
                tableName: "Invoice",
                outboxTable: "invoice_outbox",
                aggregateColumn: "InvoiceId",
                aggregateType: "INVOICE",
                eventType: "DELETED",
                payloadMapping: invoiceDeleteMapping,
                rowAlias: "OLD"
            );
            sqlCommands.Add(invoiceDeleteTrigger);

            // Common payload mapping for InvoiceLine INSERT and UPDATE triggers.
            var invoiceLinePayloadMapping = new Dictionary<string, string>
            {
                { "InvoiceLineId", "InvoiceLineId" },
                { "InvoiceId", "InvoiceId" },
                { "TrackId", "TrackId" },
                { "UnitPrice", "UnitPrice" },
                { "Quantity", "Quantity" }
            };

            // Trigger for InvoiceLine INSERT
            string invoiceLineInsertTrigger = _triggersCreationService.GenerateTrigger(
                triggerName: "trg_invoiceline_insert",
                triggerEvent: "INSERT",
                tableName: "InvoiceLine",
                outboxTable: "invoiceline_outbox",
                aggregateColumn: "InvoiceId",
                aggregateType: "INVOICELINE",
                eventType: "CREATED",
                payloadMapping: invoiceLinePayloadMapping,
                rowAlias: "NEW"
            );
            sqlCommands.Add(invoiceLineInsertTrigger);

            // Trigger for InvoiceLine UPDATE
            string invoiceLineUpdateTrigger = _triggersCreationService.GenerateTrigger(
                triggerName: "trg_invoiceline_update",
                triggerEvent: "UPDATE",
                tableName: "InvoiceLine",
                outboxTable: "invoiceline_outbox",
                aggregateColumn: "InvoiceId",
                aggregateType: "INVOICELINE",
                eventType: "UPDATED",
                payloadMapping: invoiceLinePayloadMapping,
                rowAlias: "NEW"
            );
            sqlCommands.Add(invoiceLineUpdateTrigger);

            // For InvoiceLine DELETE, only InvoiceLineId is needed.
            var invoiceLineDeleteMapping = new Dictionary<string, string>
            {
                { "InvoiceLineId", "InvoiceLineId" }
            };
            string invoiceLineDeleteTrigger = _triggersCreationService.GenerateTrigger(
                triggerName: "trg_invoiceline_delete",
                triggerEvent: "DELETE",
                tableName: "InvoiceLine",
                outboxTable: "invoiceline_outbox",
                aggregateColumn: "InvoiceId",
                aggregateType: "INVOICELINE",
                eventType: "DELETED",
                payloadMapping: invoiceLineDeleteMapping,
                rowAlias: "OLD"
            );
            sqlCommands.Add(invoiceLineDeleteTrigger);

            return sqlCommands;
        }
    }
}
