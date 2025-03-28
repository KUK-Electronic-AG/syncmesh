﻿using System;
using System.Collections.Generic;
using System.Linq;
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
    public class EventsSortingServiceDependencyTests
    {
        private readonly IEventsSortingService _service;
        private readonly Mock<IInvoiceService> _invoiceServiceMock;
        private readonly Mock<ICustomerService> _customerServiceMock;
        private readonly Mock<IAddressService> _addressServiceMock;
        private readonly IMemoryCache _memoryCache;
        private readonly IConfiguration _configuration;
        private readonly Mock<ILogger<EventsSortingService>> _loggerMock;
        private readonly DatabaseEventProcessor _databaseEventProcessor;

        public EventsSortingServiceDependencyTests()
        {
            _loggerMock = new Mock<ILogger<EventsSortingService>>();
            _invoiceServiceMock = new Mock<IInvoiceService>();
            _customerServiceMock = new Mock<ICustomerService>();
            _addressServiceMock = new Mock<IAddressService>();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "InternalKafkaProcessorParameters:MemoryCacheExpirationInSeconds", "60" },
                    { "InternalKafkaProcessorParameters:EventSortingServiceMaxWaitTimeInSeconds", "2" },
                    { "InternalKafkaProcessorParameters:EventSortingServiceAdditionalResultConsumeTimeInMilliseconds", "100" },
                    { "InternalKafkaProcessorParameters:EventSortingServiceDelayInMilliseconds", "50" }
                })
                .Build();

            var databaseLoggerMock = new Mock<ILogger<DatabaseEventProcessor>>();
            _databaseEventProcessor = new DatabaseEventProcessor(
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
                new EventsSortingService(_loggerMock.Object, _invoiceServiceMock.Object, _customerServiceMock.Object, _addressServiceMock.Object, _memoryCache, _configuration),
                _memoryCache,
                new GlobalState()
            );

            _service = TestHelpers.CreateService(
                logger: _loggerMock.Object,
                invoiceService: _invoiceServiceMock.Object,
                customerService: _customerServiceMock.Object,
                memoryCache: _memoryCache,
                configuration: _configuration
            );
        }

        #region Tests for WaitForDependencyEventAsync

        [Fact]
        public async Task WaitForDependencyEventAsync_ReturnsTrue_WhenDependencyEventAppears()
        {
            // Arrange
            string aggregateId = "222";
            string dependencyType = "INVOICE";
            string expectedDependencyAggregateId = "111";

            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var eventsToProcess = new List<EventMessage>();
            var deferredKafkaEvents = new List<EventMessage>();

            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            consumerBufferMock
                .SetupSequence(c => c.Consume(It.IsAny<TimeSpan>()))
                .Returns(TestHelpers.CreateConsumeResult("INVOICE", expectedDependencyAggregateId))
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

            // Rozszerzony komunikat błędu
            string errorMessage = $"WaitForDependencyEventAsync: Expected result TRUE for AggregateId='{aggregateId}', dependencyType='{dependencyType}', expectedDependencyAggregateId='{expectedDependencyAggregateId}'. " +
                                  $"Obtained result: {result}. ConsumedResults count: {consumedResults.Count}, eventsToProcess count: {eventsToProcess.Count}.";
            Assert.True(result, errorMessage);
        }

        [Fact]
        public async Task WaitForDependencyEventAsync_ReturnsFalse_WhenNoDependencyEventAppears_Timeout()
        {
            // Arrange
            string aggregateId = "222";
            string dependencyType = "INVOICE";
            string expectedDependencyAggregateId = "111";

            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var eventsToProcess = new List<EventMessage>();
            var deferredKafkaEvents = new List<EventMessage>();

            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            consumerBufferMock
                .Setup(c => c.Consume(It.IsAny<TimeSpan>()))
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

            string errorMessage = $"WaitForDependencyEventAsync: Expected result FALSE due to timeout for AggregateId='{aggregateId}', dependencyType='{dependencyType}', expectedDependencyAggregateId='{expectedDependencyAggregateId}'. " +
                                  $"Obtained result: {result}. ConsumedResults count: {consumedResults.Count}, eventsToProcess count: {eventsToProcess.Count}.";
            Assert.False(result, errorMessage);
        }

        #endregion

        #region Tests for EnsureDependencyForEventAsync

        [Fact]
        public async Task EnsureDependencyForEventAsync_Skips_WhenDependencyAlreadyInBuffer()
        {
            // Arrange – dependency już jest w buforze
            var invoiceEvent = TestHelpers.CreateEvent("INVOICE", "111");
            var invoiceLineEvent = TestHelpers.CreateEvent("INVOICELINE", "111", "222");
            var eventsToProcess = new List<EventMessage> { invoiceEvent, invoiceLineEvent };
            var deferredKafkaEvents = new List<EventMessage>();

            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
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

            // Assert – w tym przypadku cache powinien zostać ustawiony
            bool cacheHit = _memoryCache.TryGetValue("INVOICE:111", out _);
            string errorMessage = $"EnsureDependencyForEventAsync: Expected cache to contain key 'INVOICE:111' since dependency is already in buffer. " +
                                  $"CacheHit: {cacheHit}. EventsToProcess count: {eventsToProcess.Count}.";
            Assert.True(cacheHit, errorMessage);
        }

        [Fact]
        public async Task EnsureDependencyForEventAsync_UsesExternalCheck_WhenDependencyNotInBuffer()
        {
            // Arrange – dependency nie jest w buforze, a zewnętrzna metoda zwraca true
            _invoiceServiceMock.Setup(s => s.MappingExists(It.IsAny<int>())).ReturnsAsync(true);

            var invoiceLineEvent = TestHelpers.CreateEvent("INVOICELINE", "111", "222");
            var eventsToProcess = new List<EventMessage> { invoiceLineEvent };
            var deferredKafkaEvents = new List<EventMessage>();

            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
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

            // Assert – dependency powinno być oznaczone w cache przez zewnętrzne sprawdzenie
            bool cacheHit = _memoryCache.TryGetValue("INVOICE:111", out _);
            string errorMessage = $"EnsureDependencyForEventAsync (external check): Expected cache to contain key 'INVOICE:111'. " +
                                  $"CacheHit: {cacheHit}. InvoiceService mapping exists: true.";
            Assert.True(cacheHit, errorMessage);
        }

        [Fact]
        public async Task EnsureDependencyForEventAsync_WaitsForAdditionalEventAndSucceeds()
        {
            // Arrange – dependency nie jest w buforze, ale zewnętrzna metoda oraz dodatkowe zdarzenia zwracają true
            _invoiceServiceMock.Setup(s => s.MappingExists(It.IsAny<int>())).ReturnsAsync(false);

            var invoiceLineEvent = TestHelpers.CreateEvent("INVOICELINE", "111", "222");
            var eventsToProcess = new List<EventMessage> { invoiceLineEvent };
            var deferredKafkaEvents = new List<EventMessage>();

            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            consumerBufferMock
                .SetupSequence(c => c.Consume(It.IsAny<TimeSpan>()))
                .Returns(TestHelpers.CreateConsumeResult("INVOICE", "111"))  // <- To zdarzenie pasuje
                .Returns(() => null);
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

            // Assert – weryfikacja, że zdarzenie zostało przetworzone i zależność została obsłużona
            bool cacheHit = _memoryCache.TryGetValue("INVOICE:111", out _);
            string errorMessage = $"EnsureDependencyForEventAsync (waiting): Expected cache to contain key 'INVOICE:111'. " +
                                  $"CacheHit: {cacheHit}. Consumed additional events count: {consumedResults.Count}.";
            Assert.True(cacheHit, errorMessage);
        }

        [Fact]
        public async Task ProcessMessageToEventMessageAsync_ReturnsCorrectValues()
        {
            // Ten test nie używa refleksji, więc nie ma potrzeby go modyfikować
            // Arrange
            var consumeResult = TestHelpers.CreateConsumeResult("INVOICE", "123");

            // Act
            var eventMessage = await _service.ProcessMessageToEventMessageAsync(consumeResult);

            // Assert
            Assert.Equal(consumeResult.Message.Value, eventMessage.Payload);
            Assert.Equal("INVOICE", _service.ExtractEventType(eventMessage.Payload));
            Assert.Equal("123", _service.ExtractAggregateId(eventMessage.Payload));
        }

        [Fact]
        public async Task WaitForDependencyEventAsync_DiagnosisTest()
        {
            // Arrange
            string aggregateId = "222";
            string dependencyType = "INVOICE";
            string expectedDependencyAggregateId = "111";

            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var eventsToProcess = new List<EventMessage>();
            var deferredKafkaEvents = new List<EventMessage>();

            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            // Przygotowanie metody Consume do zwrócenia odpowiedniego zdarzenia
            consumedResults.Add(TestHelpers.CreateConsumeResult("INVOICE", "111"));
            consumerBufferMock
                .SetupSequence(c => c.Consume(It.IsAny<TimeSpan>()))
                .Returns(consumedResults[0])
                .Returns(() => null);

            string diagEventType = "brak";
            string diagAggregateId = "brak";
            if (consumedResults.Count > 0)
            {
                var additionalEvent = consumedResults[0];
                var additionalEventPayload = additionalEvent.Message.Value;
                diagEventType = _service.ExtractEventType(additionalEventPayload);
                diagAggregateId = _service.ExtractAggregateId(additionalEventPayload);
            }

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

            string errorMessage = $"WaitForDependencyEventAsync: Expected result TRUE for AggregateId='{aggregateId}', dependencyType='{dependencyType}', expectedDependencyAggregateId='{expectedDependencyAggregateId}'. " +
                                  $"Obtained result: {result}. ConsumedResults count: {consumedResults.Count}, eventsToProcess count: {eventsToProcess.Count}. " +
                                  $"Extracted additional event type: '{diagEventType}', aggregateId: '{diagAggregateId}'.";
            Assert.True(result, errorMessage);
        }

        [Fact]
        public async Task EnsureDependencyForEventAsync_DiagnosisTest()
        {
            // Arrange – dependency już jest w buforze
            var invoiceEvent = TestHelpers.CreateEvent("INVOICE", "111");
            var invoiceLineEvent = TestHelpers.CreateEvent("INVOICELINE", "111", "222");
            var eventsToProcess = new List<EventMessage> { invoiceEvent, invoiceLineEvent };
            var deferredKafkaEvents = new List<EventMessage>();

            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
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

            // Assert – w tym przypadku cache powinien zostać ustawiony
            bool cacheHit = _memoryCache.TryGetValue("INVOICE:111", out _);
            string errorMessage = $"EnsureDependencyForEventAsync: Expected cache to contain key 'INVOICE:111' since dependency is already in buffer. " +
                                  $"CacheHit: {cacheHit}. EventsToProcess count: {eventsToProcess.Count}.";
            Assert.True(cacheHit, errorMessage);
        }

        #endregion
    }
}
