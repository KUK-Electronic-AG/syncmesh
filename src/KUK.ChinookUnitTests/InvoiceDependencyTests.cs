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
    /// Tests verifying invoice dependency, event ordering and data consistency.
    /// </summary>
    public class InvoiceDependencyTests
    {
        private readonly EventsSortingService _service;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _memoryCache;
        private readonly Mock<IInvoiceService> _invoiceServiceMock;
        private readonly Mock<ICustomerService> _customerServiceMock;
        private readonly Mock<IAddressService> _addressServiceMock;
        private readonly Mock<ILogger<EventsSortingService>> _loggerMock;
        private readonly Mock<IDomainDependencyService> _domainDependencyService;

        public InvoiceDependencyTests()
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

        /// <summary>
        /// Test to verify that if a correct invoice event exists before an invoice line event,
        /// the dependency is satisfied and the mapping (cache entry) is set.
        /// </summary>
        [Fact]
        public async Task CorrectInvoiceEventMappingTest()
        {
            // Arrange:
            // Create an invoice event with aggregate_id "500" (representing the mapping)
            var invoiceEvent = TestHelpers.CreateEvent("INVOICE", "500");
            // Create an invoice line event that depends on invoice "500".
            var invoiceLineEvent = TestHelpers.CreateEvent("INVOICELINE", "500", "XYZ");

            // Simulate proper ordering: invoice event is in eventsToProcess already.
            var eventsToProcess = new List<EventMessage> { invoiceEvent, invoiceLineEvent };

            // Clear cache.
            _memoryCache.Remove("INVOICE:500");

            // Setup consumerBuffer to return no additional event (dependency is already in events list).
            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            consumerBufferMock.Setup(c => c.Consume(It.IsAny<TimeSpan>())).Returns(() => null);
            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var priorityGroup = TestHelpers.GetPartialPriorityList();

            // Act: Process the invoice line event.
            await _service.EnsureDependencyForEventAsync(
                invoiceLineEvent,
                priorityGroup,
                eventsToProcess,
                consumerBufferMock.Object,
                consumedResults,
                Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceDelayInMilliseconds"]),
                Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceAdditionalResultConsumeTimeInMilliseconds"]),
                Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceMaxWaitTimeInSeconds"]),
                new List<EventMessage>(),  // deferredKafkaEvents
                CancellationToken.None);

            // Assert: Expect mapping to be set for key "INVOICE:500".
            bool cacheHit = _memoryCache.TryGetValue("INVOICE:500", out object cacheValue);
            Assert.True(cacheHit, "Expected cache to contain key 'INVOICE:500' when correct invoice event is present.");
            Assert.IsType<bool>(cacheValue);
            Assert.True((bool)cacheValue, "Cache value for 'INVOICE:500' should be true.");
        }

        /// <summary>
        /// Test to verify that if the invoice line event's inner payload does not contain the correct InvoiceId,
        /// dependency check fails and mapping is not set.
        /// </summary>
        [Fact]
        public async Task IncorrectInvoiceEventMappingTest_ShouldNotSetCache()
        {
            // Arrange:
            // Create an invoice line event with dependencyAggregateId set incorrectly (e.g. "999" instead of expected "500").
            var invoiceLineEvent = TestHelpers.CreateEvent("INVOICELINE", "999", "XYZ");
            var eventsToProcess = new List<EventMessage> { invoiceLineEvent };

            // Clear cache.
            _memoryCache.Remove("INVOICE:500");

            // Setup consumerBuffer to always return non-matching additional events.
            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            consumerBufferMock.Setup(c => c.Consume(It.IsAny<TimeSpan>()))
                .Returns(() => TestHelpers.CreateConsumeResult("INVOICE", "999"));
            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var priorityGroup = TestHelpers.GetPartialPriorityList();

            // Act: Process the event.
            await _service.EnsureDependencyForEventAsync(
                invoiceLineEvent,
                priorityGroup,
                eventsToProcess,
                consumerBufferMock.Object,
                consumedResults,
                Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceDelayInMilliseconds"]),
                Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceAdditionalResultConsumeTimeInMilliseconds"]),
                Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceMaxWaitTimeInSeconds"]),
                new List<EventMessage>(),  // deferredKafkaEvents
                CancellationToken.None);

            // Assert: Since the correct invoice event never arrives (or inner payload is incorrect), mapping should not be set.
            bool cacheHit = _memoryCache.TryGetValue("INVOICE:500", out _);
            Assert.False(cacheHit, "Expected no cache entry for 'INVOICE:500' when invoice line event's dependency data is incorrect.");
        }

        /// <summary>
        /// Test to verify that if the invoice event is missing (or delayed) the dependency check times out,
        /// and mapping is not set.
        /// </summary>
        [Fact]
        public async Task DependencyTimeoutTest_ShouldNotSetCache_WhenInvoiceEventDoesNotArrive()
        {
            // Arrange:
            // Create an invoice line event expecting dependency "INVOICE" with expected value "500".
            var invoiceLineEvent = TestHelpers.CreateEvent("INVOICELINE", "500", "XYZ");
            var eventsToProcess = new List<EventMessage> { invoiceLineEvent };

            // Clear cache.
            _memoryCache.Remove("INVOICE:500");

            // Podmień konfigurację tylko na czas testu
            var testConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "InternalKafkaProcessorParameters:EventSortingServiceDelayInMilliseconds", "0.1" },
                    { "InternalKafkaProcessorParameters:EventSortingServiceAdditionalResultConsumeTimeInMilliseconds", "0.1" },
                    { "InternalKafkaProcessorParameters:EventSortingServiceMaxWaitTimeInSeconds", "0.5" }
                })
                .Build();

            // Setup consumerBuffer to always return non-matching events.
            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            consumerBufferMock
                .Setup(c => c.Consume(It.IsAny<TimeSpan>()))
                .Returns<TimeSpan>(ts =>
                {
                    return TestHelpers.CreateConsumeResult("INVOICE", "999");
                });
            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var priorityGroup = TestHelpers.GetPartialPriorityList();

            // Act: Process the event with a short max wait time.
            await _service.EnsureDependencyForEventAsync(
                invoiceLineEvent,
                priorityGroup,
                eventsToProcess,
                consumerBufferMock.Object,
                consumedResults,
                Convert.ToDouble(testConfiguration["InternalKafkaProcessorParameters:EventSortingServiceDelayInMilliseconds"]),
                Convert.ToDouble(testConfiguration["InternalKafkaProcessorParameters:EventSortingServiceAdditionalResultConsumeTimeInMilliseconds"]),
                1.0, // Very short wait time to force timeout.
                new List<EventMessage>(),  // deferredKafkaEvents
                CancellationToken.None);

            // Assert: Mapping should not be set since no matching invoice event arrives.
            bool cacheHit = _memoryCache.TryGetValue("INVOICE:500", out _);
            Assert.False(cacheHit, "Expected no cache entry for 'INVOICE:500' when invoice event does not arrive within timeout.");
        }

        /// <summary>
        /// Test to verify that sorting orders invoice events before invoice line events.
        /// (Assuming SortEvents is available in the service.)
        /// </summary>
        [Fact]
        public void EventOrderingTest_ShouldOrderInvoiceBeforeInvoiceLine()
        {
            // Arrange:
            // Create a list of events: some invoice events and some invoice line events.
            var invoiceEvent = TestHelpers.CreateEvent("INVOICE", "500");
            var invoiceLineEvent1 = TestHelpers.CreateEvent("INVOICELINE", "500", "XYZ1");
            var invoiceLineEvent2 = TestHelpers.CreateEvent("INVOICELINE", "500", "XYZ2");
            var priorityList = TestHelpers.GetFullPriorityList();

            var events = new List<EventMessage> { invoiceLineEvent1, invoiceEvent, invoiceLineEvent2 };

            // Act:
            // Assume the service has a method SortEvents to sort events according to dependency priority.
            // (Tutaj trzeba dostosować do faktycznej implementacji sortowania w Twoim serwisie.)
            var sortedEvents = _service.SortEvents(events, priorityList, 0);

            // Assert: Invoice event should come before any invoice line events.
            int invoiceIndex = sortedEvents.FindIndex(e => TestHelpers.ExtractEventType(e.Payload).Equals("INVOICE", StringComparison.InvariantCultureIgnoreCase));
            int invoiceLineIndex = sortedEvents.FindIndex(e => TestHelpers.ExtractEventType(e.Payload).Equals("INVOICELINE", StringComparison.InvariantCultureIgnoreCase));
            Assert.True(invoiceIndex >= 0 && invoiceLineIndex >= 0, "Both invoice and invoice line events should be present.");
            Assert.True(invoiceIndex < invoiceLineIndex, "Invoice event should be sorted before invoice line events.");
        }
    }
}
