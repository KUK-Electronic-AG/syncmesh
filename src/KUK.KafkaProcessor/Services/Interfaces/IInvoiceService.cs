using Newtonsoft.Json.Linq;

namespace KUK.KafkaProcessor.Services.Interfaces
{
    public interface IInvoiceService
    {
        // REMARK: You may add Async keywords to all the methods here
        Task AddToNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier);
        Task AddToOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier);
        Task UpdateInNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier);
        Task UpdateInOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier);
        Task DeleteFromNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier);
        Task DeleteFromOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier);
        Task<bool> MappingExists(int invoiceId);
        Task<bool> MappingExists(Guid invoiceId);
    }

}
