using KUK.Common.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;

namespace KUK.Common.Services
{
    public class ConnectorsRegistrationService : IConnectorsRegistrationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ConnectorsRegistrationService> _logger;
        private readonly string _firstConnectorFullPath;
        private readonly string _secondConnectorFullPath;
        private readonly IConfiguration _configuration;
        private readonly IUtilitiesService _utilitiesService;
        private readonly IExternalConnectionService _externalConnectionService;

        private readonly string _connectorPath = "http://localhost:8084";

        public ConnectorsRegistrationService(
            HttpClient httpClient,
            ILogger<ConnectorsRegistrationService> logger,
            AppSettingsConfig config,
            IConfiguration configuration,
            IUtilitiesService utilitiesService,
            IExternalConnectionService externalConnectionService)
        {
            _httpClient = httpClient;
            _logger = logger;
            _firstConnectorFullPath = config.FirstConnectorJsonPath;
            _secondConnectorFullPath = config.SecondConnectorJsonPath;
            _configuration = configuration;
            _utilitiesService = utilitiesService;
            _externalConnectionService = externalConnectionService;
        }

        public ConnectorsRegistrationService(
            HttpClient httpClient,
            ILogger<ConnectorsRegistrationService> logger,
            string firstConnectorFullPath,
            string secondConnectorFullPath,
            IConfiguration configuration,
            IUtilitiesService utilitiesService,
            IExternalConnectionService externalConnectionService)
        {
            _httpClient = httpClient;
            _logger = logger;
            _firstConnectorFullPath = firstConnectorFullPath;
            _secondConnectorFullPath = secondConnectorFullPath;
            _configuration = configuration;
            _utilitiesService = utilitiesService;
            _externalConnectionService = externalConnectionService;
        }

        public async Task<ConnectorRegistrationResult> RegisterConnectorAsync(string configPath)
        {
            try
            {
                var mode = _utilitiesService.GetOnlineOrDockerMode();
                if (mode == ApplicationDestinationMode.Docker)
                {
                    if (configPath.StartsWith("old-to-new-connector"))
                    {
                        configPath = _firstConnectorFullPath;
                    }
                    else if (configPath.StartsWith("new-to-old-connector"))
                    {
                        configPath = _secondConnectorFullPath;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Unknown configPath {configPath}");
                    }

                    var configJson = File.ReadAllText(configPath);
                    var content = new StringContent(configJson, Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync(_connectorPath + "/connectors", content);
                    response.EnsureSuccessStatusCode();
                }
                else if (mode == ApplicationDestinationMode.Online)
                {
                    await _externalConnectionService.RegisterConnectorAsync(configPath);
                }
                else
                {
                    throw new InvalidOperationException($"Unknown mode {mode}");
                }

                return new ConnectorRegistrationResult() { Success = true, Message = "Connector registered successfully." };
            }
            catch (Exception ex)
            {
                return new ConnectorRegistrationResult() { Success = false, Message = $"Error registering connector: {ex.Message}" };
            }
        }

        public async Task<string> UnregisterConnectorAsync(string connectorName)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{_connectorPath}/{connectorName}");
                response.EnsureSuccessStatusCode();
                return "Connector unregistered successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error unregistering connector: {ex.Message}");
                throw new InvalidOperationException($"Error unregistering connector: {ex}");
            }
        }

        public async Task<bool> IsConnectorRegistered(string connectorName)
        {
            try
            {
                var mode = _utilitiesService.GetOnlineOrDockerMode();
                List<string> connectors = null;

                if (mode == ApplicationDestinationMode.Online)
                {
                    connectors = await _externalConnectionService.GetListOfRegisteredConnectors();
                }
                else if (mode == ApplicationDestinationMode.Docker)
                {
                    string url = _connectorPath + "/connectors";

                    var response = await _httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    var responseBody = await response.Content.ReadAsStringAsync();
                    connectors = JsonConvert.DeserializeObject<List<string>>(responseBody);
                }
                else
                {
                    throw new InvalidOperationException($"Unknown mode {mode}");
                }

                return connectors.Contains(connectorName);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking if connector is registered: {ex.Message}");
                throw new InvalidOperationException($"Error checking if connector is registered: {ex}");
            }
        }

        public async Task WaitForConnectorInitializationAsync(string connectorName, int maxRetries = 10, int delayMilliseconds = 5000)
        {
            int retryCount = 0;
            while (retryCount < maxRetries)
            {
                if (await IsConnectorRegistered(connectorName))
                {
                    return;
                }
                await Task.Delay(delayMilliseconds);
                retryCount++;
            }
            throw new TimeoutException($"Connector {connectorName} failed to initialize within the expected time.");
        }

        public async Task StopConnectorIfRunning(string connectorName)
        {
            try
            {
                var mode = _utilitiesService.GetOnlineOrDockerMode();
                string status = null;

                if (mode == ApplicationDestinationMode.Docker)
                {
                    // Build URL to get status
                    string statusUrl = $"{_connectorPath}/connectors/{connectorName}/status";
                    var response = await _httpClient.GetAsync(statusUrl);

                    // If connector does not exist (404), let's log it and return
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger.LogInformation($"Connector '{connectorName}' has not been found. Not doing stopping.");
                        return;
                    }
                    response.EnsureSuccessStatusCode();

                    var responseBody = await response.Content.ReadAsStringAsync();
                    // We assume that JSON contains status in the field connector.state
                    dynamic json = JsonConvert.DeserializeObject(responseBody);
                    status = json.connector.state;
                }
                else if (mode == ApplicationDestinationMode.Online)
                {
                    // In Online mode we check connector status and handle situation when it does not exist
                    try
                    {
                        bool isConnectorRegistered = await _externalConnectionService.IsConnectorRegistered(connectorName);

                        if (!isConnectorRegistered)
                        {
                            _logger.LogInformation($"Connector {connectorName} is not registered. Not stopping this connector.");
                            return;
                        }

                        status = await _externalConnectionService.GetConnectorStatusAsync(connectorName);
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message.Contains("NotFound", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation($"Connector '{connectorName}' has not been found. Not doing stopping.");
                            return;
                        }
                        throw;
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Unknown mode: {mode}");
                }

                _logger.LogInformation($"Connector '{connectorName}' status: {status}");

                // If connector is running, we stop it
                if (status.Equals("RUNNING", StringComparison.OrdinalIgnoreCase))
                {
                    if (mode == ApplicationDestinationMode.Docker)
                    {
                        string pauseUrl = $"{_connectorPath}/connectors/{connectorName}/pause";
                        var pauseResponse = await _httpClient.PutAsync(pauseUrl, null);
                        pauseResponse.EnsureSuccessStatusCode();
                        _logger.LogInformation($"Connector '{connectorName}' has been paused in Docker mode..");
                    }
                    else if (mode == ApplicationDestinationMode.Online)
                    {
                        await _externalConnectionService.StopConnectorAsync(connectorName);
                        _logger.LogInformation($"Connector '{connectorName}' has been stopped in Online mode.");
                    }
                    else
                    {
                        throw new InvalidOperationException($"Unknown mode {mode}");
                    }
                }
                else
                {
                    _logger.LogInformation($"Connector '{connectorName}' is not running (status: {status}).");
                }
            }
            catch (Exception ex)
            {
                // If exception is about NotFound, it must have been handled above - otherwise we log error and throw exception
                if (ex.Message.Contains("NotFound", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation($"Connector '{connectorName}' has not been found. Not doing stopping.");
                    return;
                }
                _logger.LogError($"Error stopping the connector '{connectorName}': {ex.Message}");
                throw new InvalidOperationException($"Error stopping connector '{connectorName}': {ex}");
            }
        }

    }
}
