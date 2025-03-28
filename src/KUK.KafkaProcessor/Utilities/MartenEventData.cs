namespace KUK.KafkaProcessor.Utilities
{
    public class MartenEventData
    {
        public Guid Id { get; set; } // Property Id is required by Marten
        public string Source { get; set; }
        public string SyncId { get; set; }
        public string Data { get; set; }
    }
}
