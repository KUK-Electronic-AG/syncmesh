using KUK.KafkaProcessor.Services.Interfaces;
using Newtonsoft.Json.Linq;

namespace KUK.KafkaProcessor.Commands
{
    public class InsertInvoiceLineCommand : IEventCommand
    {
        private readonly IInvoiceLineService _invoiceLineService;

        public InsertInvoiceLineCommand(IInvoiceLineService invoiceLineService)
        {
            _invoiceLineService = invoiceLineService;
        }

        public async Task ExecuteToNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            await _invoiceLineService.AddToNewDatabase(eventBody, syncId, uniqueIdentifier);
        }

        public async Task ExecuteToOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            await _invoiceLineService.AddToOldDatabase(eventBody, syncId, uniqueIdentifier);
        }
    }
}
