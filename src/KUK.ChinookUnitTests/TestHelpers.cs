using System.Text.Json;
using Confluent.Kafka;
using KUK.ChinookSync.Services.Domain.Interfaces;
using KUK.KafkaProcessor.EventProcessing;
using KUK.KafkaProcessor.Services;
using KUK.KafkaProcessor.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;

namespace KUK.ChinookUnitTests
{
    public static class TestHelpers
    {
        public static IEventsSortingService CreateEventSortingService(
            ILogger<EventsSortingService> logger = null,
            IInvoiceService invoiceService = null,
            ICustomerService customerService = null,
            IAddressService addressService = null,
            IMemoryCache memoryCache = null,
            IConfiguration configuration = null,
            IDomainDependencyService domainDependencyService = null)
        {
            logger ??= new LoggerFactory().CreateLogger<EventsSortingService>();
            invoiceService ??= new Mock<IInvoiceService>().Object;
            customerService ??= new Mock<ICustomerService>().Object;
            addressService ??= new Mock<IAddressService>().Object;
            memoryCache ??= new MemoryCache(new MemoryCacheOptions());
            addressService ??= new Mock<IAddressService>().Object;
            domainDependencyService ??= new Mock<IDomainDependencyService>().Object;

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

            return new EventsSortingService(logger, memoryCache, configuration, domainDependencyService);
        }

        public static EventMessage CreateEvent(
            string eventType,
            string invoiceId,
            string invoiceLineId = null,
            string source = "OLD_TO_NEW")
        {
            var now = DateTime.UtcNow;
            var nowUnix = new DateTimeOffset(now).ToUnixTimeMilliseconds();

            // We create inner payload as JSON object
            var innerPayload = new JObject();
            innerPayload["InvoiceId"] = invoiceId;
            if (eventType.ToUpperInvariant() == "INVOICELINE")
            {
                innerPayload["InvoiceLineId"] = invoiceLineId;
            }
            innerPayload["Source"] = source;

            // We build outer payload
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
            // We create inner payload depending on the event type
            string innerPayload;
            if (eventType.Equals("INVOICE", StringComparison.InvariantCultureIgnoreCase))
            {
                // For INVOICE we set only InvoiceId equal to aggregateId
                innerPayload = JsonSerializer.Serialize(new { InvoiceId = aggregateId });
            }
            else if (eventType.Equals("INVOICELINE", StringComparison.InvariantCultureIgnoreCase))
            {
                // For INVOICELINE we set InvoiceId equal to aggregateId and we generate new InvoiceLineId identificator
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
                // For CUSTOMER we set CustomerId equal to aggregateId
                innerPayload = JsonSerializer.Serialize(new { CustomerId = aggregateId });
            }
            else
            {
                // For other types we can set empty payload or adjust the logic
                innerPayload = "{}";
            }

            // We generate outer payload in which payload field contains our inner payload
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
                // Invoice -> InvoiceLine: InvoiceLine depends on Invoice, "InvoiceId" is ID field
                new List<PriorityDependency> {
                    new PriorityDependency("Invoice", "InvoiceId"),
                    new PriorityDependency("InvoiceLine", "InvoiceId")
                },
                // Customer -> Invoice: Invoice depends on Customer, "CustomerId" is ID field
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
