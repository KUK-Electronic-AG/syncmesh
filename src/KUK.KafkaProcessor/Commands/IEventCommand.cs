using Newtonsoft.Json.Linq;

namespace KUK.KafkaProcessor.Commands
{
    public interface IEventCommand
    {
        Task ExecuteToNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier);
        Task ExecuteToOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier);
    }
}
