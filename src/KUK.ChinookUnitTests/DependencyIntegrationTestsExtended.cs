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
    /// Integration tests for event ordering and invoice mapping dependency.
    /// These tests simulate different scenarios:
    /// - Proper ordering (invoice event precedes invoice line event)
    /// - Inverse ordering (invoice event is missing)
    /// - Mixed events for data integrity.
    /// </summary>
    public class DependencyIntegrationTestsExtended
    {
        private readonly EventsSortingService _service;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _memoryCache;
        private readonly Mock<IInvoiceService> _invoiceServiceMock;
        private readonly Mock<ICustomerService> _customerServiceMock;
        private readonly Mock<IAddressService> _addressServiceMock;
        private readonly Mock<IDomainDependencyService> _domainDependencyService;
        private readonly Mock<ILogger<EventsSortingService>> _loggerMock;

        public DependencyIntegrationTestsExtended()
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
        /// Test that simulates proper ordering: the invoice event (mapping) for OldInvoiceId "417" 
        /// is already processed and present in the events list before processing the invoice line event.
        /// In such a case, the dependency check should succeed and the mapping key "INVOICE:417" be set in cache.
        /// </summary>
        [Fact]
        public async Task ProperOrderingTest_ShouldSetMappingCache_WhenInvoiceEventPrecedesInvoiceLine()
        {
            // Arrange
            // Create an invoice event with aggregate_id "417".
            var invoiceEvent = TestHelpers.CreateEvent("INVOICE", "417");
            // Create an invoice line event that depends on invoice "417".
            var invoiceLineEvent = TestHelpers.CreateEvent("INVOICELINE", "417", "someAggregate");

            // Simulate that the invoice event is already processed by placing it in eventsToProcess.
            var eventsToProcess = new List<EventMessage> { invoiceEvent, invoiceLineEvent };

            // Clear cache for key "INVOICE:417".
            _memoryCache.Remove("INVOICE:417");

            // Setup consumerBuffer to return no additional event.
            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            consumerBufferMock.Setup(c => c.Consume(It.IsAny<TimeSpan>())).Returns(() => null);

            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var priorityGroup = TestHelpers.GetPartialPriorityList();

            // Act: Invoke EnsureDependencyForEventAsync on the invoice line event.
            var deferredKafkaEvents = new List<EventMessage>();
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

            // Assert: mapping should be set in cache for key "INVOICE:417".
            bool cacheHit = _memoryCache.TryGetValue("INVOICE:417", out object cacheValue);
            Assert.True(cacheHit, "Expected cache to contain key 'INVOICE:417' when invoice event precedes invoice line.");
            Assert.IsType<bool>(cacheValue);
            Assert.True((bool)cacheValue, "Cache value for 'INVOICE:417' should be true.");
        }

        /// <summary>
        /// Test that simulates inverse ordering: the invoice line event is processed without the corresponding invoice event.
        /// In this case, dependency is not satisfied and mapping key "INVOICE:417" should not be set.
        /// </summary>
        [Fact]
        public async Task InverseOrderingTest_ShouldNotSetMappingCache_WhenInvoiceEventIsMissing()
        {
            // Arrange
            // Create only an invoice line event that depends on invoice "417".
            var invoiceLineEvent = TestHelpers.CreateEvent("INVOICELINE", "417", "someAggregate");
            var eventsToProcess = new List<EventMessage> { invoiceLineEvent };

            // Clear cache.
            _memoryCache.Remove("INVOICE:417");

            // Setup consumerBuffer to always return non-matching events.
            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            consumerBufferMock.Setup(c => c.Consume(It.IsAny<TimeSpan>()))
                .Returns(() => TestHelpers.CreateConsumeResult("INVOICE", "999"));  // Non-matching

            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var priorityGroup = TestHelpers.GetPartialPriorityList();

            // Act: Invoke dependency check.
            var deferredKafkaEvents = new List<EventMessage>();
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

            // Assert: mapping should not be set because the invoice event never arrives.
            bool cacheHit = _memoryCache.TryGetValue("INVOICE:417", out _);
            Assert.False(cacheHit, "Expected no cache entry for 'INVOICE:417' when invoice event is missing.");
        }

        /// <summary>
        /// Test that simulates a retry mechanism:
        /// multiple additional events are processed and eventually a matching event arrives.
        /// The dependency should be satisfied and the mapping key "INVOICE:417" should be set in cache.
        /// </summary>
        [Fact]
        public async Task RetryMechanismTest_FullFlow_SetsCacheWhenMatchingEventArrives()
        {
            // Arrange: simulate a sequence of non-matching events followed by a matching event.
            string dependencyType = "INVOICE";
            string expectedDependencyAggregateId = "417";

            // Create an invoice line event that depends on invoice "417".
            var invoiceLineEvent = TestHelpers.CreateEvent("INVOICELINE", "417", "222");
            // Start with only the invoice line event in eventsToProcess.
            var eventsToProcess = new List<EventMessage> { invoiceLineEvent };

            // Ensure cache is empty.
            _memoryCache.Remove("INVOICE:417");

            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            // Setup consumerBuffer to return two non-matching events, then a matching event.
            consumerBufferMock.SetupSequence(c => c.Consume(It.IsAny<TimeSpan>()))
                .Returns(TestHelpers.CreateConsumeResult("INVOICE", "999"))  // Non-matching
                .Returns(TestHelpers.CreateConsumeResult("INVOICE", "888"))  // Non-matching
                .Returns(TestHelpers.CreateConsumeResult("INVOICE", "417"))  // Matching
                .Returns(() => null);

            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var priorityGroup = TestHelpers.GetPartialPriorityList();

            // Act: Use full flow by calling EnsureDependencyForEventAsync so that cache is set when dependency is satisfied.
            var deferredKafkaEvents = new List<EventMessage>();
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

            // Assert: after processing, cache should contain key "INVOICE:417".
            bool cacheHit = _memoryCache.TryGetValue("INVOICE:417", out object cacheValue);
            Assert.True(cacheHit, "Expected cache to contain key 'INVOICE:417' after dependency is satisfied.");
            Assert.IsType<bool>(cacheValue);
            Assert.True((bool)cacheValue, "Cache value for 'INVOICE:417' should be true after dependency is satisfied.");
        }

        /// <summary>
        /// Test that simulates data integrity for a batch of events:
        /// After processing, each invoice line event should have its corresponding invoice mapping set in cache.
        /// </summary>
        [Fact]
        public async Task DataIntegrityTest_ShouldSetMappingForEachInvoiceLineEvent()
        {
            // Arrange: Create a mix of events, both invoice events and invoice line events.
            var invoiceEvent1 = TestHelpers.CreateEvent("INVOICE", "100");
            var invoiceEvent2 = TestHelpers.CreateEvent("INVOICE", "101");
            var invoiceLineEvent1 = TestHelpers.CreateEvent("INVOICELINE", "100", "A");
            var invoiceLineEvent2 = TestHelpers.CreateEvent("INVOICELINE", "101", "B");
            var eventsToProcess = new List<EventMessage> { invoiceEvent1, invoiceEvent2, invoiceLineEvent1, invoiceLineEvent2 };

            // Clear cache entries.
            _memoryCache.Remove("INVOICE:100");
            _memoryCache.Remove("INVOICE:101");

            // Simulate external check returns true for both dependencies.
            _invoiceServiceMock.Setup(s => s.MappingExists(It.IsAny<int>())).ReturnsAsync(true);

            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            consumerBufferMock.Setup(c => c.Consume(It.IsAny<TimeSpan>())).Returns(() => null);
            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var priorityGroup = TestHelpers.GetPartialPriorityList();

            // Act: Process each invoice line event using the full dependency check.
            var deferredKafkaEvents = new List<EventMessage>();
            
            foreach (var evt in eventsToProcess)
            {
                // Process only invoice line events.
                if (evt.Payload.Contains("INVOICELINE"))
                {
                    await _service.EnsureDependencyForEventAsync(
                        evt,
                        priorityGroup,
                        eventsToProcess,
                        consumerBufferMock.Object,
                        consumedResults,
                        Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceMaxWaitTimeInSeconds"]),
                        Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceAdditionalResultConsumeTimeInMilliseconds"]),
                        Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceDelayInMilliseconds"]),
                        deferredKafkaEvents,
                        CancellationToken.None);
                }
            }

            // Assert: Verify that for each invoice line event, the corresponding mapping key is set.
            bool cacheHit100 = _memoryCache.TryGetValue("INVOICE:100", out object cacheValue100);
            bool cacheHit101 = _memoryCache.TryGetValue("INVOICE:101", out object cacheValue101);
            Assert.True(cacheHit100, "Expected cache to contain key 'INVOICE:100' for invoice line dependent on invoice '100'.");
            Assert.True(cacheHit101, "Expected cache to contain key 'INVOICE:101' for invoice line dependent on invoice '101'.");
            Assert.True((bool)cacheValue100, "Cache value for 'INVOICE:100' should be true.");
            Assert.True((bool)cacheValue101, "Cache value for 'INVOICE:101' should be true.");
        }
    }
}
