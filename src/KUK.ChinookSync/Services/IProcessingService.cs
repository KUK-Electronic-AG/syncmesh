using KUK.Common.Services;

namespace KUK.ChinookSync.Services
{
    public interface IProcessingService
    {
        Task CreateConnectorTopics();
        Task RegisterOldConnector();
        Task WaitForProcessorToFinishInitializationAsync();
        Task Process();
        Task RegisterNewConnector();
        Task WaitForBothConnectorsToBeOperational(bool waitForOldConnector, bool waitForNewConnector, ApplicationDestinationMode mode);
    }
}
