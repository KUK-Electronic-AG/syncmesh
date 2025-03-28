namespace KUK.EventsDebugger
{
    public class OutboxEntry
    {
        public string SourceDatabase { get; set; }
        public string SourceTable { get; set; }

        public string EventId { get; set; }
        public string AggregateId { get; set; }
        public string AggregateType { get; set; }
        public string EventType { get; set; }
        public string Payload { get; set; }
        public string UniqueIdentifier { get; set; }
        public DateTime CreatedAt { get; set; }

        public override string ToString()
        {
            return $"EventId={EventId}, AggregateId={AggregateId}, AggregateType={AggregateType}, EventType={EventType}, Payload={Payload}, UniqueIdentifier={UniqueIdentifier}, CreatedAt={CreatedAt}";
        }
    }
}
