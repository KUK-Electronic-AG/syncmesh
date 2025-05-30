﻿using KUK.ChinookSync.Services.Domain.Interfaces;
using KUK.KafkaProcessor.Commands;
using Newtonsoft.Json.Linq;

namespace KUK.ChinookSync.Commands
{
    public class UpdateInvoiceCommand : IEventCommand
    {
        private readonly IInvoiceService _invoiceService;

        public UpdateInvoiceCommand(IInvoiceService invoiceService)
        {
            _invoiceService = invoiceService;
        }

        public async Task ExecuteToNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            await _invoiceService.UpdateInNewDatabase(eventBody, syncId, uniqueIdentifier);
        }

        public async Task ExecuteToOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            await _invoiceService.UpdateInOldDatabase(eventBody, syncId, uniqueIdentifier);
        }
    }
}
