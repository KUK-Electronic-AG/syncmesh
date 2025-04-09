namespace KUK.KafkaProcessor.Services.Interfaces
{
    public interface IDomainDependencyService
    {
        Task<bool> CheckDependencyExistsAsync(string dependencyType, string aggregateId, string source);
        bool ShouldSkipEnsuringDependency(
            string aggregateId, string dependencyType, string expectedDependencyAggregateId, string operation, bool isDeleteOperation);
        string GetAggregateIdToSkip(string idFieldName, dynamic outerJson);
    }
}
