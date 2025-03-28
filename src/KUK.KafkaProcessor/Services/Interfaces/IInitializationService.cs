namespace KUK.KafkaProcessor.Services.Interfaces
{
    public interface IInitializationService
    {
        Task<bool> InitializeNewDatabase();
        Task<bool> CreateTriggersInNewDatabase();
    }
}
