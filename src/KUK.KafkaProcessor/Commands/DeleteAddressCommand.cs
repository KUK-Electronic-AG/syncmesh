using KUK.KafkaProcessor.Services.Interfaces;
using Newtonsoft.Json.Linq;

namespace KUK.KafkaProcessor.Commands
{
    public class DeleteAddressCommand : IEventCommand
    {
        private readonly IAddressService _addressService;

        public DeleteAddressCommand(IAddressService addressService)
        {
            _addressService = addressService;
        }

        public Task ExecuteToNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            throw new NotImplementedException();
        }

        public Task ExecuteToOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            throw new NotImplementedException();
        }
    }
}
