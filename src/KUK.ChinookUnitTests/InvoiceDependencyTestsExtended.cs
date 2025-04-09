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
    /// <summary>
    /// Tests verifying dependency waiting and event sorting behavior.
    /// </summary>
    public class InvoiceDependencyTestsExtended
    {
        private readonly EventsSortingService _service;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _memoryCache;
        private readonly Mock<IInvoiceService> _invoiceServiceMock;
        private readonly Mock<ICustomerService> _customerServiceMock;
        private readonly Mock<IAddressService> _addressServiceMock;
        private readonly Mock<ILogger<EventsSortingService>> _loggerMock;
        private readonly Mock<IDomainDependencyService> _domainDependencyService;

        public InvoiceDependencyTestsExtended()
        {
            _loggerMock = new Mock<ILogger<EventsSortingService>>();
            _invoiceServiceMock = new Mock<IInvoiceService>();
            _customerServiceMock = new Mock<ICustomerService>();
            _addressServiceMock = new Mock<IAddressService>();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { "InternalKafkaProcessorParameters:MemoryCacheExpirationInSeconds", "60" },
                { "InternalKafkaProcessorParameters:EventSortingServiceMaxWaitTimeInSeconds", "2" },
                { "InternalKafkaProcessorParameters:EventSortingServiceAdditionalResultConsumeTimeInMilliseconds", "100" },
                { "InternalKafkaProcessorParameters:EventSortingServiceDelayInMilliseconds", "50" }
            }).Build();

            _domainDependencyService = new Mock<IDomainDependencyService>();
            _service = new EventsSortingService(
                _loggerMock.Object, _memoryCache, _configuration, _domainDependencyService.Object);
        }

        [Fact]
        public async Task WaitForDependencyEventAsync_ShouldReturnTrue_WhenMatchingEventAppearsInBuffer()
        {
            // Arrange: Create an event that expects dependency of type "INVOICE" with expectedAggregateId "500"
            string aggregateId = "SomeAggregate";
            string dependencyType = "INVOICE";
            string expectedDependencyAggregateId = "500";
            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            // Global buffer zawiera już matching event:
            var invoiceEvent = TestHelpers.CreateEvent("INVOICE", "500");
            var eventsToProcess = new List<EventMessage> { invoiceEvent };
            var deferredKafkaEvents = new List<EventMessage>();

            // Setup consumerBuffer: nie zwraca dodatkowych eventów
            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            consumerBufferMock.Setup(c => c.Consume(It.IsAny<TimeSpan>())).Returns(() => null);

            // Act
            bool result = await InvokeWaitForDependencyEventAsync(aggregateId, dependencyType, expectedDependencyAggregateId, consumerBufferMock.Object, consumedResults, eventsToProcess, deferredKafkaEvents);

            // Assert
            Assert.True(result, "Expected WaitForDependencyEventAsync to return true when matching INVOICE event is already in buffer.");
        }

        [Fact]
        public async Task WaitForDependencyEventAsync_ShouldReturnFalse_WhenNoMatchingEventAppears()
        {
            // Arrange: Create an event that expects dependency "INVOICE" with expectedAggregateId "500"
            string aggregateId = "SomeAggregate";
            string dependencyType = "INVOICE";
            string expectedDependencyAggregateId = "500";
            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var eventsToProcess = new List<EventMessage>(); // global buffer is empty
            var deferredKafkaEvents = new List<EventMessage>();

            // Setup consumerBuffer: always returns non-matching event
            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            consumerBufferMock.Setup(c => c.Consume(It.IsAny<TimeSpan>()))
                .Returns(() => TestHelpers.CreateConsumeResult("INVOICELINE", "999"));

            // Act
            bool result = await InvokeWaitForDependencyEventAsync(aggregateId, dependencyType, expectedDependencyAggregateId, consumerBufferMock.Object, consumedResults, eventsToProcess, deferredKafkaEvents);

            // Assert
            Assert.False(result, "Expected WaitForDependencyEventAsync to return false when no matching event appears.");
        }

        [Fact]
        public void SortEvents_ShouldOrderInvoiceBeforeInvoiceLine()
        {
            // Arrange: Create an INVOICE event and two INVOICELINE events.
            var invoiceEvent = TestHelpers.CreateEvent("INVOICE", "500");
            var invoiceLineEvent1 = TestHelpers.CreateEvent("INVOICELINE", "500", "500");
            var invoiceLineEvent2 = TestHelpers.CreateEvent("INVOICELINE", "500", "501");

            var events = new List<EventMessage> { invoiceLineEvent1, invoiceEvent, invoiceLineEvent2 };
            var priorityLists = TestHelpers.GetFullPriorityList();

            // Act: Invoke SortEvents.
            var sortedEvents = _service.SortEvents(events, priorityLists, 0);

            // Assert: Invoice event should be earlier.
            int invoiceIndex = sortedEvents.FindIndex(e => TestHelpers.ExtractEventType(e.Payload)
                .Equals("INVOICE", StringComparison.InvariantCultureIgnoreCase));
            int invoiceLineIndex = sortedEvents.FindIndex(e => TestHelpers.ExtractEventType(e.Payload)
                .Equals("INVOICELINE", StringComparison.InvariantCultureIgnoreCase));

            Assert.True(invoiceIndex >= 0 && invoiceLineIndex >= 0, "Both INVOICE and INVOICELINE events should be present.");
            Assert.True(invoiceIndex < invoiceLineIndex, "INVOICE event should be sorted before INVOICELINE events.");
        }

        // Metoda pomocnicza do bezpośredniego wywołania WaitForDependencyEventAsync
        private async Task<bool> InvokeWaitForDependencyEventAsync(
            string aggregateId,
            string dependencyType,
            string expectedDependencyAggregateId,
            IConsumer<Ignore, string> consumerBuffer,
            List<ConsumeResult<Ignore, string>> consumedResults,
            List<EventMessage> eventsToProcess,
            List<EventMessage> deferredKafkaEvents)
        {
            return await _service.WaitForDependencyEventAsync(
                aggregateId,
                dependencyType,
                expectedDependencyAggregateId,
                "OLD_TO_NEW",
                consumerBuffer,
                consumedResults,
                eventsToProcess,
                Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceDelayInMilliseconds"]),
                Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceAdditionalResultConsumeTimeInMilliseconds"]),
                Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceMaxWaitTimeInSeconds"]),
                deferredKafkaEvents,
                CancellationToken.None);
        }
    }
}
