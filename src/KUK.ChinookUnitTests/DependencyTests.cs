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
    /// Tests for dependency checking logic (WaitForDependencyEventAsync).
    /// </summary>
    public class DependencyTests
    {
        private readonly Mock<ILogger<EventsSortingService>> _loggerMock;
        private readonly Mock<IInvoiceService> _invoiceServiceMock;
        private readonly Mock<ICustomerService> _customerServiceMock;
        private readonly Mock<IAddressService> _addressServiceMock;
        private readonly IMemoryCache _memoryCache;
        private readonly IConfiguration _configuration;
        private readonly Mock<IDomainDependencyService> _domainDependencyService;
        private readonly EventsSortingService _service;

        public DependencyTests()
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
        public async Task WaitForDependencyEventAsync_ReturnsTrue_WhenMatchingEventArrives()
        {
            // Arrange
            string aggregateId = "222";
            string dependencyType = "INVOICE";
            string expectedDependencyAggregateId = "111";

            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var eventsToProcess = new List<EventMessage>();
            var deferredKafkaEvents = new List<EventMessage>();

            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            // Prepare consumer to return a matching invoice event
            consumerBufferMock.SetupSequence(c => c.Consume(It.IsAny<TimeSpan>()))
                .Returns(TestHelpers.CreateConsumeResult("INVOICE", "111"))
                .Returns(() => null);

            // Act - instead of reflection we use a public method
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

            // Assert
            Assert.True(result, "Expected WaitForDependencyEventAsync to return true when a matching event arrives.");
        }

        [Fact]
        public async Task WaitForDependencyEventAsync_ReturnsFalse_WhenNoMatchingEventArrives()
        {
            // Arrange
            string aggregateId = "222";
            string dependencyType = "INVOICE";
            string expectedDependencyAggregateId = "111";

            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var eventsToProcess = new List<EventMessage>();
            var deferredKafkaEvents = new List<EventMessage>();

            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            // Consumer returns an event that does NOT match (wrong aggregate_id)
            consumerBufferMock.SetupSequence(c => c.Consume(It.IsAny<TimeSpan>()))
                .Returns(TestHelpers.CreateConsumeResult("INVOICE", "999"))
                .Returns(() => null);

            // Act - instead of reflection we use a public method
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

            // Assert
            Assert.False(result, "Expected WaitForDependencyEventAsync to return false when no matching event arrives.");
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

            // Act - zamiast refleksji używamy metody publicznej
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
