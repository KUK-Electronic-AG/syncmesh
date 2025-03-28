using System.Collections;
using System.Reflection;
using Confluent.Kafka;
using KUK.KafkaProcessor.EventProcessing;
using KUK.KafkaProcessor.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using NetTopologySuite.Index.HPRtree;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Cms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Text;
using Newtonsoft.Json;

namespace KUK.KafkaProcessor.Services
{
    public class EventsSortingService : IEventsSortingService
    {
        private readonly ILogger<EventsSortingService> _logger;
        private readonly IInvoiceService _invoiceService;
        private readonly ICustomerService _customerService;
        private readonly IAddressService _addressService;
        private readonly IMemoryCache _memoryCache;
        private readonly IConfiguration _configuration;
        private readonly MemoryCacheEntryOptions _cacheOptions;

        public EventsSortingService(
            ILogger<EventsSortingService> logger,
            IInvoiceService invoiceService,
            ICustomerService customerService,
            IAddressService addressService,
            IMemoryCache memoryCache,
            IConfiguration configuration)
        {
            _logger = logger;
            _invoiceService = invoiceService;
            _customerService = customerService;
            _addressService = addressService;
            _memoryCache = memoryCache;
            _configuration = configuration;
            double memoryCacheExpirationInSeconds = Convert.ToDouble(configuration["InternalKafkaProcessorParameters:MemoryCacheExpirationInSeconds"]);
            _cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(memoryCacheExpirationInSeconds));
        }

        public List<EventMessage> SortEvents(List<EventMessage> events, List<List<PriorityDependency>> priorityLists, int bufferId)
        {
            // Dependencies: what depends on what
            var dependencies = new Dictionary<EventMessage, List<EventMessage>>();
            var reverseDependencies = new Dictionary<EventMessage, List<EventMessage>>();

            // Initialize both maps for all the events
            foreach (var e in events)
            {
                dependencies[e] = new List<EventMessage>();
                reverseDependencies[e] = new List<EventMessage>();
            }

            foreach (var e in events)
            {
                var payload = JObject.Parse(e.Payload.ToString());
                var innerPayload = JObject.Parse(payload["payload"]?.ToString() ?? "{}");

                foreach (var list in priorityLists)
                {
                    for (int i = 1; i < list.Count; i++)
                    {
                        var dependentType = list[i].Type.ToUpperInvariant();
                        var dependsOnType = list[i - 1].Type.ToUpperInvariant();
                        var idField = list[i].IdField;

                        var eventType = ExtractEventType(e.Payload)?.ToUpperInvariant();
                        if (eventType == dependentType)
                        {
                            var foreignId = innerPayload[idField]?.ToString();
                            if (foreignId != null)
                            {
                                var target = events.FirstOrDefault(ev =>
                                    ExtractEventType(ev.Payload)?.ToUpperInvariant() == dependsOnType &&
                                    ExtractAggregateId(ev.Payload) == foreignId);

                                if (target != null)
                                {
                                    dependencies[e].Add(target);
                                    reverseDependencies[target].Add(e); // This target already has initialized list
                                }
                            }
                        }
                    }
                }
            }

            // REMARK: You may consider moving initialization of reverseDependencies earlier in the call stack and passing it as parameter

            // Number of input dependencies
            var inDegree = events.ToDictionary(e => e, e => dependencies[e].Count);

            var queue = new Queue<EventMessage>(events.Where(e => inDegree[e] == 0));
            var result = new List<EventMessage>();

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                result.Add(current);

                foreach (var dependent in reverseDependencies[current])
                {
                    inDegree[dependent]--;
                    if (inDegree[dependent] == 0)
                    {
                        queue.Enqueue(dependent);
                    }
                }
            }

            // Fallback – if anything else is left (e.g. cycles)
            if (result.Count < events.Count)
            {
                var missing = events.Except(result).ToList();
                result.AddRange(missing);
            }

            return result;
        }

        public (int primary, int secondary) GetPriority(JObject json, List<List<PriorityDependency>> priorityLists)
        {
            string eventType = json["aggregate_type"]?.ToString()?.Trim() ?? "";
            eventType = eventType.ToUpperInvariant();

            // We search each group in order
            for (int groupIndex = 0; groupIndex < priorityLists.Count; groupIndex++)
            {
                var group = priorityLists[groupIndex];
                for (int elementIndex = 0; elementIndex < group.Count; elementIndex++)
                {
                    if (string.Equals(group[elementIndex].Type, eventType, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return (groupIndex, elementIndex);
                    }
                }
            }
            return (int.MaxValue, int.MaxValue);
        }

        public string ExtractAggregateId(string payload)
        {
            try
            {
                var json = JObject.Parse(payload);

                // If payload is snapshot, we return empty string
                if (json["source"] != null)
                {
                    string snapshot = json["source"]["snapshot"]?.ToString();
                    if (!string.IsNullOrEmpty(snapshot) &&
                        (snapshot.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                         snapshot.Equals("last", StringComparison.OrdinalIgnoreCase)))
                    {
                        return string.Empty;
                    }
                }

                string aggId = json["aggregate_id"]?.ToString().Trim();
                if (string.IsNullOrEmpty(aggId))
                {
                    _logger.LogWarning("Property 'aggregate_id' is missing or empty in payload.");
                    return "MISSING_AGGREGATE_ID";
                }
                return aggId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while extracting aggregate_id from payload: {Payload}", payload);
                throw;
            }
        }

        public string ExtractEventType(string payload)
        {
            try
            {
                var json = JObject.Parse(payload);

                // If payload is snapshot, we return empty string
                if (json["source"] != null)
                {
                    string snapshot = json["source"]["snapshot"]?.ToString();
                    if (!string.IsNullOrEmpty(snapshot) &&
                        (snapshot.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                         snapshot.Equals("last", StringComparison.OrdinalIgnoreCase)))
                    {
                        return string.Empty;
                    }
                }

                // First we try to read "aggregate_type" property
                string eventType = json["aggregate_type"]?.ToString()?.Trim();

                // If there is no "aggregate_type", we try with "event_type"
                if (string.IsNullOrEmpty(eventType))
                {
                    eventType = json["event_type"]?.ToString()?.Trim();
                }
                if (string.IsNullOrEmpty(eventType))
                {
                    _logger.LogWarning("Event type is missing or empty in payload.");
                    return "MISSING_EVENT_TYPE";
                }
                return eventType;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting event type from payload: {Payload}", payload);
                throw;
            }
        }

        public string ExtractEventSource(string payload)
        {
            return ExtractProperty(payload, "__source_name");
        }

        private string ExtractProperty(string payload, string propertyName)
        {
            try
            {
                var json = JObject.Parse(payload);

                // If payload is snapshot, we return empty string
                if (json["source"] != null)
                {
                    string snapshot = json["source"]["snapshot"]?.ToString();
                    if (!string.IsNullOrEmpty(snapshot) &&
                        (snapshot.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                         snapshot.Equals("last", StringComparison.OrdinalIgnoreCase)))
                    {
                        return string.Empty;
                    }
                }

                string value = json[propertyName]?.ToString().Trim();
                if (string.IsNullOrEmpty(value))
                {
                    throw new InvalidOperationException($"Property '{propertyName}' is missing or empty in payload.");
                }
                return value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while extracting property '{PropertyName}' from payload: {Payload}", propertyName, payload);
                throw;
            }
        }

        public string ExtractOperation(string payload)
        {
            var eventType = ExtractProperty(payload, "event_type", convertToUpper: false);
            if (string.IsNullOrEmpty(eventType))
            {
                return string.Empty; // for initial snapshot
            }
            var operation = GetOpBasedOnEventType(eventType);
            return operation;
        }

        public string GetOpBasedOnEventType(string eventType)
        {
            switch (eventType)
            {
                case "c":
                case "CREATED":
                    return "c";
                case "u":
                case "UPDATED":
                    return "u";
                case "d":
                case "DELETED":
                    return "d";
                default:
                    throw new InvalidOperationException($"Unknown event type {eventType}");
            }
        }

        public async Task<bool> CheckDependencyExistsAsync(string dependencyType, string aggregateId, string source)
        {
            _logger.LogDebug($"CheckDependencyExistsAsync (dependencyType={dependencyType}, aggregateId={aggregateId}, source={source})");
            string normalizedDependencyType = NormalizeDependencyType(dependencyType);

            // We always return true for special case of address
            if (aggregateId == "CREATE_NEW_ADDRESS")
            {
                return true;
            }

            switch (normalizedDependencyType)
            {
                case "INVOICE":
                    switch (source.ToUpper())
                    {
                        case "OLD_TO_NEW":
                            _logger.LogDebug($"Checking in old - CheckDependencyExistsAsync (normalizedDependencyType={normalizedDependencyType}, aggregateId={aggregateId})");
                            var foundInOld = await _invoiceService.MappingExists(int.Parse(aggregateId));
                            _logger.LogDebug($"Found in old: {foundInOld} - CheckDependencyExistsAsync (normalizedDependencyType={normalizedDependencyType}, aggregateId={aggregateId})");
                            return foundInOld;
                        case "NEW_TO_OLD":
                            _logger.LogDebug($"Checking in new - CheckDependencyExistsAsync (normalizedDependencyType={normalizedDependencyType}, aggregateId={aggregateId})");
                            var foundInNew = await _invoiceService.MappingExists(Guid.Parse(aggregateId));
                            _logger.LogDebug($"Found in new: {foundInNew} - CheckDependencyExistsAsync (normalizedDependencyType={normalizedDependencyType}, aggregateId={aggregateId})");
                            return foundInNew;
                        default:
                            _logger.LogWarning($"CheckDependencyExistsAsync - unknown source {source}");
                            throw new InvalidOperationException($"Unknown source {source}");
                    }
                case "CUSTOMER":
                    switch (source.ToUpper())
                    {
                        case "OLD_TO_NEW":
                            _logger.LogDebug($"Checking in old - CheckDependencyExistsAsync (normalizedDependencyType={normalizedDependencyType}, aggregateId={aggregateId})");
                            var foundInOld = await _customerService.MappingExists(int.Parse(aggregateId));
                            _logger.LogDebug($"Found in old: {foundInOld} - CheckDependencyExistsAsync (normalizedDependencyType={normalizedDependencyType}, aggregateId={aggregateId})");
                            return foundInOld;
                        case "NEW_TO_OLD":
                            _logger.LogDebug($"Checking in new - CheckDependencyExistsAsync (normalizedDependencyType={normalizedDependencyType}, aggregateId={aggregateId})");
                            var foundInNew = await _customerService.MappingExists(Guid.Parse(aggregateId));
                            _logger.LogDebug($"Found in new: {foundInNew} - CheckDependencyExistsAsync (normalizedDependencyType={normalizedDependencyType}, aggregateId={aggregateId})");
                            return foundInNew;
                        default:
                            _logger.LogWarning($"CheckDependencyExistsAsync - unknown source {source}");
                            throw new InvalidOperationException($"Unknown source {source}");
                    }
                case "ADDRESS":
                    switch (source.ToUpper())
                    {
                        case "OLD_TO_NEW":
                            _logger.LogDebug($"Checking in old - CheckDependencyExistsAsync (normalizedDependencyType={normalizedDependencyType}, aggregateId={aggregateId})");
                            var foundInOld = await _addressService.MappingExists(int.Parse(aggregateId));
                            _logger.LogDebug($"Found in old: {foundInOld} - CheckDependencyExistsAsync (normalizedDependencyType={normalizedDependencyType}, aggregateId={aggregateId})");
                            return foundInOld;
                        case "NEW_TO_OLD":
                            _logger.LogDebug($"Checking in new - CheckDependencyExistsAsync (normalizedDependencyType={normalizedDependencyType}, aggregateId={aggregateId})");
                            var foundInNew = await _addressService.MappingExists(Guid.Parse(aggregateId));
                            _logger.LogDebug($"Found in new: {foundInNew} - CheckDependencyExistsAsync (normalizedDependencyType={normalizedDependencyType}, aggregateId={aggregateId})");
                            return foundInNew;
                        default:
                            _logger.LogWarning($"CheckDependencyExistsAsync - unknown source {source}");
                            throw new InvalidOperationException($"Unknown source {source}");
                    }
                // REMARK: Here you add new cases for other types like "A", "B", "C" etc.
                default:
                    _logger.LogError($"CheckDependencyExistsAsync - default {normalizedDependencyType}");
                    return false;
            }
        }

        public PriorityDependency GetDependency(string eventType, List<List<PriorityDependency>> priorityLists)
        {
            foreach (var group in priorityLists)
            {
                int index = group.FindIndex(x => string.Equals(x.Type, eventType, StringComparison.InvariantCultureIgnoreCase));
                if (index > 0)
                {
                    return group[index - 1];
                }
            }
            return null;
        }

        public bool IsDependentEvent(string eventType, List<List<PriorityDependency>> priorityLists)
        {
            foreach (var group in priorityLists)
            {
                int index = group.FindIndex(x => string.Equals(x.Type, eventType, StringComparison.InvariantCultureIgnoreCase));
                if (index >= 0)
                {
                    return index > 0;
                }
            }
            return false;
        }

        public bool ShouldCacheEvent(string eventType, List<List<PriorityDependency>> priorityLists)
        {
            foreach (var group in priorityLists)
            {
                // We search for event in the group (not taking into account case sensitivity)
                int index = group.FindIndex(x => string.Equals(x.Type, eventType, StringComparison.InvariantCultureIgnoreCase));
                // If event is in the group and is not the last element, it will be dependency for next events
                if (index != -1 && index < group.Count - 1)
                {
                    return true;
                }
            }
            return false;
        }

        public async Task<List<EventMessage>> EnsureDependenciesAsync(
            List<EventMessage> eventsToProcess,
            List<List<PriorityDependency>> priorityLists,
            IConsumer<Ignore, string> consumerBuffer,
            List<ConsumeResult<Ignore, string>> consumedResults,
            List<EventMessage> deferredKafkaEvents,
            CancellationToken cancellationToken)
        {
            double maxWaitTimeInSeconds = 
                Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceMaxWaitTimeInSeconds"]);
            double additionalResultConsumeTimeInMilliseconds = 
                Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceAdditionalResultConsumeTimeInMilliseconds"]);
            double delayInMilliseconds = 
                Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceDelayInMilliseconds"]);

            foreach (var group in priorityLists)
            {
                // We process a copy of the list to avoid conflicts between iteration and modifications of the original list
                var eventsCopy = eventsToProcess.ToList();

                foreach (var evt in eventsCopy)
                {
                    // Extract necessary data from the payload once
                    string eventType = ExtractEventType(evt.Payload);
                    if (IsEventTypeInGroup(group, eventType))
                    {
                        int priorityIndex = IndexOfEventType(group, eventType);
                        // Jeśli event ma zależność (nie jest pierwszym w grupie)
                        if (priorityIndex > 0)
                        {
                            await EnsureDependencyForEventAsync(evt, group, eventsToProcess, consumerBuffer,
                                consumedResults, maxWaitTimeInSeconds, additionalResultConsumeTimeInMilliseconds,
                                delayInMilliseconds, deferredKafkaEvents, cancellationToken);
                        }
                    }
                }
            }

            return eventsToProcess;
        }

        /// <summary>
        /// For a given event, extracts all necessary data from the payload and ensures
        /// that its dependency (e.g. INVOICE for INVOICELINE) has been satisfied - either it's already in the buffer,
        /// or it will appear as an additional event, or it exists in the system (external check).
        /// </summary>
        public async Task EnsureDependencyForEventAsync(
            EventMessage evt,
            List<PriorityDependency> group,
            List<EventMessage> eventsToProcess,
            IConsumer<Ignore, string> consumerBuffer,
            List<ConsumeResult<Ignore, string>> consumedResults,
            double maxWaitTimeInSeconds,
            double additionalResultConsumeTimeInMilliseconds,
            double delayInMilliseconds,
            List<EventMessage> deferredKafkaEvents,
            CancellationToken cancellationToken)
        {
            // Extract necessary properties once.
            string eventType = ExtractEventType(evt.Payload);
            string aggregateId = ExtractAggregateId(evt.Payload);
            string source = ExtractEventSource(evt.Payload);
            _logger.LogDebug("EnsureDependencyForEventAsync: Processing event. aggregateId='{AggregateId}', eventType='{EventType}', source='{Source}'.",
                aggregateId, eventType, source);

            int priorityIndex = IndexOfEventType(group, eventType);
            if (priorityIndex <= 0)
            {
                _logger.LogDebug("EnsureDependencyForEventAsync: No dependency required for eventType='{EventType}' and aggregateId='{AggregateId}'.", eventType, aggregateId);
                return; // No dependency required.
            }

            PriorityDependency dependency = group[priorityIndex - 1];
            string dependencyType = dependency.Type;
            string idField = dependency.IdField;
            _logger.LogDebug($"[DIAGNOSTIC] Calling ExtractDependencyId with idField={idField} and payload={evt.Payload}");
            string expectedDependencyAggregateId = ExtractDependencyId(evt.Payload, idField);
            if (string.IsNullOrEmpty(expectedDependencyAggregateId))
            {
                _logger.LogWarning($"Expected dependency aggregate id is empty for idField={idField}");
            }
            
            // We define operation (CREATED, UPDATED, DELETED) from the payload
            string operation = ExtractProperty(evt.Payload, "event_type", false);
            bool isDeleteOperation = operation.Equals("DELETED", StringComparison.OrdinalIgnoreCase) || 
                                      operation.Equals("d", StringComparison.OrdinalIgnoreCase);
            
            // If ID of dependency not found
            if (string.IsNullOrEmpty(expectedDependencyAggregateId))
            {
                // For DELETE operations we simply skip checking the dependency
                if (isDeleteOperation)
                {
                    _logger.LogDebug("EnsureDependencyForEventAsync: Missing dependency ID for {DependencyType} in DELETE event with aggregateId={AggregateId}. Skipping dependency check.",
                        dependencyType, aggregateId);
                    return;
                }
                // For UPDATE operation we check if ID of dependency is CREATE_NEW_ADDRESS
                else if (operation.Equals("UPDATED", StringComparison.OrdinalIgnoreCase) && expectedDependencyAggregateId == "CREATE_NEW_ADDRESS")
                {
                    _logger.LogDebug("EnsureDependencyForEventAsync: Ignoring missing AddressId for UPDATE event with aggregateId={AggregateId}.", aggregateId);
                    return; // We ignore lack of ID in case of update
                }
                // For other operations (CREATED) we log warning but we still process
                else
                {
                    _logger.LogWarning("EnsureDependencyForEventAsync: Missing dependency ID for {DependencyType} in {Operation} event with aggregateId={AggregateId}. ExpectedDependencyAggregateId={ExpectedDependencyAggregateId}. This may indicate an issue with the data.",
                        dependencyType, operation, aggregateId, expectedDependencyAggregateId);
                    return;
                }
            }
            
            string cacheKey = $"{dependencyType.ToUpperInvariant()}:{expectedDependencyAggregateId}";
            _logger.LogDebug("EnsureDependencyForEventAsync: For event with aggregateId='{AggregateId}', dependencyType='{DependencyType}', expectedDependencyAggregateId='{ExpectedDependencyAggregateId}', cacheKey='{CacheKey}'.",
                aggregateId, dependencyType, expectedDependencyAggregateId, cacheKey);

            // Log current cache status.
            bool cacheContains = _memoryCache.TryGetValue(cacheKey, out object existingCacheValue);
            _logger.LogDebug("EnsureDependencyForEventAsync: Cache status for key '{CacheKey}': {CacheStatus}. EventType='{EventType}', AggregateId='{AggregateId}'", cacheKey, cacheContains, eventType, aggregateId);

            if (IsDependencyInCacheOrBuffer(eventsToProcess, dependencyType, expectedDependencyAggregateId))
            {
                _logger.LogDebug("EnsureDependencyForEventAsync: Dependency already satisfied in cache or buffer for cache key '{CacheKey}' with EventyType='{EventType}' and AggregateId='{Aggregateid}'.", cacheKey, eventType, aggregateId);
                _memoryCache.Set(cacheKey, true, _cacheOptions);
                return;
            }

            // Perform external dependency check.
            bool dependencyExists = await CheckDependencyExistsAsync(dependencyType, expectedDependencyAggregateId, source);
            _logger.LogDebug("EnsureDependencyForEventAsync: External dependency check for key '{CacheKey}' returned '{DependencyExists}'. EventType='{EventType}', AggregateId='{AggregateId}'", cacheKey, dependencyExists, eventType, aggregateId);
            if (dependencyExists)
            {
                _logger.LogDebug("EnsureDependencyForEventAsync: External check confirmed dependency for key '{CacheKey}'. EventType='{EventType}', AggregateId='{AggregateId}'", cacheKey, eventType, aggregateId);
                _memoryCache.Set(cacheKey, true, _cacheOptions);
                return;
            }

            // Wait for additional events to satisfy the dependency.
            bool found = await WaitForDependencyEventAsync(
                aggregateId,
                dependencyType,
                expectedDependencyAggregateId,
                source,
                consumerBuffer,
                consumedResults,
                eventsToProcess,
                delayInMilliseconds,
                additionalResultConsumeTimeInMilliseconds,
                maxWaitTimeInSeconds,
                deferredKafkaEvents,
                cancellationToken);

            if (found)
            {
                _logger.LogDebug("EnsureDependencyForEventAsync: Dependency satisfied for key '{CacheKey}' after waiting.", cacheKey);
                _memoryCache.Set(cacheKey, true, _cacheOptions);
            }
            else
            {
                _logger.LogWarning("EnsureDependencyForEventAsync: Dependency not satisfied for key '{CacheKey}' after waiting.", cacheKey);
            }
        }

        /// <summary>
        /// Checks if the dependency (specified as dependencyType and expectedDependencyAggregateId)
        /// is already in the buffer or stored in cache.
        /// </summary>
        private bool IsDependencyInCacheOrBuffer(
            List<EventMessage> eventsToProcess,
            string dependencyType,
            string expectedDependencyAggregateId)
        {
            bool dependencyInBuffer = eventsToProcess.Any(e =>
                string.Equals(ExtractEventType(e.Payload), dependencyType, StringComparison.InvariantCultureIgnoreCase) &&
                ExtractAggregateId(e.Payload) == expectedDependencyAggregateId);
            bool cacheHit = _memoryCache.TryGetValue($"{dependencyType.ToUpperInvariant()}:{expectedDependencyAggregateId}", out _);
            return dependencyInBuffer || cacheHit;
        }

        /// <summary>
        /// Waits for an additional event that satisfies the dependency. Returns true if such event was found,
        /// or false after the maximum wait time has elapsed.
        /// </summary>
        public async Task<bool> WaitForDependencyEventAsync(
            string aggregateId,
            string dependencyType,
            string expectedDependencyAggregateId,
            string source,
            IConsumer<Ignore, string> consumerBuffer,
            List<ConsumeResult<Ignore, string>> consumedResults,
            List<EventMessage> eventsToProcess,
            double delayInMilliseconds,
            double additionalResultConsumeTimeInMilliseconds,
            double maxWaitTimeInSeconds,
            List<EventMessage> deferredKafkaEvents,
            CancellationToken cancellationToken)
        {
            var waitStartTime = DateTime.UtcNow;
            var maxWaitTime = TimeSpan.FromSeconds(maxWaitTimeInSeconds);
            int attempt = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                attempt++;
                var elapsed = DateTime.UtcNow - waitStartTime;
                _logger.LogDebug("WaitForDependencyEventAsync [Attempt {Attempt}]: Waiting for dependency for aggregateId='{AggregateId}'. Expected dependencyType='{DependencyType}', expectedDependencyAggregateId='{ExpectedDependencyAggregateId}'. Elapsed={ElapsedSeconds}s.",
                    attempt, aggregateId, dependencyType, expectedDependencyAggregateId, elapsed.TotalSeconds);

                if (elapsed > maxWaitTime)
                {
                    _logger.LogError("WaitForDependencyEventAsync [Attempt {Attempt}]: Timeout waiting for dependency for aggregateId='{AggregateId}' after {ElapsedSeconds}s.",
                        attempt, aggregateId, elapsed.TotalSeconds);
                    return false;
                }

                // Additional check - verify if the appropriate event has already appeared in the global buffer.
                if (eventsToProcess.Any(e =>
                        string.Equals(ExtractEventType(e.Payload)?.Trim(), dependencyType?.Trim(), StringComparison.InvariantCultureIgnoreCase) &&
                        string.Equals(ExtractAggregateId(e.Payload)?.Trim(), expectedDependencyAggregateId?.Trim(), StringComparison.InvariantCultureIgnoreCase)))
                {
                    _logger.LogInformation("WaitForDependencyEventAsync [Attempt {Attempt}]: Found matching {DependencyType} event in global buffer for expectedDependencyAggregateId='{ExpectedDependencyAggregateId}'.",
                        attempt, dependencyType, expectedDependencyAggregateId);
                    return true;
                }

                var matchedEvent = deferredKafkaEvents
                    .SingleOrDefault(e => IsMatch(dependencyType, expectedDependencyAggregateId, e, attempt));

                if (matchedEvent != null)
                {
                    eventsToProcess.Add(matchedEvent);
                    deferredKafkaEvents.Remove(matchedEvent);
                }
                
                // New external check - if source is set
                if (!string.IsNullOrWhiteSpace(source))
                {
                    bool externalCheck = await CheckDependencyExistsAsync(dependencyType, expectedDependencyAggregateId, source);
                    _logger.LogDebug("WaitForDependencyEventAsync [Attempt {Attempt}]: External dependency check for key '{Key}' returned '{Result}'.",
                        attempt, $"{dependencyType.ToUpperInvariant()}:{expectedDependencyAggregateId}", externalCheck);
                    if (externalCheck)
                    {
                        _logger.LogInformation("WaitForDependencyEventAsync [Attempt {Attempt}]: External dependency confirmed for aggregateId='{AggregateId}'.", attempt, aggregateId);
                        return true;
                    }
                }

                var additionalResult = consumerBuffer.Consume(TimeSpan.FromMilliseconds(additionalResultConsumeTimeInMilliseconds));
                if (additionalResult != null)
                {
                    EventMessage additionalEvent = await ProcessMessageToEventMessageAsync(additionalResult);

                    bool isMatch = IsMatch(dependencyType, expectedDependencyAggregateId, additionalEvent, attempt);

                    if (isMatch)
                    {
                        _logger.LogInformation("WaitForDependencyEventAsync [Attempt {Attempt}]: Dependency satisfied for aggregateId='{AggregateId}'.", attempt, aggregateId);
                        consumedResults.Add(additionalResult);
                        eventsToProcess.Add(additionalEvent);

                        return true;
                    }
                    else
                    {
                        _logger.LogDebug("WaitForDependencyEventAsync [Attempt {Attempt}]: Additional event does not match. Expected: type='{DependencyType}', aggregateId='{ExpectedDependencyAggregateId}'.",
                            attempt, dependencyType, expectedDependencyAggregateId);
                        deferredKafkaEvents.Add(additionalEvent);
                    }
                }
                else
                {
                    _logger.LogDebug("WaitForDependencyEventAsync [Attempt {Attempt}]: No additional event received for aggregateId='{AggregateId}'.", attempt, aggregateId);
                }

                await Task.Delay(Convert.ToInt32(delayInMilliseconds), cancellationToken);
            }

            return false;
        }

        private bool IsMatch(string dependencyType, string expectedDependencyAggregateId, EventMessage additionalEvent, int attempt)
        {
            string additionalEventType = ExtractEventType(additionalEvent.Payload)?.Trim();
            string additionalAggregateId = ExtractAggregateId(additionalEvent.Payload)?.Trim();
            _logger.LogDebug("WaitForDependencyEventAsync [Attempt {Attempt}]: Received additional event. Extracted type='{AdditionalEventType}', aggregateId='{AdditionalAggregateId}'.",
                attempt, additionalEventType, additionalAggregateId);

            return string.Equals(additionalEventType, dependencyType?.Trim(), StringComparison.InvariantCultureIgnoreCase) &&
                                    string.Equals(additionalAggregateId, expectedDependencyAggregateId?.Trim(), StringComparison.InvariantCultureIgnoreCase);
        }

        public string ExtractDependencyId(string payload, string idFieldName)
        {
            try
            {
                // Deserialize main payload to dynamic
                dynamic outerJson = JsonConvert.DeserializeObject<dynamic>(payload);
                
                if (outerJson?.payload == null)
                {
                    return string.Empty;
                }
                
                string innerPayloadStr = outerJson.payload.ToString();
                
                try
                {
                    // Deserialize inner payload
                    dynamic innerJson = JsonConvert.DeserializeObject<dynamic>(innerPayloadStr);
                    
                    // Check if this event is of type CREATED for customer
                    if (outerJson.aggregate_type?.ToString().Equals("CUSTOMER", StringComparison.OrdinalIgnoreCase) == true &&
                        outerJson.event_type?.ToString().Equals("CREATED", StringComparison.OrdinalIgnoreCase) == true &&
                        idFieldName.Equals("AddressId", StringComparison.OrdinalIgnoreCase))
                    {
                        // For CREATED event for customer, we return special ID
                        // that will be later processed by CustomerService
                        return "CREATE_NEW_ADDRESS";
                    }
                    
                    // We try to read property with the name idFieldName
                    foreach (JProperty property in innerJson)
                    {
                        if (property.Name.Equals(idFieldName, StringComparison.OrdinalIgnoreCase))
                        {
                            return property.Value.ToString();
                        }
                    }
                    
                    // If property not found, we return empty string
                    return string.Empty;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failure parsing inner payload: {ex.Message}");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error extracting {idFieldName} from payload: {ex.Message}");
                return string.Empty;
            }
        }

        private int IndexOfEventType(List<PriorityDependency> group, string eventType)
        {
            return group.FindIndex(g => string.Equals(g.Type, eventType, StringComparison.InvariantCultureIgnoreCase));
        }

        private bool IsEventTypeInGroup(List<PriorityDependency> groups, string eventType)
        {
            return groups.Any(g => string.Equals(g.Type, eventType, StringComparison.InvariantCultureIgnoreCase));
        }

        public async Task<EventMessage> ProcessMessageToEventMessageAsync(
            ConsumeResult<Ignore, string> consumeResult)
        {
            // Retrieve the raw event body
            string eventBody = consumeResult.Message.Value;

            _logger.LogDebug($"Processing message at offset: '{consumeResult.TopicPartitionOffset}'.");
            // REMARK: Here you may write debug code that will ensure that all the
            // events from buffer contain correct time (synchronized time without different timezone)

            // Parse the JSON payload using JObject for easier property checking
            var jObject = JObject.Parse(eventBody);
            DateTime timestamp;

            // First, try to get "created_at" at the root level
            if (jObject.TryGetValue("created_at", out JToken createdAtToken))
            {
                if (createdAtToken.Type == JTokenType.Integer)
                {
                    // If created_at is an integer, assume it's a Unix timestamp in milliseconds.
                    long unixTimestamp = createdAtToken.Value<long>();
                    timestamp = DateTimeOffset.FromUnixTimeMilliseconds(unixTimestamp).UtcDateTime;
                }
                else
                {
                    // Otherwise, attempt to convert directly to DateTime.
                    timestamp = createdAtToken.ToObject<DateTime>();
                }
            }
            else if (jObject["source"] != null &&
                     jObject["source"]["snapshot"] != null &&
                     (jObject["source"]["snapshot"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase) ||
                      jObject["source"]["snapshot"].ToString().Equals("last", StringComparison.OrdinalIgnoreCase)))
            {
                // If the event is a snapshot (source.snapshot == "true" or "last"), set timestamp to DateTime.MinValue
                timestamp = DateTime.MinValue;
            }
            else
            {
                // Fallback: use current time
                timestamp = DateTime.Now;
            }

            // Return a new EventMessage with the payload and determined timestamp
            return new EventMessage
            {
                Payload = eventBody,
                Timestamp = timestamp
            };
        }

        private string ExtractProperty(string payload, string propertyName, bool convertToUpper = true)
        {
            try
            {
                var json = JObject.Parse(payload);
                var value = json[propertyName]?.ToString();
                if (string.IsNullOrWhiteSpace(value))
                    return string.Empty;

                return convertToUpper ? value.Trim().ToUpperInvariant() : value.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError("ExtractProperty exception: {Exception}", ex);
                return string.Empty;
            }
        }

        [Obsolete("Use ExtractDependencyId instead")]
        public string ExtractDependencyAggregateId(string payload)
        {
            try
            {
                var json = JObject.Parse(payload);
                return json["aggregate_id"]?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error extracting dependency aggregate id: {Exception}", ex);
                return string.Empty;
            }
        }
        public bool IsSnapshotEvent(string payload)
        {
            try
            {
                var json = JObject.Parse(payload);
                var snapshot = json["source"]?["snapshot"]?.ToString();
                return !string.IsNullOrEmpty(snapshot) && snapshot.Equals("true", StringComparison.InvariantCultureIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError("IsSnapshotEvent: Error parsing payload: {Exception}", ex);
                return false;
            }
        }

        /// <summary>
        /// Returns the expected dependency ID for the given event payload and dependency type.
        /// For dependencyType "Invoice", this method extracts the inner property "InvoiceId".
        /// For other types, it falls back to the outer aggregate_id.
        /// </summary>
        private string GetExpectedDependencyId(string payload, string dependencyType)
        {
            var outerJson = JObject.Parse(payload);
            // For Invoice dependency, try to get the inner "InvoiceId" property.
            if (string.Equals(dependencyType, "Invoice", StringComparison.OrdinalIgnoreCase))
            {
                var innerPayload = outerJson["payload"] as JObject;
                if (innerPayload != null)
                {
                    // Do a case-insensitive search for "InvoiceId"
                    var prop = innerPayload.Properties()
                        .FirstOrDefault(p => string.Equals(p.Name, "InvoiceId", StringComparison.OrdinalIgnoreCase));
                    if (prop != null)
                    {
                        return prop.Value.ToString().Trim();
                    }
                }
            }
            // Otherwise, return the outer aggregate_id.
            return outerJson["aggregate_id"]?.ToString().Trim() ?? string.Empty;
        }

        private string NormalizeDependencyType(string dependencyType)
        {
            if (string.IsNullOrEmpty(dependencyType))
            {
                return string.Empty;
            }
            
            return dependencyType.Trim().ToUpperInvariant();
        }
    }
}

