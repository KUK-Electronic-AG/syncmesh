using Newtonsoft.Json;

namespace KUK.KafkaProcessor.Models
{
    public class Source
    {
        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("connector")]
        public string Connector { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("ts_ms")]
        public long TsMs { get; set; }

        [JsonProperty("snapshot")]
        public string Snapshot { get; set; }

        [JsonProperty("db")]
        public string Db { get; set; }

        [JsonProperty("sequence")]
        public object Sequence { get; set; }

        [JsonProperty("table")]
        public object Table { get; set; }

        [JsonProperty("server_id")]
        public int ServerId { get; set; }

        [JsonProperty("gtid")]
        public object Gtid { get; set; }

        [JsonProperty("file")]
        public string File { get; set; }

        [JsonProperty("pos")]
        public int Pos { get; set; }

        [JsonProperty("row")]
        public int Row { get; set; }

        [JsonProperty("thread")]
        public object Thread { get; set; }

        [JsonProperty("query")]
        public object Query { get; set; }

        [JsonProperty("op")]
        public string Op { get; set; }
    }
}
