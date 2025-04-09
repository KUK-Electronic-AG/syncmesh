using System.Text.Json.Serialization;

namespace KUK.KafkaProcessor.Models
{
    public class ConnectorInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("connector")]
        public Connector Connector { get; set; }

        [JsonPropertyName("tasks")]
        public List<TaskInfo> Tasks { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }
    }
}
