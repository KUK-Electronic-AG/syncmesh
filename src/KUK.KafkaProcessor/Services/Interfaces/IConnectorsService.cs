using KUK.Common.Services;

namespace KUK.KafkaProcessor.Services.Interfaces
{
    public interface IConnectorsService
    {
        Task ValidateOldConnectorPrerequisites();
        Task RegisterOldConnector();
        Task<bool> WaitForProcessorToFinishInitializationAsync();
        Task RegisterNewConnector();
        Task ValidateNewConnectorPrerequisites();
        Task WaitForBothConnectorsToBeOperational(bool waitForOldConnector, bool waitForNewConnector, ApplicationDestinationMode mode);
    }
}
