using Confluent.Kafka;
using KUK.ChinookSync.Services.Domain.Interfaces;
using KUK.KafkaProcessor.EventProcessing;
using KUK.KafkaProcessor.Services;
using KUK.KafkaProcessor.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace KUK.ChinookUnitTests
{
    public class EventsSortingServiceDependencyOrderTests
    {
        private readonly Mock<ILogger<EventsSortingService>> _loggerMock;
        private readonly Mock<IInvoiceService> _invoiceServiceMock;
        private readonly Mock<ICustomerService> _customerServiceMock;
        private readonly Mock<IAddressService> _addressServiceMock;
        private readonly IMemoryCache _memoryCache;
        private readonly IConfiguration _configuration;
        private readonly Mock<IDomainDependencyService> _domainDependencyService;
        private readonly EventsSortingService _service;

        public EventsSortingServiceDependencyOrderTests()
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

            _domainDependencyService = new Mock<IDomainDependencyService>();
            _service = new EventsSortingService(
                _loggerMock.Object, _memoryCache, _configuration, _domainDependencyService.Object);
        }

        [Fact]
        public async void EnsureDependencies_ShouldProcessOnlyMatchingDependencies()
        {
            // We create the main INVOICELINE event for INVOICEID=1
            string firstInvoiceAggregateId = "1";
            string secondInvoiceAggregateId = "2";
            var mainEvent = TestHelpers.CreateEvent("INVOICELINE", firstInvoiceAggregateId, "line1");

            // We prepare a buffer with two events:
            // 1. INVOICELINE for INVOICEID=2 (should not be included)
            // 2. INVOICE for INVOICEID=1 (should be included)
            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            
            // We simulate the order of events in the buffer
            consumerBufferMock.SetupSequence(c => c.Consume(It.IsAny<TimeSpan>()))
                .Returns(TestHelpers.CreateConsumeResult("INVOICELINE", secondInvoiceAggregateId)) // Unrelated INVOICELINE
                .Returns(TestHelpers.CreateConsumeResult("INVOICE", firstInvoiceAggregateId)) // Related INVOICE
                .Returns(() => null);

            var eventsToProcess = new List<EventMessage> { mainEvent };
            var priorityLists = TestHelpers.GetFullPriorityList();

            // Act
            var deferredKafkaEvents = new List<EventMessage>();
            var result = await _service.EnsureDependenciesAsync(
                eventsToProcess,
                priorityLists,
                consumerBufferMock.Object,
                consumedResults,
                deferredKafkaEvents,
                CancellationToken.None);

            // Assert
            Assert.Equal(2, result.Count); // There should be 2 events: INVOICE and INVOICELINE for ID=1

            // Check if we have the correct INVOICE
            var invoice = result.Find(e => TestHelpers.ExtractEventType(e.Payload) == "INVOICE");
            Assert.NotNull(invoice);
            Assert.Equal(firstInvoiceAggregateId, TestHelpers.ExtractAggregateId(invoice.Payload));

            // Check if we have the correct INVOICELINE
            var invoiceLine = result.Find(e => TestHelpers.ExtractEventType(e.Payload) == "INVOICELINE");
            Assert.NotNull(invoiceLine);
            Assert.Equal(firstInvoiceAggregateId, TestHelpers.ExtractAggregateId(invoiceLine.Payload));

            // Check if the unrelated INVOICELINE is not included
            Assert.DoesNotContain(result, e => 
                TestHelpers.ExtractEventType(e.Payload) == "INVOICELINE" && 
                TestHelpers.ExtractAggregateId(e.Payload) == secondInvoiceAggregateId);
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