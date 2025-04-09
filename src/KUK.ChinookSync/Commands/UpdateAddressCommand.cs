using KUK.ChinookSync.Services.Domain.Interfaces;
using KUK.KafkaProcessor.Commands;
using Newtonsoft.Json.Linq;

namespace KUK.ChinookSync.Commands
{
    public class UpdateAddressCommand : IEventCommand
    {
        private readonly IAddressService _addressService;

        public UpdateAddressCommand(IAddressService addressService)
        {
            _addressService = addressService;
        }

        public async Task ExecuteToNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            await _addressService.UpdateInNewDatabase(eventBody, syncId, uniqueIdentifier);
        }

        public async Task ExecuteToOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            await _addressService.UpdateInOldDatabase(eventBody, syncId, uniqueIdentifier);
        }
    }
}
