namespace KUK.KafkaProcessor.EventProcessing
{
    /// <summary>
    /// Represents priority dependency between types of events.
    /// </summary>
    public class PriorityDependency
    {
        /// <summary>
        /// Event type (e.g. "INVOICE", "CUSTOMER", "INVOICELINE").
        /// </summary>
        public string Type { get; }

        /// <summary>
        /// Name of the field in paylaod that contains dependency ID (e.g. "InvoiceId", "CustomerId").
        /// </summary>
        public string IdField { get; }

        /// <summary>
        /// Creates new object of priority dependency.
        /// </summary>
        /// <param name="type">Event type.</param>
        /// <param name="idField">Name of the field in the payload, that contains ID of dependency.</param>
        public PriorityDependency(string type, string idField)
        {
            Type = type;
            IdField = idField;
        }

        public override string ToString()
        {
            return $"Type={Type}, IdField={IdField}";
        }
    }
} 