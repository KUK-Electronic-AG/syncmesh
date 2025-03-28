using Newtonsoft.Json;

namespace KUK.KafkaProcessor.Models
{
    public class DebeziumMessage
    {
        [JsonProperty("source")]
        public Source Source { get; set; }

        [JsonProperty("ts_ms")]
        public long TsMs { get; set; }

        [JsonProperty("databaseName")]
        public string DatabaseName { get; set; }

        [JsonProperty("schemaName")]
        public object SchemaName { get; set; }

        [JsonProperty("ddl")]
        public string Ddl { get; set; }

        [JsonProperty("tableChanges")]
        public List<object> TableChanges { get; set; }
    }
}