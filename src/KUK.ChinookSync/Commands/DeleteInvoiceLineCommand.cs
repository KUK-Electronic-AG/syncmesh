using KUK.ChinookSync.Services.Domain.Interfaces;
using KUK.KafkaProcessor.Commands;
using Newtonsoft.Json.Linq;

namespace KUK.ChinookSync.Commands
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
