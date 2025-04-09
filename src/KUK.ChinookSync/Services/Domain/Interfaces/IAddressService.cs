using Newtonsoft.Json.Linq;

namespace KUK.ChinookSync.Services.Domain.Interfaces
{
    public interface IAddressService
    {
        Task AddToNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier);
        Task AddToOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier);
        Task UpdateInNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier);
        Task UpdateInOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier);
        Task DeleteFromNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier);
        Task DeleteFromOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier);
        Task<bool> MappingExists(int addressId);
        Task<bool> MappingExists(Guid addressId);
    }
}
