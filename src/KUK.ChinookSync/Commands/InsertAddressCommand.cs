using KUK.ChinookSync.Services.Domain.Interfaces;
using KUK.KafkaProcessor.Commands;
using Newtonsoft.Json.Linq;

namespace KUK.ChinookSync.Commands
{
    public class InsertAddressCommand : IEventCommand
    {
        private readonly IAddressService _addressService;

        public InsertAddressCommand(IAddressService addressService)
        {
            _addressService = addressService;
        }

        public async Task ExecuteToNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            await _addressService.AddToNewDatabase(eventBody, syncId, uniqueIdentifier);
        }

        public async Task ExecuteToOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            await _addressService.AddToOldDatabase(eventBody, syncId, uniqueIdentifier);
        }
    }
}
