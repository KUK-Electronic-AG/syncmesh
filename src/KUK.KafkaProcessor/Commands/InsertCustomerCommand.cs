using KUK.KafkaProcessor.Services.Interfaces;
using Newtonsoft.Json.Linq;

namespace KUK.KafkaProcessor.Commands
{
    public class InsertCustomerCommand : IEventCommand
    {
        private readonly ICustomerService _customerService;

        public InsertCustomerCommand(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        public async Task ExecuteToNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            await _customerService.AddToNewDatabase(eventBody, syncId, uniqueIdentifier);
        }

        public async Task ExecuteToOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            await _customerService.AddToOldDatabase(eventBody, syncId, uniqueIdentifier);
        }
    }
}
