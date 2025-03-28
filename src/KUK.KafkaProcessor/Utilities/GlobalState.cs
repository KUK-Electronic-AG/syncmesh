namespace KUK.KafkaProcessor.Utilities
{
    public class GlobalState
    {
        public bool IsInitialized { get; set; }
        public bool SnapshotLastReceived { get; set; }
        public bool IsPollyRetrying { get; set; }
    }
}
