using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using KUK.Common.Services;
using KUK.KafkaProcessor.Commands;
using KUK.KafkaProcessor.EventProcessing;
using KUK.KafkaProcessor.Services;
using KUK.KafkaProcessor.Services.Interfaces;
using KUK.KafkaProcessor.Utilities;
using Marten;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KUK.UnitTests
{
    /// <summary>
    /// Integration tests for dependency and invoice mapping scenarios.
    /// These tests simulate ordering, external dependency check, timeout and retry cases.
    /// </summary>
    public class DependencyIntegrationTests
    {
        private readonly Mock<ILogger<EventsSortingService>> _loggerMock;
        private readonly Mock<IInvoiceService> _invoiceServiceMock;
        private readonly Mock<ICustomerService> _customerServiceMock;
        private readonly Mock<IAddressService> _addressServiceMock;
        private readonly IMemoryCache _memoryCache;
        private readonly IConfiguration _configuration;
        private readonly EventsSortingService _service;

        public DependencyIntegrationTests()
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

        /// <summary>
        /// Test that if the dependency (invoice event) arrives before the invoice line,
        /// the dependency is satisfied and the mapping key is set in cache.
        /// </summary>
        [Fact]
        public async Task ProperOrderingDependencyTest()
        {
            // Arrange: simulate invoice event arriving before invoice line event.
            var invoiceEvent = TestHelpers.CreateEvent("INVOICE", "417");
            var invoiceLineEvent = TestHelpers.CreateEvent("INVOICELINE", "417", "100");

            // In the integration scenario, the invoice event should be processed first.
            var eventsToProcess = new List<EventMessage> { invoiceEvent, invoiceLineEvent };

            // Clear cache and ensure that dependency is not yet set.
            _memoryCache.Remove("INVOICE:417");

            // Simulate that consumer buffer returns no additional event (since invoice event already exists).
            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            consumerBufferMock.Setup(c => c.Consume(It.IsAny<TimeSpan>())).Returns(() => null);
            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var priorityGroup = TestHelpers.GetPartialPriorityList();

            // Invoke EnsureDependencyForEventAsync on the invoice line event.
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

            // Assert: mapping should be set in cache.
            bool cacheHit = _memoryCache.TryGetValue("INVOICE:417", out object cacheValue);
            Assert.True(cacheHit, "Expected cache to contain key 'INVOICE:417' when invoice event is present before invoice line.");
            Assert.IsType<bool>(cacheValue);
            Assert.True((bool)cacheValue, "Cache value for 'INVOICE:417' should be true.");
        }

        /// <summary>
        /// Test that when no matching dependency event arrives within the timeout,
        /// the dependency check fails and mapping is not set.
        /// </summary>
        [Fact]
        public async Task TimeoutScenarioTest()
        {
            // Arrange: simulate that no invoice event arrives for dependency "INVOICE" with expected ID "417".
            string aggregateId = "222";
            string dependencyType = "INVOICE";
            string expectedDependencyAggregateId = "417";

            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var eventsToProcess = new List<EventMessage>();
            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            // Consumer always returns non-matching event.
            consumerBufferMock.Setup(c => c.Consume(It.IsAny<TimeSpan>()))
                .Returns(() => TestHelpers.CreateConsumeResult("INVOICE", "999"));

            // Tworzymy własną instancję DatabaseEventProcessor dla testu
            var databaseLoggerMock = new Mock<ILogger<DatabaseEventProcessor>>();
            var databaseEventProcessor = new DatabaseEventProcessor(
                _configuration,
                databaseLoggerMock.Object,
                new Mock<IEventCommandFactory>().Object,
                new Mock<IDocumentStore>().Object,
                new Mock<IKafkaService>().Object,
                new Mock<IInitializationService>().Object,
                new Mock<IConnectorsRegistrationService>().Object,
                new Mock<IHostApplicationLifetime>().Object,
                new Mock<IUniqueIdentifiersService>().Object,
                new Mock<IRetryHelper>().Object,
                new Mock<IUtilitiesService>().Object,
                new Mock<IExternalConnectionService>().Object,
                _service,
                _memoryCache,
                new GlobalState()
            );

            var deferredKafkaEvents = new List<EventMessage>();
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

            Assert.False(result, "Expected WaitForDependencyEventAsync to return false when no matching event arrives within the timeout.");
            bool cacheHit = _memoryCache.TryGetValue("INVOICE:417", out _);
            Assert.False(cacheHit, "Expected no cache entry for 'INVOICE:417' when dependency check fails due to timeout.");
        }

        [Fact]
        public async Task RetryMechanismTest_FullFlow_SetsCacheWhenMatchingEventArrives()
        {
            // Arrange: simulate a sequence of non-matching events followed by a matching event.
            string dependencyType = "INVOICE";
            string expectedDependencyAggregateId = "417";

            // Create an invoice line event that depends on invoice with ID "417".
            var invoiceLineEvent = TestHelpers.CreateEvent("INVOICELINE", "417", "222");
            // Assume eventsToProcess initially contains only the invoice line event.
            var eventsToProcess = new List<EventMessage> { invoiceLineEvent };
            var deferredKafkaEvents = new List<EventMessage>();

            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            consumerBufferMock
                .SetupSequence(c => c.Consume(It.IsAny<TimeSpan>()))
                .Returns(TestHelpers.CreateConsumeResult("INVOICE", "999"))  // First non-matching
                .Returns(TestHelpers.CreateConsumeResult("INVOICE", "888"))  // Second non-matching
                .Returns(TestHelpers.CreateConsumeResult("INVOICE", "417"))  // Third matching
                .Returns(() => null);

            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var priorityGroup = TestHelpers.GetPartialPriorityList();

            // Instead of calling WaitForDependencyEventAsync directly,
            // call EnsureDependencyForEventAsync so that the cache is set if dependency is satisfied.
            await _service.EnsureDependencyForEventAsync(
                invoiceLineEvent,
                priorityGroup,
                eventsToProcess,
                consumerBufferMock.Object,
                consumedResults,
                Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceDelayInMilliseconds"]),
                Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceAdditionalResultConsumeTimeInMilliseconds"]),
                Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceMaxWaitTimeInSeconds"]),
                deferredKafkaEvents,
                CancellationToken.None);

            // Assert: after processing, cache should contain key "INVOICE:417".
            bool cacheHit = _memoryCache.TryGetValue("INVOICE:417", out object cacheValue);
            Assert.True(cacheHit, "Expected cache to contain key 'INVOICE:417' after dependency is satisfied.");
            Assert.IsType<bool>(cacheValue);
            Assert.True((bool)cacheValue, "Cache value for 'INVOICE:417' should be true after dependency is satisfied.");
        }


        /// <summary>
        /// Test that verifies data integrity: after processing a list of events, all invoice line events that require a dependency
        /// have the corresponding invoice mapping set in cache.
        /// </summary>
        [Fact]
        public async Task DataIntegrityTest()
        {
            // Arrange: Create a mix of events, some invoice events and some invoice line events.
            var invoiceEvent1 = TestHelpers.CreateEvent("INVOICE", "100");
            var invoiceEvent2 = TestHelpers.CreateEvent("INVOICE", "101");
            var invoiceLineEvent1 = TestHelpers.CreateEvent("INVOICELINE", "100", "A");
            var invoiceLineEvent2 = TestHelpers.CreateEvent("INVOICELINE", "101", "B");
            var eventsToProcess = new List<EventMessage> { invoiceEvent1, invoiceEvent2, invoiceLineEvent1, invoiceLineEvent2 };

            // Clear cache
            _memoryCache.Remove("INVOICE:100");
            _memoryCache.Remove("INVOICE:101");

            // Simulate dependency check: Assume external check returns true for both.
            _invoiceServiceMock.Setup(s => s.MappingExists(It.IsAny<int>())).ReturnsAsync(true);

            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            consumerBufferMock.Setup(c => c.Consume(It.IsAny<TimeSpan>())).Returns(() => null);
            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var priorityGroup = TestHelpers.GetPartialPriorityList();

            // Act: Process each invoice line event.
            var deferredKafkaEvents = new List<EventMessage>();
            
            foreach (var evt in eventsToProcess)
            {
                if (evt.Payload.Contains("INVOICELINE"))
                {
                    await _service.EnsureDependencyForEventAsync(
                        evt,
                        priorityGroup,
                        eventsToProcess,
                        consumerBufferMock.Object,
                        consumedResults,
                        Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceDelayInMilliseconds"]),
                        Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceAdditionalResultConsumeTimeInMilliseconds"]),
                        Convert.ToDouble(_configuration["InternalKafkaProcessorParameters:EventSortingServiceMaxWaitTimeInSeconds"]),
                        deferredKafkaEvents,
                        CancellationToken.None);
                }
            }

            // Assert: Check that for each invoice line event, the corresponding mapping is set in cache.
            bool cacheHit100 = _memoryCache.TryGetValue("INVOICE:100", out object cacheValue100);
            bool cacheHit101 = _memoryCache.TryGetValue("INVOICE:101", out object cacheValue101);
            Assert.True(cacheHit100, "Expected cache to contain key 'INVOICE:100' for invoice line dependent on invoice '100'.");
            Assert.True(cacheHit101, "Expected cache to contain key 'INVOICE:101' for invoice line dependent on invoice '101'.");
            Assert.True((bool)cacheValue100, "Cache value for 'INVOICE:100' should be true.");
            Assert.True((bool)cacheValue101, "Cache value for 'INVOICE:101' should be true.");
        }
    }
}
