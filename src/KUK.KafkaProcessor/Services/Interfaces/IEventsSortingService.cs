using Confluent.Kafka;
using KUK.KafkaProcessor.EventProcessing;
using Newtonsoft.Json.Linq;

namespace KUK.KafkaProcessor.Services.Interfaces
{
    public interface IEventsSortingService
    {
        List<EventMessage> SortEvents(List<EventMessage> eventsToProcess, List<List<PriorityDependency>> priorityLists, int bufferId);
        string ExtractAggregateId(string payload);
        (int primary, int secondary) GetPriority(JObject json, List<List<PriorityDependency>> priorities);
        string ExtractEventType(string payload);
        string ExtractEventSource(string payload);
        string ExtractOperation(string payload);
        string GetOpBasedOnEventType(string eventType);
        Task<bool> CheckDependencyExistsAsync(string dependencyType, string aggregateId, string source);
        Task<EventMessage> ProcessMessageToEventMessageAsync(ConsumeResult<Ignore, string> consumeResult);
        Task<List<EventMessage>> EnsureDependenciesAsync(
            List<EventMessage> eventsToProcess,
            List<List<PriorityDependency>> priorityLists,
            IConsumer<Ignore, string> consumerBuffer,
            List<ConsumeResult<Ignore, string>> consumedResults,
            List<EventMessage> deferredKafkaEvents,
            CancellationToken cancellationToken);
        bool IsDependentEvent(string eventType, List<List<PriorityDependency>> priorityLists);
        PriorityDependency GetDependency(string eventType, List<List<PriorityDependency>> priorityLists);
        bool ShouldCacheEvent(string eventType, List<List<PriorityDependency>> priorityLists);
        string ExtractDependencyId(string payload, string idField);
        bool IsSnapshotEvent(string payload);
        
        // Method added to enable tests to work properly without using reflection
        Task EnsureDependencyForEventAsync(
            EventMessage evt,
            List<PriorityDependency> group,
            List<EventMessage> eventsToProcess,
            IConsumer<Ignore, string> consumerBuffer,
            List<ConsumeResult<Ignore, string>> consumedResults,
            double maxWaitTimeInSeconds,
            double additionalResultConsumeTimeInMilliseconds,
            double delayInMilliseconds,
            List<EventMessage> deferredKafkaEvents,
            CancellationToken cancellationToken);
            
        // Method added to enable tests to work properly without using reflection
        Task<bool> WaitForDependencyEventAsync(
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
            CancellationToken cancellationToken);
    }
}
