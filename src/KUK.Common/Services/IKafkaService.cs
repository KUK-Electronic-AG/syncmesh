namespace KUK.Common.Services
{
    public interface IKafkaService
    {
        Task<short?> GetReplicationFactorAsync(string topicName);
        Task CreateTopicAsync(string topicName, int numPartitions, short replicationFactor);
        Task DeleteKafkaTopic(string topicName);
    }
}
