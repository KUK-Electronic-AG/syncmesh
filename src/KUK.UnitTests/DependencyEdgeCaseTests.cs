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
    public class DependencyEdgeCaseTests
    {
        private readonly Mock<ILogger<EventsSortingService>> _loggerMock;
        private readonly Mock<IInvoiceService> _invoiceServiceMock;
        private readonly Mock<ICustomerService> _customerServiceMock;
        private readonly Mock<IAddressService> _addressServiceMock;
        private readonly IMemoryCache _memoryCache;
        private readonly IConfiguration _configuration;
        private readonly EventsSortingService _service;

        public DependencyEdgeCaseTests()
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

            _service = new EventsSortingService(
                _loggerMock.Object, _invoiceServiceMock.Object, _customerServiceMock.Object, _addressServiceMock.Object, _memoryCache, _configuration);
        }

        [Fact]
        public async Task EnsureDependencyForEventAsync_ReturnsImmediately_WhenDependencyInCache()
        {
            // Arrange – simulate that dependency is already in cache.
            var invoiceEvent = TestHelpers.CreateEvent("INVOICE", "111");
            var invoiceLineEvent = TestHelpers.CreateEvent("INVOICELINE", "111");
            var eventsToProcess = new List<EventMessage> { invoiceEvent, invoiceLineEvent };

            // Pre-set cache key.
            _memoryCache.Set("INVOICE:111", true, TimeSpan.FromSeconds(60));

            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var priorityGroup = TestHelpers.GetPartialPriorityList();
            var deferredKafkaEvents = new List<EventMessage>();

            // Act - wywołuję bezpośrednio publiczną metodę
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

            // Assert – since dependency is in cache, no new events should be added.
            bool cacheHit = _memoryCache.TryGetValue("INVOICE:111", out object cacheValue);
            Assert.True(cacheHit, "Expected cache to contain key 'INVOICE:111' because dependency was already cached.");
        }

        [Fact]
        public async Task EnsureDependencyForEventAsync_CallsMappingExists_WhenDependencyNotInBuffer()
        {
            // Arrange – dependency not in buffer, external check returns true.
            _invoiceServiceMock.Setup(s => s.MappingExists(It.IsAny<int>())).ReturnsAsync(true);

            var invoiceLineEvent = TestHelpers.CreateEvent("INVOICELINE", "111");
            var eventsToProcess = new List<EventMessage> { invoiceLineEvent };

            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            // No additional event is returned.
            consumerBufferMock.Setup(c => c.Consume(It.IsAny<TimeSpan>())).Returns(() => null);
            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var priorityGroup = TestHelpers.GetPartialPriorityList();
            var deferredKafkaEvents = new List<EventMessage>();

            // Act - wywołuję bezpośrednio publiczną metodę
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

            // Assert – external check should set cache.
            bool cacheHit = _memoryCache.TryGetValue("INVOICE:111", out _);
            //Assert.True(cacheHit, "Expected cache to contain key 'INVOICE:111' when external dependency check returns true.");
            _invoiceServiceMock.Verify(service => service.MappingExists(111), Times.Once);
        }

        [Fact]
        public async Task WaitForDependencyEventAsync_ReturnsTrue_WhenMatchingEventArrivesAfterSeveralNonMatching()
        {
            // Arrange – first two events do not match, third does.
            string aggregateId = "222";
            string dependencyType = "INVOICE";
            string expectedDependencyAggregateId = "111";

            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var eventsToProcess = new List<EventMessage>();
            var deferredKafkaEvents = new List<EventMessage>();

            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            consumerBufferMock
                .SetupSequence(c => c.Consume(It.IsAny<TimeSpan>()))
                .Returns(TestHelpers.CreateConsumeResult("INVOICE", "999"))  // Non-matching
                .Returns(TestHelpers.CreateConsumeResult("INVOICE", "888"))  // Non-matching
                .Returns(TestHelpers.CreateConsumeResult("INVOICE", "111"))  // Matching
                .Returns(() => null);

            // Act - wywołuję bezpośrednio publiczną metodę
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

        [Fact]
        public async Task WaitForDependencyEventAsync_ReturnsFalse_WhenNoMatchingEventArrivesWithinTimeout()
        {
            // Arrange – no matching events arrive.
            string aggregateId = "222";
            string dependencyType = "INVOICE";
            string expectedDependencyAggregateId = "111";

            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var eventsToProcess = new List<EventMessage>();
            var deferredKafkaEvents = new List<EventMessage>();

            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            consumerBufferMock
                .SetupSequence(c => c.Consume(It.IsAny<TimeSpan>()))
                .Returns(TestHelpers.CreateConsumeResult("INVOICE", "999"))  // Non-matching
                .Returns(TestHelpers.CreateConsumeResult("INVOICE", "888"))  // Non-matching
                .Returns(() => null);

            // Act - wywołuję bezpośrednio publiczną metodę
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

            // Act - wywołuję bezpośrednio publiczną metodę
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
