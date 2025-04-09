using Newtonsoft.Json.Linq;

namespace KUK.ChinookSync.Services.Domain.Interfaces
{
    public interface IInvoiceLineService
    {
        Task AddToNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier);
        Task AddToOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier);
        Task UpdateInNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier);
        Task UpdateInOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier);
        Task DeleteFromNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier);
        Task DeleteFromOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier);
    }
}
