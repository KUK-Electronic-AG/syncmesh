using KUK.KafkaProcessor.Services.Interfaces;
using Newtonsoft.Json.Linq;

namespace KUK.KafkaProcessor.Commands
{
    public class DeleteInvoiceLineCommand : IEventCommand
    {
        private readonly IInvoiceLineService _invoiceLineService;

        public DeleteInvoiceLineCommand(IInvoiceLineService invoiceLineService)
        {
            _invoiceLineService = invoiceLineService;
        }

        public async Task ExecuteToNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            await _invoiceLineService.DeleteFromNewDatabase(eventBody, syncId, uniqueIdentifier);
        }

        public async Task ExecuteToOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            await _invoiceLineService.DeleteFromOldDatabase(eventBody, syncId, uniqueIdentifier);
        }
    }
}
