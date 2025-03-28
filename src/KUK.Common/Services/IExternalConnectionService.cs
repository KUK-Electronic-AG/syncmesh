namespace KUK.Common.Services
{
    public interface IExternalConnectionService
    {
        Task<List<string>> GetListOfRegisteredConnectors();
        Task CreatePublicationForAllTables();
        Task RegisterConnectorAsync(string configPath);
        Task DeleteConnectorAsync(string connectorName);
        Task DeleteConnectorsAsync(string connectorNamePrefix);
        Task RestartConnectorAsync(string connectorName);
        Task<string> GetConnectorStatusAsync(string connectorName);
        Task StopConnectorAsync(string connectorName);
        Task<bool> IsConnectorRegistered(string connectorName);
    }
}
