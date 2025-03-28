namespace KUK.KafkaProcessor.EventProcessing
{
    public interface IRetryHelper
    {
        Task ExecuteWithRetryAsync(Func<Task> operation);
        Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation);
        T ExecuteWithRetry<T>(Func<T> operation);
        void ExecuteWithRetry(Action operation);
    }
}
