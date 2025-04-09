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
    /// Tests to verify the behavior when dependency identifiers are inconsistent.
    /// </summary>
    public class InvoiceDependencyMismatchTests
    {
        private readonly EventsSortingService _service;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _memoryCache;
        private readonly Mock<IInvoiceService> _invoiceServiceMock;
        private readonly Mock<ICustomerService> _customerServiceMock;
        private readonly Mock<IAddressService> _addressServiceMock;
        private readonly Mock<IDomainDependencyService> _domainDependencyService;
        private readonly Mock<ILogger<EventsSortingService>> _loggerMock;

        public InvoiceDependencyMismatchTests()
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
        /// This test simulates a mismatch in dependency identifiers:
        /// The invoice line event is created with dependencyAggregateId = "417",
        /// but the additional invoice event (which should satisfy the dependency) returns an aggregate_id of "0d640540-aabb-4e18-b852-cb679ddaf4a9".
        /// We expect that the dependency check fails (i.e. mapping is not set).
        /// </summary>
        [Fact]
        public async Task DependencyMismatchTest_ShouldFail_WhenDependencyIdentifiersDoNotMatch()
        {
            // Arrange:
            // Create an invoice line event that expects dependencyAggregateId "417"
            var invoiceLineEvent = TestHelpers.CreateEvent("INVOICELINE", "417", "SomeAggregate");
            var eventsToProcess = new List<EventMessage> { invoiceLineEvent };

            // Clear the cache.
            _memoryCache.Remove("INVOICE:417");

            // Setup consumerBuffer to simulate that additional event comes with a different aggregate_id.
            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            consumerBufferMock.SetupSequence(c => c.Consume(It.IsAny<TimeSpan>()))
                .Returns(TestHelpers.CreateConsumeResult("INVOICE", "0d640540-aabb-4e18-b852-cb679ddaf4a9")) // Mismatched dependency
                .Returns(() => null);

            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var priorityGroup = TestHelpers.GetPartialPriorityList();
            var deferredKafkaEvents = new List<EventMessage>();

            // Act: Process the invoice line event.
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

            // Assert: The cache for key "INVOICE:417" should not be set.
            bool cacheHit = _memoryCache.TryGetValue("INVOICE:417", out _);
            Assert.False(cacheHit, "Expected no cache entry for 'INVOICE:417' due to dependency identifier mismatch.");
        }
    }
}
