using KUK.ChinookSync.Services.Domain.Interfaces;
using KUK.KafkaProcessor.Commands;
using Newtonsoft.Json.Linq;

namespace KUK.ChinookSync.Commands
{
    public class DeleteInvoiceCommand : IEventCommand
    {
        private readonly IInvoiceService _invoiceService;

        public DeleteInvoiceCommand(IInvoiceService invoiceService)
        {
            _invoiceService = invoiceService;
        }

        public async Task ExecuteToNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            await _invoiceService.DeleteFromNewDatabase(eventBody, syncId, uniqueIdentifier);
        }

        public async Task ExecuteToOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            await _invoiceService.DeleteFromOldDatabase(eventBody, syncId, uniqueIdentifier);
        }
    }
}
