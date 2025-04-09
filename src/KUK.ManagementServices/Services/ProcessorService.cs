using System.Diagnostics;
using KUK.Common;
using KUK.ManagementServices.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace KUK.ManagementServices.Services
{
    public class ProcessorService : IProcessorService
    {
        private readonly ILogger<ProcessorService> _logger;
        private Process? _processor;
        private readonly string _processorExeFullPath;
        private readonly string _processorAppSettingsFullPath;
        private readonly string _processorExePath;
        private readonly string _processorLogsFullPath;
        private readonly AppSettingsConfig _appSettingsConfig;
        private readonly HttpClient _httpClient;

        public ProcessorService(
            ILogger<ProcessorService> logger,
            AppSettingsConfig config,
            HttpClient httpClient)
        {
            _logger = logger;

            _appSettingsConfig = config;
            _processorExeFullPath = config.ProcessorExeFullPath;
            _processorAppSettingsFullPath = config.ProcessorAppSettingsFullPath;
            _processorExePath = config.ProcessorExePath;
            _processorLogsFullPath = config.ProcessorLogsFullPath;
            _httpClient = httpClient;
        }

        public async Task<bool> DeleteProcessorLogs()
        {
            try
            {
                var logsLocation = _processorLogsFullPath;
                if (string.IsNullOrWhiteSpace(logsLocation))
                {
                    throw new InvalidOperationException($"ProcessorLogsFullPath is empty, cannot delete the logs");
                }
                File.Delete(logsLocation);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Cannot delete processor logs due to unhandled exception {ex}");
                return false;
            }
            return true;
        }

        public async Task<bool> AreProcessorLogsFreeFromErrors(string logsPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(logsPath))
                {
                    throw new InvalidOperationException($"ProcessorLogsFullPath is empty, cannot read the logs");
                }

                List<string> errorLogs = new List<string>();

                using (StreamReader reader = new StreamReader(logsPath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (/*line.Contains("[ERR]") || */line.Contains("[FATAL]") || line.Contains("[FTL]"))
                        {
                            errorLogs.Add(line);
                        }
                    }
                }

                foreach (string log in errorLogs)
                {
                    _logger.LogInformation(log);
                }

                return !errorLogs.Any();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Cannot read processor logs due to unhandled exception {ex}");
                return false;
            }
        }

        public async Task<bool> StartProcessorAsync()
        {
            try
            {
                if (ShouldSpawnNewProcess())
                {
                    _processor = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = _processorExeFullPath,
                            Arguments = $"--appsettings \"{_processorAppSettingsFullPath}\"",
                            UseShellExecute = true,
                            CreateNoWindow = false,
                            WorkingDirectory = _processorExePath // required to correctly read appsettings from KafkaProcessor project
                        }
                    };
                    _processor.Start();
                }
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error starting processor: {ex.Message}");
                return await Task.FromResult(false);
            }
        }

        public async Task<bool> KillProcessorAsync()
        {
            try
            {
                string processName = System.IO.Path.GetFileNameWithoutExtension(_processorExeFullPath);
                var processes = Process.GetProcessesByName(processName);

                foreach (var process in processes)
                {
                    process.Kill();
                    process.WaitForExit();
                }

                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error killing processor: {ex.Message}");
                return await Task.FromResult(false);
            }
        }

        private bool ShouldSpawnNewProcess()
        {
            // REMARK: There may be better way than catching exception

            try
            {
                if (_processor == null || _processor.HasExited)
                {
                    return true;
                }
            }
            catch (InvalidOperationException)
            {
                return true;
            }
            throw new InvalidOperationException("Cannot check if we should spawn new process");
        }
    }
}
