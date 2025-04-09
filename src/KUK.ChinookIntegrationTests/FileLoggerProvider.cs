using Microsoft.Extensions.Logging;

namespace KUK.ChinookIntegrationTests
{
    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _filePath;
        private readonly LogLevel _minLevel;

        public FileLoggerProvider(string filePath, LogLevel minLevel = LogLevel.Information)
        {
            _filePath = filePath;
            _minLevel = minLevel;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(_filePath, categoryName, _minLevel);
        }

        public void Dispose()
        {
            // If you need to do any cleanup, do it here
        }
    }
}
