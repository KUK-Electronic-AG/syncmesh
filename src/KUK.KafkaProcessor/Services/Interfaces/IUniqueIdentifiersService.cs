using KUK.Common.Contexts;

namespace KUK.KafkaProcessor.Services.Interfaces
{
    public interface IUniqueIdentifiersService
    {
        bool IdentifierExists(string identifier);
        Task MarkIdentifierAsProcessedInOldDatabase(string identifier, IOldDataChangesContext oldContext);
        Task MarkIdentifierAsProcessedInNewDatabase(string identifier, NewDbContext newContext);
    }
}
