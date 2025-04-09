using System.Text.Json.Serialization;

namespace KUK.KafkaProcessor.Models
{
    public class TaskInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; }

        [JsonPropertyName("worker_id")]
        public string WorkerId { get; set; }
    }
}
