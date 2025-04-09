using System.Text.Json.Serialization;

namespace KUK.KafkaProcessor.Models
{
    public class Connector
    {
        [JsonPropertyName("state")]
        public string State { get; set; }

        [JsonPropertyName("worker_id")]
        public string WorkerId { get; set; }
    }
}
