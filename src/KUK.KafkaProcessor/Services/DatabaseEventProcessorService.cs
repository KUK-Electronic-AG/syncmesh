using Confluent.Kafka;
using KUK.Common.Services;
using KUK.KafkaProcessor.Commands;
using KUK.KafkaProcessor.EventProcessing;
using KUK.KafkaProcessor.Services.Interfaces;
using KUK.KafkaProcessor.Utilities;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KUK.KafkaProcessor.Services
{
    public class DatabaseEventProcessorService : IDatabaseEventProcessorService
    {
        private readonly IConfiguration _configuration;
        private readonly IEventCommandFactory _commandFactory;
        private readonly ILogger<DatabaseEventProcessorService> _logger;
        private readonly IDocumentStore _martenStore;
        private readonly IKafkaService _kafkaService;
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly IUniqueIdentifiersService _uniqueIdentifiersService;
        private readonly IRetryHelper _retryHelper;
        private readonly IUtilitiesService _utilitiesService;
        private readonly IEventsSortingService _eventsSortingService;
        private readonly GlobalState _globalState;

        private IConsumer<Ignore, string> _consumerOld;
        private IConsumer<Ignore, string> _consumerNew;
        private IConsumer<Ignore, string> _eventConsumer;
        private IConsumer<Ignore, string> _bufferConsumer;

        private List<List<PriorityDependency>> _priorityLists;

        internal List<EventMessage> DeferredKafkaEvents { get; set; } = new List<EventMessage>();

        private const string OLD_TO_NEW_SOURCE = "old_to_new";
        private const string NEW_TO_OLD_SOURCE = "new_to_old";

        public DatabaseEventProcessorService(
            IConfiguration configuration,
            ILogger<DatabaseEventProcessorService> logger,
            IEventCommandFactory commandFactory,
            IDocumentStore store,
            IKafkaService kafkaService,
            IHostApplicationLifetime applicationLifetime,
            IUniqueIdentifiersService uniqueIdentifiersService,
            IRetryHelper retryHelper,
            IUtilitiesService utilitiesService,
            IEventsSortingService eventsSortingService,
            GlobalState globalState
            )
        {
            _configuration = configuration;
            _logger = logger;
            _commandFactory = commandFactory;
            _martenStore = store;
            _kafkaService = kafkaService;
            _applicationLifetime = applicationLifetime;
            _uniqueIdentifiersService = uniqueIdentifiersService;
            _retryHelper = retryHelper;
            _utilitiesService = utilitiesService;
            _eventsSortingService = eventsSortingService;

            _applicationLifetime.ApplicationStopping.Register(OnStopping);
            _globalState = globalState;

            _priorityLists = new List<List<PriorityDependency>>
            {
                // Invoice -> InvoiceLine: InvoiceLine depends on Invoice, ID field is "InvoiceId"
                new List<PriorityDependency> { 
                    new PriorityDependency("Invoice", "InvoiceId"), 
                    new PriorityDependency("InvoiceLine", "InvoiceId") 
                },
                // Customer -> Invoice: Invoice depends on Customer, ID field is "CustomerId"
                new List<PriorityDependency> { 
                    new PriorityDependency("Customer", "CustomerId"), 
                    new PriorityDependency("Invoice", "CustomerId") 
                },
                // Address -> Customer: Customer depends on Address, ID field is "AddressId"
                new List<PriorityDependency> {
                    new PriorityDependency("Address", "AddressId"),
                    new PriorityDependency("Customer", "AddressId")
                }
            };
        }

        public async Task RunEventProcessingAsync()
        {
            _logger.LogDebug($"Started method {nameof(RunEventProcessingAsync)}");

            ValidateAppSettingsConfiguration();
            await ValidatePresenceOfTopicConfiguration();

            await ValidateDebeziumTopics();

            // Kafka configuration
            var config = new ConsumerConfig
            {
                GroupId = _configuration["Kafka:GroupId"],
                BootstrapServers = _configuration["Kafka:BootstrapServers"],
                AutoOffsetReset = AutoOffsetReset.Earliest
            };

            var mode = _utilitiesService.GetOnlineOrDockerMode();
            if (mode == ApplicationDestinationMode.Online)
            {
                await ThrowIfConfigurationKeyNotPresent("OnlineMode:SslCaLocation");
                await ThrowIfConfigurationKeyNotPresent("OnlineMode:SslCertificateLocation");
                await ThrowIfConfigurationKeyNotPresent("OnlineMode:SslKeyLocation");

                config.SecurityProtocol = SecurityProtocol.Ssl;
                config.SslCaLocation = _configuration["OnlineMode:SslCaLocation"];
                config.SslCertificateLocation = _configuration["OnlineMode:SslCertificateLocation"];
                config.SslKeyLocation = _configuration["OnlineMode:SslKeyLocation"];
            }

            // Creating Kafka client
            _consumerOld = new ConsumerBuilder<Ignore, string>(config).Build();
            _consumerNew = new ConsumerBuilder<Ignore, string>(config).Build();
            _consumerOld.Subscribe(_configuration["Kafka:OldToNewTopic"]);
            _consumerNew.Subscribe(_configuration["Kafka:NewToOldTopic"]);

            // Creating event sourcing producer and consumer
            var eventQueueTopic = _configuration["Kafka:EventQueueTopic"];
            await _kafkaService.CreateTopicAsync(eventQueueTopic, 1, 3);
            using IProducer<string, string> producer = new ProducerBuilder<string, string>(config).Build();
            _eventConsumer = new ConsumerBuilder<Ignore, string>(config).Build();
            _eventConsumer.Subscribe(eventQueueTopic);

            var bufferTopic = _configuration["Kafka:BufferTopic"];
            await _kafkaService.CreateTopicAsync(bufferTopic, 1, 3);
            _bufferConsumer = new ConsumerBuilder<Ignore, string>(config).Build();
            _bufferConsumer.Subscribe(bufferTopic);

            var cts = new CancellationTokenSource();

            // Start processing
            await StartConsumersAsync(cts.Token, _consumerOld, _consumerNew, _bufferConsumer, producer);
            _globalState.IsInitialized = true;
            _logger.LogInformation("Marked KafkaProcessor as initialized");
            await ProcessDebeziumEventsAsync(_eventConsumer, _martenStore, cts.Token);

            // Wait for process to end by stopping on Azure or user pressing Ctrl+C
            _logger.LogInformation("Event processing started. Press Ctrl+C to stop.");

            // Wait for the application to stop
            await Task.Delay(Timeout.Infinite, cts.Token);
        }

        private async Task ValidatePresenceOfTopicConfiguration()
        {
            await ThrowIfConfigurationKeyNotPresent("Kafka:OldToNewTopic");
            await ThrowIfConfigurationKeyNotPresent("Kafka:SchemaChangesOldTopic");
            await ThrowIfConfigurationKeyNotPresent("Kafka:DbHistoryOldTopic");

            await ThrowIfConfigurationKeyNotPresent("Kafka:NewToOldTopic");
            await ThrowIfConfigurationKeyNotPresent("Kafka:SchemaChangesNewTopic");
            await ThrowIfConfigurationKeyNotPresent("Kafka:DbHistoryNewTopic");
        }

        private async Task ThrowIfConfigurationKeyNotPresent(string topicKey)
        {
            var topic = _configuration[topicKey];
            if (string.IsNullOrWhiteSpace(topic))
            {
                throw new InvalidOperationException($"Value of {topicKey} is not set in appsettings.json");
            }
        }



        

        

        private async Task StartConsumersAsync(
            CancellationToken cancellationToken,
            IConsumer<Ignore, string> consumerOld,
            IConsumer<Ignore, string> consumerNew,
            IConsumer<Ignore, string> consumerBuffer,
            IProducer<string, string> producer
        )
        {
            // Consumer task for old topic
            var taskOld = Task.Run(async () =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        ConsumeResult<Ignore, string> consumeResult = _retryHelper.ExecuteWithRetry(() => consumerOld.Consume(cancellationToken));
                        var eventMsg = await _eventsSortingService.ProcessMessageToEventMessageAsync(consumeResult);
                        string aggregateId = _eventsSortingService.ExtractAggregateId(eventMsg.Payload);

                        await producer.ProduceAsync(_configuration["Kafka:BufferTopic"], new Message<string, string>
                        {
                            Key = aggregateId,
                            Value = eventMsg.Payload
                        });

                        _logger.LogInformation($"Consumed message from old topic at: '{consumeResult.TopicPartitionOffset}' with aggregate_id: {aggregateId}.");
                        _retryHelper.ExecuteWithRetry(() => consumerOld.Commit(consumeResult));
                    }
                }
                catch (ConsumeException e)
                {
                    _logger.LogError("Error in consumerOld: " + e.Message);
                    ProcessErrorHandler(e.Error);
                }
            }, cancellationToken);

            // Consumer task for new topic
            var taskNew = Task.Run(async () =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        ConsumeResult<Ignore, string> consumeResult = _retryHelper.ExecuteWithRetry(() => consumerNew.Consume(cancellationToken));
                        var eventMsg = await _eventsSortingService.ProcessMessageToEventMessageAsync(consumeResult);
                        string aggregateId = _eventsSortingService.ExtractAggregateId(eventMsg.Payload);

                        await producer.ProduceAsync(_configuration["Kafka:BufferTopic"], new Message<string, string>
                        {
                            Key = aggregateId,
                            Value = eventMsg.Payload
                        });

                        _logger.LogInformation($"Consumed message from new topic at: '{consumeResult.TopicPartitionOffset}' with aggregate_id: {aggregateId}. Payload: {eventMsg.Payload}");
                        _retryHelper.ExecuteWithRetry(() => consumerNew.Commit(consumeResult));
                    }
                }
                catch (ConsumeException e)
                {
                    _logger.LogError("Error in consumerNew: " + e.Message);
                    ProcessErrorHandler(e.Error);
                }
            }, cancellationToken);

            var pendingEvents = new List<EventMessage>();
            var pendingConsumedResults = new List<ConsumeResult<Ignore, string>>();
            int bufferId = 0;

            var flushTask = Task.Run(async () =>
            {
                await StartFlushCycleAsync(consumerBuffer, cancellationToken, producer);
            }, cancellationToken);
        }

        private async Task StartFlushCycleAsync(
            IConsumer<Ignore, string> consumerBuffer,
            CancellationToken cancellationToken,
            IProducer<string, string> producer)
        {
            try
            {
                // Get configuration parameters
                double delayMs = Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceDelayInMilliseconds"]);
                double collectionMs = Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:BufferEventsCollectionInMilliseconds"]);
                double waitingTimeMs = Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:BufferEventsWaitingTimeInMilliseconds"]);
                double findingDepMaxAttempts = Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:FindingDependenciesMaxAttempts"]);
                double findingDepWaitMs = Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:FindingDependenciesWaitingTimeInMilliseconds"]);

                // Global buffers
                var pendingEvents = new List<EventMessage>();
                var pendingConsumedResults = new List<ConsumeResult<Ignore, string>>();
                int bufferId = 0;

                while (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Flush cycle started at {StartTime}.", DateTime.UtcNow);

                    // We capture new events
                    var (newEvents, newConsumedResults) = await CollectNewEventsAsync(consumerBuffer, collectionMs, waitingTimeMs, cancellationToken);
                    _logger.LogInformation("New events collected: {Count}.", newEvents.Count);

                    if (DeferredKafkaEvents.Any())
                    {
                        newEvents.InsertRange(0, DeferredKafkaEvents);
                        DeferredKafkaEvents = new List<EventMessage>();
                    }

                    // We add to global buffers
                    pendingEvents.AddRange(newEvents);
                    pendingConsumedResults.AddRange(newConsumedResults);
                    LogPendingEvents(pendingEvents, "Before EnsureDependenciesAsync");

                    // We check dependencies - this method may modify list of events
                    pendingEvents = await _eventsSortingService.EnsureDependenciesAsync(
                        pendingEvents, _priorityLists, consumerBuffer, pendingConsumedResults, DeferredKafkaEvents, cancellationToken);
                    _logger.LogDebug("After EnsureDependenciesAsync, pending events count: {Count}.", pendingEvents.Count);

                    LogPendingEvents(pendingEvents, "After EnsureDependenciesAsync");

                    if (pendingEvents.Any())
                    {
                        var sortedEvents = _eventsSortingService.SortEvents(pendingEvents, _priorityLists, bufferId);
                        _logger.LogDebug("Sorted events count: {Count} (bufferId={BufferId}).", sortedEvents.Count, bufferId);
                        bufferId++;
                        
                        bool allProduced = true;
                        // We process and produce each event
                        foreach (var evt in sortedEvents)
                        {
                            bool processed = await ProcessAndProduceEventAsync(evt, producer);
                            if (!processed)
                            {
                                allProduced = false;
                                break;
                            }
                        }

                        if (allProduced)
                        {
                            _logger.LogInformation("All events produced successfully. Committing offsets for {Count} consumed messages.", pendingConsumedResults.Count);
                            consumerBuffer.Commit(pendingConsumedResults.Select(result => result.TopicPartitionOffset));
                            pendingEvents.Clear();
                            pendingConsumedResults.Clear();
                            _logger.LogDebug("Global buffers cleared after commit.");
                        }
                        else
                        {
                            _logger.LogWarning("Not all events were produced successfully. Offsets will not be committed in this cycle.");
                        }
                    }
                    else
                    {
                        _logger.LogInformation("No pending events to process in this flush cycle.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical("Flush task exception: {Exception}", ex);
                throw;
            }
            finally
            {
                consumerBuffer.Close();
            }
        }

        /// <summary>
        /// Acquires new events for some  period of time
        /// </summary>
        private async Task<(List<EventMessage> newEvents, List<ConsumeResult<Ignore, string>> newConsumedResults)>
            CollectNewEventsAsync(IConsumer<Ignore, string> consumerBuffer, double collectionMs, double waitingTimeMs, CancellationToken cancellationToken)
        {
            var newEvents = new List<EventMessage>();
            var newConsumedResults = new List<ConsumeResult<Ignore, string>>();
            var startTime = DateTime.UtcNow;

            while (DateTime.UtcNow - startTime < TimeSpan.FromMilliseconds(collectionMs))
            {
                var consumeResult = consumerBuffer.Consume(TimeSpan.FromMilliseconds(waitingTimeMs));
                if (consumeResult != null)
                {
                    newConsumedResults.Add(consumeResult);
                    var eventMsg = await _eventsSortingService.ProcessMessageToEventMessageAsync(consumeResult);
                    newEvents.Add(eventMsg);
                    _logger.LogDebug("New event collected: {EventMessage}", eventMsg);
                }
            }
            return (newEvents, newConsumedResults);
        }

        /// <summary>
        /// It logs content of global events buffer.
        /// </summary>
        private void LogPendingEvents(List<EventMessage> pendingEvents, string additionalComment)
        {
            _logger.LogDebug("Global pending events count after adding new events ({AdditionalComment}): {Count}. Details: {Details}",
                additionalComment,
                pendingEvents.Count,
                string.Join(", ", pendingEvents.Select(e =>
                    $"{_eventsSortingService.ExtractAggregateId(e.Payload)}:{_eventsSortingService.ExtractEventType(e.Payload)}")));
        }

        private async Task<bool> ProcessAndProduceEventAsync(
            EventMessage evt,
            IProducer<string, string> producer)
        {
            string aggregateId = _eventsSortingService.ExtractAggregateId(evt.Payload);
            string eventType = _eventsSortingService.ExtractEventType(evt.Payload);

            _logger.LogDebug("Producing event: Key={Key}, EventType={EventType}", aggregateId, eventType);
            try
            {
                await producer.ProduceAsync(_configuration["Kafka:EventQueueTopic"], new Message<string, string>
                {
                    Key = aggregateId,
                    Value = evt.Payload
                });
                _logger.LogDebug("Event produced with aggregate_id: {AggregateId}, timestamp: {Timestamp}, eventType={EventType}",
                    aggregateId, evt.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fffff"), eventType);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending event to EventQueueTopic for aggregate_id {AggregateId}", aggregateId);
                return false;
            }
        }

        private void ProcessErrorHandler(Error error)
        {
            // REMARK: Here we need to think about the best way of handling errors
            _logger.LogError($"Error: {error.Reason}");
        }

        private async Task ProcessDebeziumEventsAsync(
            IConsumer<Ignore, string> eventConsumer,
            IDocumentStore store,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Started watching for eventQueue events");

                while (!cancellationToken.IsCancellationRequested)
                {
                    // Consume event with retry logic.
                    var consumeResult = _retryHelper.ExecuteWithRetry(() => eventConsumer.Consume());
                    var eventBody = consumeResult.Message.Value;
                    _logger.LogDebug("Raw event body: {EventBody}", eventBody);

                    // Parse the JSON payload.
                    var jsonDocument = JObject.Parse(eventBody);

                    // Extract and log key properties.
                    string aggregateId = jsonDocument["aggregate_id"]?.ToString();
                    string aggregateType = jsonDocument["aggregate_type"]?.ToString();
                    string eventType = jsonDocument["event_type"]?.ToString();
                    _logger.LogDebug("Extracted event properties: aggregate_id='{AggregateId}', aggregate_type='{AggregateType}', event_type='{EventType}'",
                        aggregateId, aggregateType, eventType);

                    // Check for snapshot events.
                    var snapshotValue = jsonDocument["source"]?["snapshot"]?.ToString();
                    if (snapshotValue == "true" || snapshotValue == "last")
                    {
                        _logger.LogInformation("Received snapshot event. snapshot={SnapshotValue}. Timestamp: {Timestamp}", snapshotValue, DateTime.Now);
                        if (snapshotValue == "last")
                        {
                            _globalState.SnapshotLastReceived = true;
                        }
                        _retryHelper.ExecuteWithRetry(() => eventConsumer.Commit(consumeResult));
                        continue;
                    }

                    _logger.LogDebug("Processing event with body: {EventBody}", eventBody);

                    // Extract operation and log.
                    var opElement = jsonDocument["__op"];
                    if (opElement != null)
                    {
                        var op = opElement.ToString();
                        _logger.LogDebug("Operation type: {Op}", op);

                        if (op == "r")
                        {
                            _logger.LogDebug("Ignoring read operation");
                            continue;
                        }

                        // Extract source and generate syncId.
                        var source = jsonDocument["__source_name"]?.ToString();
                        _logger.LogDebug("Source: {Source}", source);
                        var syncId = Guid.NewGuid().ToString();
                        _logger.LogDebug("Generated syncId: {SyncId}", syncId);

                        // Log before saving to Postgres.
                        _logger.LogDebug("Saving event to Postgres with syncId: {SyncId}", syncId);
                        await _retryHelper.ExecuteWithRetryAsync(async () =>
                        {
                            using (var session = store.LightweightSession())
                            {
                                session.Store(new MartenEventData
                                {
                                    Source = source,
                                    SyncId = syncId,
                                    Data = eventBody
                                });
                                await session.SaveChangesAsync();
                            }
                        });

                        // Log before synchronizing with MySQL.
                        _logger.LogDebug("Synchronizing event with MySQL for syncId: {SyncId}", syncId);
                        await SynchronizeWithMySQL(jsonDocument, source, syncId);
                    }

                    // Confirmation of message receipt.
                    _retryHelper.ExecuteWithRetry(() => eventConsumer.Commit(consumeResult));
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Event processing was cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogCritical("Unhandled exception in ProcessDebeziumEventsAsync: {Exception}", ex);
                throw;
            }
        }

        private async Task SynchronizeWithMySQL(JObject eventBody, string source, string syncId)
        {
            try
            {
                // Get operation type
                var eventType = eventBody["event_type"];
                string op = GetOperation(eventBody, eventType);

                var table = eventBody["__source_table"];
                var uniqueIdentifier = eventBody["unique_identifier"];
                var payload = ConvertPayloadToJObject(eventBody["payload"]);

                _logger.LogDebug($"New event: op={op}, sourceTable={table}");

                if (_retryHelper.ExecuteWithRetry(() => _uniqueIdentifiersService.IdentifierExists(uniqueIdentifier.ToString())))
                {
                    _logger.LogDebug($"Message with ID {uniqueIdentifier.ToString()} has already been processed. Skipping.");
                    return;
                }

                IEventCommand command = _commandFactory.GetCommand(source, table.ToString(), op.ToString());
                if (command != null)
                {
                    if (source == OLD_TO_NEW_SOURCE)
                    {
                        _logger.LogDebug("Starting ExecuteToNewDatabase");
                        await _retryHelper.ExecuteWithRetryAsync(() => command.ExecuteToNewDatabase(payload, syncId, uniqueIdentifier.ToString()));
                        _logger.LogDebug($"Marked identifier {uniqueIdentifier.ToString()} as processed for ExecuteToNewDatabase");
                    }
                    else if (source == NEW_TO_OLD_SOURCE)
                    {
                        _logger.LogDebug("Starting ExecuteToOldDatabase");
                        await _retryHelper.ExecuteWithRetryAsync(() => command.ExecuteToOldDatabase(payload, syncId, uniqueIdentifier.ToString()));
                        _logger.LogDebug($"Marked identifier {uniqueIdentifier.ToString()} as processed for ExecuteToOldDatabase");
                    }
                    else
                    {
                        throw new InvalidOperationException($"Unknown source {source}");
                    }
                }
                else
                {
                    _logger.LogError($"Unknown command for source={source}, table={table}, event_type={eventType}");
                }
            }
            catch (Exception ex)
            {
                // REMARK: Here we need to think about the best way of handling errors
                _logger.LogCritical($"Unhandled exception synchronizing with MySQL: {ex}");
                throw;
            }
        }

        private string GetOperation(JObject eventBody, JToken? eventType)
        {
            var opType = eventBody["__op"];
            var opByEventType = string.Empty;
            var opByOpType = string.Empty;
            var op = string.Empty;
            if (eventType != null && !string.IsNullOrWhiteSpace(eventType.ToString()))
            {
                opByEventType = _eventsSortingService.GetOpBasedOnEventType(eventType.ToString());
            }
            if (!string.IsNullOrWhiteSpace(opType.ToString()))
            {
                opByOpType = _eventsSortingService.GetOpBasedOnEventType(opType.ToString());
            }
            if (!string.IsNullOrEmpty(opByEventType) && !string.IsNullOrEmpty(opByOpType) && opByEventType != opByOpType)
            {
                _logger.LogDebug($"Received op type in inconsistent way. {opByEventType} & {opByOpType}. Trusting eventType rather than op.");
                op = opByEventType;
            }
            else
            {
                if (!string.IsNullOrEmpty(opByEventType))
                {
                    op = opByEventType;
                }
                if (!string.IsNullOrEmpty(opByOpType))
                {
                    op = opByOpType;
                }
            }

            return op;
        }

        private JObject? ConvertPayloadToJObject(JToken? payloadToken)
        {
            // REMARK: Consider if we really want it to be JObject
            if (payloadToken is JObject alreadyJObject)
            {
                return alreadyJObject;
            }
            else if (payloadToken is JValue payloadValue)
            {
                string payloadString = payloadValue.ToString();
                try
                {
                    JObject payloadObject = JObject.Parse(payloadString);
                    return payloadObject;
                }
                catch (JsonReaderException ex)
                {
                    throw new InvalidOperationException($"Cannot parse JSON in JValue payload {ex.Message}. Payload string {payloadString}");
                }
            }
            else if (payloadToken == null)
            {
                return null;
            }
            else
            {
                throw new InvalidOperationException($"Payload is of unknown type: {payloadToken.GetType().FullName} - cannot convert to JObject.");
            }
        }

        private void ValidateAppSettingsConfiguration()
        {
            ValidateRequired("Kafka:GroupId");
            ValidateRequired("Kafka:BootstrapServers");

            // Sprawdzenie liczby wpisów w Kafka:BootstrapServers
            var bootstrapServers = _configuration["Kafka:BootstrapServers"];
            if (bootstrapServers.Split(',').Length == 1)
            {
                _logger.LogWarning("There is only one entry in Kafka:BootstrapServers which is incorrect in production scenario. It's valid for test scenarios though.");
            }

            ValidateRequired("Kafka:OldToNewTopic");
            ValidateRequired("Kafka:NewToOldTopic");
            ValidateRequired("Kafka:EventQueueTopic");

            ValidateRequired("InternalKafkaProcessorParameters:MemoryCacheExpirationInSeconds");
            ValidateRequired("InternalKafkaProcessorParameters:BufferEventsCollectionInMilliseconds");
            ValidateRequired("InternalKafkaProcessorParameters:BufferEventsWaitingTimeInMilliseconds");
            ValidateRequired("InternalKafkaProcessorParameters:FindingDependenciesMaxAttempts");
            ValidateRequired("InternalKafkaProcessorParameters:FindingDependenciesWaitingTimeInMilliseconds");
            ValidateRequired("InternalKafkaProcessorParameters:EventSortingServiceMaxWaitTimeInSeconds");
            ValidateRequired("InternalKafkaProcessorParameters:EventSortingServiceAdditionalResultConsumeTimeInMilliseconds");
            ValidateRequired("InternalKafkaProcessorParameters:EventSortingServiceDelayInMilliseconds");
        }

        private void ValidateRequired(string key)
        {
            if (string.IsNullOrWhiteSpace(_configuration[key]))
            {
                _logger.LogError("Value of {Key} is null or empty", key);
                throw new InvalidOperationException($"Value of {key} is null or empty");
            }
        }

        private async Task ValidateDebeziumTopics()
        {
            // REMARK: As added safety measure we could check here that Debezium is running in order to avoid this error:
            // An error occurred while fetching replication factor for topic old_to_new.***: Sequence contains no elements
            // However, we check for topic to become available inside GetReplicationFactorAsync so this is not necessary.
            // If you hit the error here, you may check if topic was correctly created at http://localhost:9021/clusters/1/management/topics

            // Check if topics are correctly created
            _logger.LogInformation("Checking replication factor for both topics - it may take a while if there is any issue");
            short? oldToNewReplicationFactor = await _kafkaService.GetReplicationFactorAsync(_configuration["Kafka:OldToNewTopic"]);
            short? newToOldReplicationFactor = await _kafkaService.GetReplicationFactorAsync(_configuration["Kafka:NewToOldTopic"]);
            if (oldToNewReplicationFactor == null || newToOldReplicationFactor == null)
            {
                throw new InvalidOperationException($"Cannot check replication factor for {_configuration["Kafka:OldToNewTopic"]} or {_configuration["Kafka:NewToOldTopic"]}");
            }
            if (oldToNewReplicationFactor < 3 || oldToNewReplicationFactor < 3)
            {
                _logger.LogWarning($"Replication factor for {_configuration["Kafka:OldToNewTopic"]} or {_configuration["Kafka:NewToOldTopic"]} is less than three. This is not good for production, only for local testing. Consider changing Debezium connectors topics auto-creation value, i.e. replication.factor parameters.");
            }
        }

        private void OnStopping()
        {
            _logger.LogInformation("Application is stopping...");
            _consumerOld?.Close();
            _consumerNew?.Close();
            _eventConsumer?.Close();
            _logger.LogInformation("Application stopped");
        }
    }
}
