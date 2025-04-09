using KUK.ChinookSync.Services.Domain.Interfaces;
using KUK.KafkaProcessor.Commands;
using Newtonsoft.Json.Linq;

namespace KUK.ChinookSync.Commands
{
    public class InsertInvoiceCommand : IEventCommand
    {
        private readonly IInvoiceService _invoiceService;

        public InsertInvoiceCommand(IInvoiceService invoiceService)
        {
            _invoiceService = invoiceService;
        }

        public async Task ExecuteToNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            await _invoiceService.AddToNewDatabase(eventBody, syncId, uniqueIdentifier);
        }

        public async Task ExecuteToOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            await _invoiceService.AddToOldDatabase(eventBody, syncId, uniqueIdentifier);
        }
    }
}
