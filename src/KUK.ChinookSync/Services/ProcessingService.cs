using KUK.Common.Services;
using KUK.KafkaProcessor.Services.Interfaces;

namespace KUK.ChinookSync.Services
{
    public class ProcessingService : IProcessingService
    {
        private ILogger<ProcessingService> _logger;
        private IInitializationService _initializationService;
        private IRunnerService _runnerService;

        public ProcessingService(
            ILogger<ProcessingService> logger,
            IInitializationService initializationService,
            IRunnerService runnerService)
        {
            _logger = logger;
            _initializationService = initializationService;
            _runnerService = runnerService;
        }

        public async Task Process()
        {
            // Initialize new database
            // REMARK: Ideally, when this method is in progress, there should be change freeze on production.
            // The reason is that if something is added after the connector is registered and before migration is finished,
            // it may lead to new inserts being added twice (first time from db initialization, second time from Kafka Debezium event).
            bool databaseInitializationResult = await _initializationService.InitializeNewDatabase();
            if (!databaseInitializationResult)
            {
                var errorMessage = $"Failure initializing new database. Going to return.";
                _logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            bool triggersCreationResult = await _initializationService.CreateTriggersInNewDatabase();
            if (!triggersCreationResult)
            {
                var errorMessage = $"Failure creating triggers in new database. Going to return.";
                _logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            await _runnerService.Run();
        }

        public async Task CreateConnectorTopics()
        {
            await _runnerService.CreateConnectorTopics();
        }

        public async Task RegisterOldConnector()
        {
            await _runnerService.RegisterOldConnector();
        }

        public async Task WaitForProcessorToFinishInitializationAsync()
        {
            await _runnerService.WaitForProcessorToFinishInitializationAsync();
        }

        public async Task RegisterNewConnector()
        {
            await _runnerService.RegisterNewConnector();
        }

        public async Task WaitForBothConnectorsToBeOperational(bool waitForOldConnector, bool waitForNewConnector, ApplicationDestinationMode mode)
        {
            await _runnerService.WaitForBothConnectorsToBeOperational(waitForOldConnector, waitForNewConnector, mode);
        }
    }
}
