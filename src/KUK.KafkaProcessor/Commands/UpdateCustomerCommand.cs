using KUK.KafkaProcessor.Services.Interfaces;
using Newtonsoft.Json.Linq;

namespace KUK.KafkaProcessor.Commands
{
    public class UpdateCustomerCommand : IEventCommand
    {
        private readonly ICustomerService _customerService;

        public UpdateCustomerCommand(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        public async Task ExecuteToNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            await _customerService.UpdateInNewDatabase(eventBody, syncId, uniqueIdentifier);
        }

        public async Task ExecuteToOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            await _customerService.UpdateInOldDatabase(eventBody, syncId, uniqueIdentifier);
        }
    }
}
