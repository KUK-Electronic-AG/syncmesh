using KUK.Common.Services;
using KUK.KafkaProcessor.Services.Interfaces;
using KUK.KafkaProcessor.Utilities;

namespace KUK.KafkaProcessor.Services
{
    public class RunnerService : IRunnerService
    {
        private readonly IDatabaseEventProcessorService _databaseEventProcessorService;
        private readonly IConnectorsService _connectorsService;
        private readonly GlobalState _globalState;

        public RunnerService(
            IDatabaseEventProcessorService databaseEventProcessorService,
            IConnectorsService connectorsService,
            GlobalState globalState)
        {
            _databaseEventProcessorService = databaseEventProcessorService;
            _connectorsService = connectorsService;
            _globalState = globalState;
        }

        public async Task Run()
        {
            var eventProcessorTask = Task.Run(async () =>
            {
                try
                {
                    await _databaseEventProcessorService.RunEventProcessingAsync();
                }
                catch (Exception ex)
                {
                    // TODO: Improve logging
                    Console.WriteLine($"An error occurred: {ex.Message}");
                }
            });

            await eventProcessorTask;
        }

        public async Task CreateConnectorTopics()
        {
            await _connectorsService.ValidateOldConnectorPrerequisites();
            await _connectorsService.ValidateNewConnectorPrerequisites();
        }

        public async Task RegisterOldConnector()
        {
            await _connectorsService.RegisterOldConnector();
        }

        public bool SnapshotLastReceived()
        {
            return _globalState.SnapshotLastReceived;
        }

        public async Task WaitForProcessorToFinishInitializationAsync()
        {
            await _connectorsService.WaitForProcessorToFinishInitializationAsync();
        }

        public async Task RegisterNewConnector()
        {
            await _connectorsService.ValidateNewConnectorPrerequisites();
            await _connectorsService.RegisterNewConnector();
        }

        public async Task WaitForBothConnectorsToBeOperational(bool waitForOldConnector, bool waitForNewConnector, ApplicationDestinationMode mode)
        {
            await _connectorsService.WaitForBothConnectorsToBeOperational(waitForOldConnector, waitForNewConnector, mode);
        }
    }
}
