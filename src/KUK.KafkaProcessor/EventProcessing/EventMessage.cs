using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KUK.KafkaProcessor.EventProcessing
{
    public class EventMessage
    {
        public string Payload { get; set; }
        public DateTime Timestamp { get; set; }

        public override string ToString()
        {
            string eventType = "";
            string invoiceId = "";
            string invoiceLineId = "";

            try
            {
                // Parse the outer JSON, which contains aggregate_type and the inner payload
                var outerObj = JObject.Parse(Payload);
                eventType = outerObj["aggregate_type"]?.ToString() ?? "";

                // Attempt to read the nested JSON contained in the "payload" field
                string innerPayloadStr = outerObj["payload"]?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(innerPayloadStr))
                {
                    var innerObj = JObject.Parse(innerPayloadStr);
                    invoiceId = innerObj["InvoiceId"]?.ToString() ?? "";
                    invoiceLineId = innerObj["InvoiceLineId"]?.ToString() ?? "";
                }
            }
            catch
            {
                // In case of parsing problems, leave values empty.
            }

            return $"Timestamp={Timestamp}, Type={eventType}, InvoiceId={invoiceId}, InvoiceLineId={invoiceLineId}, Payload={Payload}";
        }
    }
}
