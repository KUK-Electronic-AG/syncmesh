using KUK.KafkaProcessor.Services.Interfaces;
using Newtonsoft.Json.Linq;

namespace KUK.KafkaProcessor.Commands
{
    public class DeleteCustomerCommand : IEventCommand
    {
        private readonly ICustomerService _customerService;

        public DeleteCustomerCommand(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        public async Task ExecuteToNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            await _customerService.DeleteFromNewDatabase(eventBody, syncId, uniqueIdentifier);
        }

        public async Task ExecuteToOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            await _customerService.DeleteFromOldDatabase(eventBody, syncId, uniqueIdentifier);
        }
    }
}
