using KUK.Common.Contexts;

namespace KUK.KafkaProcessor.Services.Interfaces
{
    public interface IUniqueIdentifiersService
    {
        bool IdentifierExists(string identifier);
        Task MarkIdentifierAsProcessedInOldDatabase(string identifier, Chinook1DataChangesContext oldContext);
        Task MarkIdentifierAsProcessedInNewDatabase(string identifier, Chinook2Context newContext);
    }
}
