using System.Text.Json;
using Confluent.Kafka;
using KUK.Common.Services;
using KUK.KafkaProcessor.Commands;
using KUK.KafkaProcessor.EventProcessing;
using KUK.KafkaProcessor.Services;
using KUK.KafkaProcessor.Services.Interfaces;
using KUK.KafkaProcessor.Utilities;
using Marten;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;

namespace KUK.UnitTests
{
    public static class TestHelpers
    {
        public static IEventsSortingService CreateService(
            ILogger<EventsSortingService> logger = null,
            IInvoiceService invoiceService = null,
            ICustomerService customerService = null,
            IAddressService addressService = null,
            IMemoryCache memoryCache = null,
            IConfiguration configuration = null)
        {
            // Use provided logger or create a default one
            logger ??= new LoggerFactory().CreateLogger<EventsSortingService>();

            // Use provided invoiceService mock or create a default one
            invoiceService ??= new Mock<IInvoiceService>().Object;

            // Use provided customerService mock or create a default one
            customerService ??= new Mock<ICustomerService>().Object;
            
            // Use provided addressService mock or create a default one
            addressService ??= new Mock<IAddressService>().Object;

            // Use provided memory cache or create a new one
            memoryCache ??= new MemoryCache(new MemoryCacheOptions());

            // Use provided configuration or create one with default values
            configuration ??= new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "InternalKafkaProcessorParameters:MemoryCacheExpirationInSeconds", "60" },
                    { "InternalKafkaProcessorParameters:EventSortingServiceMaxWaitTimeInSeconds", "5" },
                    { "InternalKafkaProcessorParameters:EventSortingServiceAdditionalResultConsumeTimeInMilliseconds", "100" },
                    { "InternalKafkaProcessorParameters:EventSortingServiceDelayInMilliseconds", "50" }
                })
                .Build();

            return new EventsSortingService(logger, invoiceService, customerService, addressService, memoryCache, configuration);
        }

        public static EventMessage CreateEvent(
            string eventType,
            string invoiceId,
            string invoiceLineId = null,
            string source = "OLD_TO_NEW")
        {
            var now = DateTime.UtcNow;
            var nowUnix = new DateTimeOffset(now).ToUnixTimeMilliseconds();

            // Tworzymy inner payload jako obiekt JSON
            var innerPayload = new JObject();
            innerPayload["InvoiceId"] = invoiceId;
            if (eventType.ToUpperInvariant() == "INVOICELINE")
            {
                innerPayload["InvoiceLineId"] = invoiceLineId;
            }
            innerPayload["Source"] = source;

            // Budujemy outer payload
            var outerPayload = new JObject
            {
                ["event_id"] = Guid.NewGuid().ToString(),
                ["aggregate_id"] = invoiceId,
                ["aggregate_type"] = eventType,
                ["event_type"] = eventType.ToUpperInvariant(),
                ["payload"] = innerPayload.ToString(Newtonsoft.Json.Formatting.None),
                ["unique_identifier"] = Guid.NewGuid().ToString(),
                ["created_at"] = nowUnix,
                ["__deleted"] = "false",
                ["__op"] = "c",
                ["__source_ts_ms"] = nowUnix,
                ["__source_table"] = eventType.ToLowerInvariant() + "_outbox",
                ["__source_name"] = source.ToLowerInvariant(),
                ["__query"] = ""
            };

            return new EventMessage
            {
                Timestamp = now,
                Payload = outerPayload.ToString(Newtonsoft.Json.Formatting.None)
            };
        }

        public static string ExtractEventType(string payload)
        {
            var json = JsonSerializer.Deserialize<Dictionary<string, object>>(payload);
            return json != null && json.ContainsKey("event_type") ? json["event_type"].ToString().Trim() : null;
        }

        public static string ExtractAggregateId(string payload)
        {
            var json = JsonSerializer.Deserialize<Dictionary<string, object>>(payload);
            return json != null && json.ContainsKey("aggregate_id") ? json["aggregate_id"].ToString().Trim() : null;
        }

        public static ConsumeResult<Ignore, string> CreateConsumeResult(string eventType, string aggregateId)
        {
            // Tworzymy inner payload w zależności od typu zdarzenia.
            string innerPayload;
            if (eventType.Equals("INVOICE", StringComparison.InvariantCultureIgnoreCase))
            {
                // Dla INVOICE ustawiamy tylko InvoiceId równe aggregateId.
                innerPayload = JsonSerializer.Serialize(new { InvoiceId = aggregateId });
            }
            else if (eventType.Equals("INVOICELINE", StringComparison.InvariantCultureIgnoreCase))
            {
                // Dla INVOICELINE ustawiamy InvoiceId równe aggregateId oraz generujemy nowy identyfikator InvoiceLineId.
                innerPayload = JsonSerializer.Serialize(new
                {
                    TrackId = 1,
                    Quantity = 1,
                    InvoiceId = aggregateId,
                    UnitPrice = 1,
                    InvoiceLineId = Guid.NewGuid().ToString()
                });
            }
            else if (eventType.Equals("CUSTOMER", StringComparison.InvariantCultureIgnoreCase))
            {
                // Dla CUSTOMER ustawiamy CustomerId równe aggregateId
                innerPayload = JsonSerializer.Serialize(new { CustomerId = aggregateId });
            }
            else
            {
                // Dla innych typów można zostawić pusty payload lub dostosować logikę.
                innerPayload = "{}";
            }

            // Generujemy zewnętrzny payload, w którym pole "payload" zawiera nasz inner payload.
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var payload = JsonSerializer.Serialize(new
            {
                event_id = Guid.NewGuid().ToString(),
                aggregate_id = aggregateId,
                aggregate_type = eventType,
                event_type = eventType,
                payload = innerPayload,
                unique_identifier = Guid.NewGuid().ToString(),
                created_at = now,
                __deleted = "false",
                __op = "c",
                __source_ts_ms = now,
                __source_table = $"{eventType.ToLowerInvariant()}_outbox",
                __source_name = "old_to_new"
            });

            return new ConsumeResult<Ignore, string>
            {
                Message = new Message<Ignore, string> { Value = payload }
            };
        }

        public static List<List<PriorityDependency>> GetFullPriorityList()
        {
            return new List<List<PriorityDependency>>
            {
                // Invoice -> InvoiceLine: InvoiceLine zależy od Invoice, polem ID jest "InvoiceId"
                new List<PriorityDependency> {
                    new PriorityDependency("Invoice", "InvoiceId"),
                    new PriorityDependency("InvoiceLine", "InvoiceId")
                },
                // Customer -> Invoice: Invoice zależy od Customer, polem ID jest "CustomerId"
                new List<PriorityDependency> {
                    new PriorityDependency("Customer", "CustomerId"),
                    new PriorityDependency("Invoice", "CustomerId")
                }
            };
        }

        public static List<PriorityDependency> GetPartialPriorityList()
        {
            return
                new List<PriorityDependency> {
                    new PriorityDependency("Invoice", "InvoiceId"),
                    new PriorityDependency("InvoiceLine", "InvoiceId")
                };
        }
    }
}
