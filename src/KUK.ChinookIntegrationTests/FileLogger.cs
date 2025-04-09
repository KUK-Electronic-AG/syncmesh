using Microsoft.Extensions.Logging;

namespace KUK.ChinookIntegrationTests
{
    public class FileLogger : ILogger
    {
        private readonly string _filePath;
        private readonly object _lock = new object();
        private readonly string _categoryName;
        private readonly LogLevel _minLevel;

        internal static int ErrorsCounter = 0;

        public FileLogger(string filePath, string categoryName, LogLevel minLevel)
        {
            _filePath = filePath;
            _categoryName = categoryName;
            _minLevel = minLevel;
        }

        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            if (formatter == null)
            {
                throw new ArgumentNullException(nameof(formatter));
            }

            var message = formatter(state, exception);
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            var logRecord = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] {_categoryName}: {message}";
            if (exception != null)
            {
                logRecord += Environment.NewLine + exception.ToString();
            }

            if (exception != null || logLevel == LogLevel.Error || logLevel == LogLevel.Critical)
            {
                ErrorsCounter++;
            }

            using (var mutex = new Mutex(false, "Global\\IntegrationTestLogMutex"))
            {
                mutex.WaitOne();
                try
                {
                    File.AppendAllText(_filePath, logRecord + Environment.NewLine);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }

        private class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new NullScope();
            public void Dispose() { }
        }
    }
}
