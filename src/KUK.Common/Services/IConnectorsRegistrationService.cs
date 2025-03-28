using KUK.Common.Utilities;

namespace KUK.Common.Services
{
    public interface IConnectorsRegistrationService
    {
        Task<ConnectorRegistrationResult> RegisterConnectorAsync(string connectorName);
        Task<string> UnregisterConnectorAsync(string connectorName);
        Task<bool> IsConnectorRegistered(string connectorName);
        Task WaitForConnectorInitializationAsync(string connectorName, int maxRetries = 10, int delayMilliseconds = 5000);
        Task StopConnectorIfRunning(string connectorName);
    }
}
