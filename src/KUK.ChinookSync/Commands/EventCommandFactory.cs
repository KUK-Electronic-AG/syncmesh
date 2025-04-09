using KUK.ChinookSync.Services.Domain.Interfaces;
using KUK.KafkaProcessor.Commands;

namespace KUK.ChinookSync.Commands
{
    public class EventCommandFactory : IEventCommandFactory
    {
        private readonly Dictionary<string, IEventCommand> _commands;

        private readonly ILogger<EventCommandFactory> _logger;

        public EventCommandFactory(
            ICustomerService customerService,
            IInvoiceService invoiceService,
            IInvoiceLineService invoiceLineService,
            IAddressService addressService,
            ILogger<EventCommandFactory> logger)
        {
            _logger = logger;

            _commands = new Dictionary<string, IEventCommand>
            {
                { "old_to_new_customer_outbox_c", new InsertCustomerCommand(customerService) },
                { "old_to_new_customer_outbox_u", new UpdateCustomerCommand(customerService) },
                { "old_to_new_customer_outbox_d", new DeleteCustomerCommand(customerService) },
                { "old_to_new_invoice_outbox_c", new InsertInvoiceCommand(invoiceService) },
                { "old_to_new_invoice_outbox_u", new UpdateInvoiceCommand(invoiceService) },
                { "old_to_new_invoice_outbox_d", new DeleteInvoiceCommand(invoiceService) },
                { "old_to_new_invoiceline_outbox_c", new InsertInvoiceLineCommand(invoiceLineService) },
                { "old_to_new_invoiceline_outbox_u", new UpdateInvoiceLineCommand(invoiceLineService) },
                { "old_to_new_invoiceline_outbox_d", new DeleteInvoiceLineCommand(invoiceLineService) },
                { "new_to_old_CustomerOutbox_c", new InsertCustomerCommand(customerService) },
                { "new_to_old_CustomerOutbox_u", new UpdateCustomerCommand(customerService) },
                { "new_to_old_CustomerOutbox_d", new DeleteCustomerCommand(customerService) },
                { "new_to_old_InvoiceOutbox_c", new InsertInvoiceCommand(invoiceService) },
                { "new_to_old_InvoiceOutbox_u", new UpdateInvoiceCommand(invoiceService) },
                { "new_to_old_InvoiceOutbox_d", new DeleteInvoiceCommand(invoiceService) },
                { "new_to_old_InvoiceLineOutbox_c", new InsertInvoiceLineCommand(invoiceLineService) },
                { "new_to_old_InvoiceLineOutbox_u", new UpdateInvoiceLineCommand(invoiceLineService) },
                { "new_to_old_InvoiceLineOutbox_d", new DeleteInvoiceLineCommand(invoiceLineService) },
                { "new_to_old_AddressOutbox_c", new InsertAddressCommand(addressService) },
                { "new_to_old_AddressOutbox_u", new UpdateAddressCommand(addressService) },
                // REMARK: Here you can add other commands for other tables and operations
            };
        }

        public IEventCommand GetCommand(string command, string table, string operation)
        {
            var key = $"{command}_{table}_{operation}";
            _logger.LogInformation($"Executing command with key: {key}");
            return _commands.ContainsKey(key) ? _commands[key] : null;
        }
    }
}
