using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using KUK.KafkaProcessor.EventProcessing;
using KUK.KafkaProcessor.Services;
using KUK.KafkaProcessor.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KUK.UnitTests
{
    public class EventsSortingServiceExtractDependencyTests
    {
        private readonly Mock<ILogger<EventsSortingService>> _loggerMock;
        private readonly Mock<IInvoiceService> _invoiceServiceMock;
        private readonly Mock<ICustomerService> _customerServiceMock;
        private readonly Mock<IAddressService> _addressServiceMock;
        private readonly IMemoryCache _memoryCache;
        private readonly IConfiguration _configuration;
        private readonly EventsSortingService _service;

        public EventsSortingServiceExtractDependencyTests()
        {
            _loggerMock = new Mock<ILogger<EventsSortingService>>();
            _invoiceServiceMock = new Mock<IInvoiceService>();
            _customerServiceMock = new Mock<ICustomerService>();
            _addressServiceMock = new Mock<IAddressService>();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "InternalKafkaProcessorParameters:EventSortingServiceDelayInMilliseconds", "50" },
                    { "InternalKafkaProcessorParameters:EventSortingServiceAdditionalResultConsumeTimeInMilliseconds", "100" },
                    { "InternalKafkaProcessorParameters:EventSortingServiceMaxWaitTimeInSeconds", "5" },
                    { "InternalKafkaProcessorParameters:MemoryCacheExpirationInSeconds", "300" }
                })
                .Build();

            _service = new EventsSortingService(_loggerMock.Object, _invoiceServiceMock.Object, _customerServiceMock.Object, _addressServiceMock.Object, _memoryCache, _configuration);
        }

        [Fact]
        public void ExtractCustomerDependencyFromInvoice_ShouldReturnCustomerId()
        {
            // Arrange
            // Tworzymy payloady dla Invoice zawierające CustomerId podobnie jak w logach
            string payload = @"{
                ""event_id"": ""2"",
                ""aggregate_id"": ""414"",
                ""aggregate_type"": ""INVOICE"",
                ""event_type"": ""CREATED"",
                ""payload"": ""{\""Total\"":\""100.00\"",\""InvoiceId\"":\""414\"",\""CustomerId\"":\""60\"",\""BillingCity\"":\""Anytown\"",\""InvoiceDate\"":\""2025-03-26 12:24:56.000000\"",\""BillingState\"":null,\""BillingAddress\"":\""123 Main St (added from nested query)\"",\""BillingCountry\"":\""USA\"",\""BillingPostalCode\"":null}"",
                ""unique_identifier"": ""53d65484-0a3d-11f0-9a9b-e268f5e74164"",
                ""created_at"": ""2025-03-26T12:24:56.347529Z"",
                ""__deleted"": ""false"",
                ""__op"": ""c"",
                ""__source_ts_ms"": 1742991896446,
                ""__source_table"": ""invoice_outbox"",
                ""__source_name"": ""old_to_new""
            }";

            // Act - wywołujemy nową metodę ExtractDependencyId
            string customerId = _service.ExtractDependencyId(payload, "CustomerId");

            // Assert
            Assert.Equal("60", customerId);
        }

        [Fact]
        public void ExtractDependencyAggregateId_ShouldReturnAggregateId_WhenInvoiceLineEventProvided()
        {
            // Arrange
            string payload = @"{
                ""event_id"": ""539a3e86-d2d7-49d6-9eb0-7f3289886264"",
                ""aggregate_id"": ""2b78acbd-7afd-46f1-bb60-268f1be20d93"",
                ""aggregate_type"": ""INVOICELINE"",
                ""event_type"": ""CREATED"",
                ""payload"": ""{\""TrackId\"":\""1\"",\""Quantity\"":\""1\"",\""InvoiceId\"":\""2b78acbd-7afd-46f1-bb60-268f1be20d93\"",\""UnitPrice\"":\""1\"",\""InvoiceLineId\"":\""1a9d7043-1a2d-4e47-9475-919565208738\""}"",
                ""unique_identifier"": ""fd751d94-c179-4174-81e4-341e1d41c0cb"",
                ""created_at"": ""2025-03-25T08:04:41.119974Z"",
                ""__deleted"": ""false"",
                ""__op"": ""c"",
                ""__source_ts_ms"": 1742889881121,
                ""__source_table"": ""InvoiceLineOutbox"",
                ""__source_name"": ""new_to_old""
            }";

            // Act
            string result = _service.ExtractDependencyAggregateId(payload);

            // Assert
            Assert.Equal("2b78acbd-7afd-46f1-bb60-268f1be20d93", result);
        }


        [Fact]
        public void ExtractDependencyAggregateId_ShouldReturnEmptyString_WhenPayloadIsInvalid()
        {
            // Arrange
            string invalidPayload = "invalid json";

            // Act
            string result = _service.ExtractDependencyAggregateId(invalidPayload);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void ExtractDependencyAggregateId_ShouldReturnEmptyString_WhenAggregateIdIsMissing()
        {
            // Arrange
            string payload = @"{
                ""event_id"": ""539a3e86-d2d7-49d6-9eb0-7f3289886264"",
                ""event_type"": ""CREATED"",
                ""payload"": ""{}"",
                ""__source_name"": ""new_to_old""
            }";

            // Act
            string result = _service.ExtractDependencyAggregateId(payload);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public async Task ProcessInvoiceLine_DoesNotSetCache_WhenInvoiceMappingNotFound()
        {
            // Arrange – simulate an invoice line event expecting dependency "INVOICE" with expected ID "417".
            _invoiceServiceMock.Setup(s => s.MappingExists(It.IsAny<int>())).ReturnsAsync(false);

            // Create an invoice line event where dependencyAggregateId is "417".
            var invoiceLineEvent = TestHelpers.CreateEvent("INVOICELINE", "417", "someAggregate");
            var eventsToProcess = new List<EventMessage> { invoiceLineEvent };
            var deferredKafkaEvents = new List<EventMessage>();

            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            // Consumer does not return any additional event.
            consumerBufferMock.Setup(c => c.Consume(It.IsAny<TimeSpan>())).Returns(() => null);
            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var priorityGroup = TestHelpers.GetPartialPriorityList();

            // Act - calling the public method directly
            await _service.EnsureDependencyForEventAsync(
                invoiceLineEvent,
                priorityGroup,
                eventsToProcess,
                consumerBufferMock.Object,
                consumedResults,
                Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceMaxWaitTimeInSeconds"]),
                Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceAdditionalResultConsumeTimeInMilliseconds"]),
                Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceDelayInMilliseconds"]),
                deferredKafkaEvents,
                CancellationToken.None);

            // Assert – since no invoice mapping is found, cache should NOT be set for key "INVOICE:417".
            bool cacheHit = _memoryCache.TryGetValue("INVOICE:417", out _);
            Assert.False(cacheHit, "Expected no cache entry for 'INVOICE:417' when invoice mapping is not found.");
        }
    }
} 