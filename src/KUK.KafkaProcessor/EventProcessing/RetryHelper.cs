using KUK.KafkaProcessor.Utilities;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace KUK.KafkaProcessor.EventProcessing
{
    public class RetryHelper : IRetryHelper
    {
        private const int RETRY_COUNT = 10;
        private const int RETRY_POWER = 2;

        private readonly GlobalState _globalState;
        private readonly ILogger<RetryHelper> _logger;

        public RetryHelper(GlobalState globalState, ILogger<RetryHelper> logger)
        {
            _globalState = globalState;
            _logger = logger;
        }

        public async Task ExecuteWithRetryAsync(Func<Task> operation)
        {
            // At the startup we assume that retry is not in progress
            _globalState.IsPollyRetrying = false;

            AsyncRetryPolicy retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount: RETRY_COUNT,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(RETRY_POWER, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        // When Polly is doing retry, we set flag to true
                        _globalState.IsPollyRetrying = true;
                        _logger.LogWarning($"Retry {retryCount} due to: {exception.Message} with stack trace {exception.StackTrace}");
                    });

            try
            {
                await retryPolicy.ExecuteAsync(operation);
            }
            finally
            {
                // After operation ends (success or failure) we set flag to false
                _globalState.IsPollyRetrying = false;
            }
        }

        public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation)
        {
            _globalState.IsPollyRetrying = false;

            AsyncRetryPolicy<T> retryPolicy = Policy<T>
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount: RETRY_COUNT,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(RETRY_POWER, retryAttempt)),
                    onRetry: (outcome, timeSpan, retryCount, context) =>
                    {
                        _globalState.IsPollyRetrying = true;
                        var exceptionMessage = outcome.Exception != null ? outcome.Exception.Message : "No exception";
                        var exceptionStackTrace = outcome.Exception != null ? outcome.Exception.StackTrace : "No stack trace";
                        _logger.LogWarning($"Retry {retryCount} due to: {exceptionMessage} with stack trace {exceptionStackTrace}");
                    });

            try
            {
                return await retryPolicy.ExecuteAsync(operation);
            }
            finally
            {
                _globalState.IsPollyRetrying = false;
            }
        }

        public T ExecuteWithRetry<T>(Func<T> operation)
        {
            _globalState.IsPollyRetrying = false;

            var retryPolicy = Policy<T>
                .Handle<Exception>()
                .WaitAndRetry(
                    retryCount: RETRY_COUNT,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(RETRY_POWER, retryAttempt)),
                    onRetry: (outcome, timeSpan, retryCount, context) =>
                    {
                        _globalState.IsPollyRetrying = true;
                        var exceptionMessage = outcome.Exception != null ? outcome.Exception.Message : "No exception";
                        var exceptionStackTrace = outcome.Exception != null ? outcome.Exception.StackTrace : "No stack trace";
                        _logger.LogWarning($"Retry {retryCount} due to: {exceptionMessage} with stack trace {exceptionStackTrace}");
                    });

            try
            {
                return retryPolicy.Execute(operation);
            }
            finally
            {
                _globalState.IsPollyRetrying = false;
            }
        }

        public void ExecuteWithRetry(Action operation)
        {
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetry(
                    retryCount: RETRY_COUNT,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(RETRY_POWER, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning($"Retry {retryCount} due to: {exception.Message} with stack trace {exception.StackTrace}");
                    });

            retryPolicy.Execute(operation);
        }
    }
}
