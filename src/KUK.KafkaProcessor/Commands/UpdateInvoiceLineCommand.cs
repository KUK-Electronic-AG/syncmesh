using KUK.KafkaProcessor.Services.Interfaces;
using Newtonsoft.Json.Linq;

namespace KUK.KafkaProcessor.Commands
{
    public class UpdateInvoiceLineCommand : IEventCommand
    {
        private readonly IInvoiceLineService _invoiceLineService;

        public UpdateInvoiceLineCommand(IInvoiceLineService invoiceLineService)
        {
            _invoiceLineService = invoiceLineService;
        }

        public async Task ExecuteToNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            await _invoiceLineService.UpdateInNewDatabase(eventBody, syncId, uniqueIdentifier);
        }

        public async Task ExecuteToOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            await _invoiceLineService.UpdateInOldDatabase(eventBody, syncId, uniqueIdentifier);
        }
    }
}
