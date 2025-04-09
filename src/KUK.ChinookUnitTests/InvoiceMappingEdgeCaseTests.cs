using Confluent.Kafka;
using KUK.ChinookSync.Services.Domain;
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
    /// Tests for invoice mapping related edge cases.
    /// </summary>
    public class InvoiceMappingEdgeCaseTests
    {
        private readonly Mock<ILogger<EventsSortingService>> _loggerMock;
        private readonly Mock<IInvoiceService> _invoiceServiceMock;
        private readonly Mock<ICustomerService> _customerServiceMock;
        private readonly Mock<IAddressService> _addressServiceMock;
        private readonly IMemoryCache _memoryCache;
        private readonly IConfiguration _configuration;
        private readonly IDomainDependencyService _domainDependencyService;
        private readonly EventsSortingService _service;

        public InvoiceMappingEdgeCaseTests()
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

            var domainDependencyServiceLoggerMock = new Mock<ILogger<DomainDependencyService>>();
            _domainDependencyService = new DomainDependencyService(domainDependencyServiceLoggerMock.Object, _invoiceServiceMock.Object, _customerServiceMock.Object, _addressServiceMock.Object);
            _service = new EventsSortingService(
                _loggerMock.Object, _memoryCache, _configuration, _domainDependencyService);
        }

        /// <summary>
        /// Test that if the dependency (mapping) is already cached, the dependency check returns immediately.
        /// </summary>
        [Fact]
        public async Task MappingAlreadyCachedTest()
        {
            // Arrange
            var invoiceEvent = TestHelpers.CreateEvent("INVOICE", "417");
            var invoiceLineEvent = TestHelpers.CreateEvent("INVOICELINE", "417", "someAggregate");
            var eventsToProcess = new List<EventMessage> { invoiceEvent, invoiceLineEvent };

            // Pre-set cache with mapping (simulate that invoice mapping for OldInvoiceId 417 exists)
            _memoryCache.Set("INVOICE:417", true, TimeSpan.FromSeconds(60));

            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var priorityGroup = TestHelpers.GetPartialPriorityList();
            var deferredKafkaEvents = new List<EventMessage>();

            // Act
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

            // Assert – since mapping is cached, cache entry should remain and no additional events should be processed.
            bool cacheHit = _memoryCache.TryGetValue("INVOICE:417", out object cacheValue);
            Assert.True(cacheHit, "Expected cache to contain key 'INVOICE:417' because mapping was already cached.");
            Assert.IsType<bool>(cacheValue);
            Assert.True((bool)cacheValue, "Cache value for key 'INVOICE:417' should be true.");
        }

        /// <summary>
        /// Test that when dependency is not in buffer or cache but external check returns true,
        /// the mapping is set in cache.
        /// </summary>
        [Fact]
        public async Task MappingExternalCheckTest()
        {
            // Arrange – dependency not in buffer, external check returns true.
            _invoiceServiceMock.Setup(s => s.MappingExists(It.IsAny<int>())).ReturnsAsync(true);

            var invoiceLineEvent = TestHelpers.CreateEvent("INVOICELINE", "417", "someAggregate");
            var eventsToProcess = new List<EventMessage> { invoiceLineEvent };

            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            // No additional event returned.
            consumerBufferMock.Setup(c => c.Consume(It.IsAny<TimeSpan>())).Returns(() => null);
            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var priorityGroup = TestHelpers.GetPartialPriorityList();
            var deferredKafkaEvents = new List<EventMessage>();

            // Act
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

            // Assert – external check should have set mapping in cache.
            bool cacheHit = _memoryCache.TryGetValue("INVOICE:417", out _);
            Assert.True(cacheHit, "Expected cache to contain key 'INVOICE:417' when external dependency check returns true.");
        }

        /// <summary>
        /// Test that when additional events are processed, and a matching event arrives after several non-matching ones,
        /// the dependency is eventually satisfied.
        /// </summary>
        [Fact]
        public async Task WaitForDependencyEventAsync_ReturnsTrue_WhenMatchingEventArrivesAfterSeveralNonMatching()
        {
            // Arrange – first two events do not match, third matches.
            string aggregateId = "222";
            string dependencyType = "INVOICE";
            string expectedDependencyAggregateId = "417";

            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var eventsToProcess = new List<EventMessage>();
            var deferredKafkaEvents = new List<EventMessage>();

            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            consumerBufferMock
                .SetupSequence(c => c.Consume(It.IsAny<TimeSpan>()))
                .Returns(TestHelpers.CreateConsumeResult("INVOICE", "999"))  // Non-matching
                .Returns(TestHelpers.CreateConsumeResult("INVOICE", "888"))  // Non-matching
                .Returns(TestHelpers.CreateConsumeResult("INVOICE", "417"))  // Matching
                .Returns(() => null);

            bool result = await _service.WaitForDependencyEventAsync(
                aggregateId,
                dependencyType,
                expectedDependencyAggregateId,
                "OLD_TO_NEW",
                consumerBufferMock.Object,
                consumedResults,
                eventsToProcess,
                Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceDelayInMilliseconds"]),
                Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceAdditionalResultConsumeTimeInMilliseconds"]),
                Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceMaxWaitTimeInSeconds"]),
                deferredKafkaEvents,
                CancellationToken.None);

            Assert.True(result, "Expected WaitForDependencyEventAsync to eventually return true after a matching event arrives.");
        }

        /// <summary>
        /// Test that WaitForDependencyEventAsync returns false when no matching event arrives within the timeout.
        /// </summary>
        [Fact]
        public async Task WaitForDependencyEventAsync_ReturnsFalse_WhenNoMatchingEventArrivesWithinTimeout()
        {
            // Arrange – no matching events arrive.
            string aggregateId = "222";
            string dependencyType = "INVOICE";
            string expectedDependencyAggregateId = "417";

            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var eventsToProcess = new List<EventMessage>();
            var deferredKafkaEvents = new List<EventMessage>();

            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            consumerBufferMock
                .SetupSequence(c => c.Consume(It.IsAny<TimeSpan>()))
                .Returns(TestHelpers.CreateConsumeResult("INVOICE", "999"))  // Non-matching
                .Returns(TestHelpers.CreateConsumeResult("INVOICE", "888"))  // Non-matching
                .Returns(() => null);

            bool result = await _service.WaitForDependencyEventAsync(
                aggregateId,
                dependencyType,
                expectedDependencyAggregateId,
                "OLD_TO_NEW",
                consumerBufferMock.Object,
                consumedResults,
                eventsToProcess,
                Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceDelayInMilliseconds"]),
                Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceAdditionalResultConsumeTimeInMilliseconds"]),
                Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceMaxWaitTimeInSeconds"]),
                deferredKafkaEvents,
                CancellationToken.None);

            Assert.False(result, "Expected WaitForDependencyEventAsync to return false when no matching event arrives within timeout.");
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

            // Act
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
