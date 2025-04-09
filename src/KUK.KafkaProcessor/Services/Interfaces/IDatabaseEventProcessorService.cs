namespace KUK.KafkaProcessor.Services.Interfaces
{
    public interface IDatabaseEventProcessorService
    {
        Task RunEventProcessingAsync();
    }
}
