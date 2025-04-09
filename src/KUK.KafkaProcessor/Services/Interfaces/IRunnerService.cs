using KUK.Common.Services;

namespace KUK.KafkaProcessor.Services.Interfaces
{
    public interface IRunnerService
    {
        Task Run();
        Task CreateConnectorTopics();
        Task RegisterOldConnector();
        Task WaitForProcessorToFinishInitializationAsync();
        bool SnapshotLastReceived();
        Task RegisterNewConnector();
        Task WaitForBothConnectorsToBeOperational(bool waitForOldConnector, bool waitForNewConnector, ApplicationDestinationMode mode);
    }
}
