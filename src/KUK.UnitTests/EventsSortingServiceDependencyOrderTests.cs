using System;
using System.Collections.Generic;
using System.Threading;
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
    public class EventsSortingServiceDependencyOrderTests
    {
        private readonly Mock<ILogger<EventsSortingService>> _loggerMock;
        private readonly Mock<IInvoiceService> _invoiceServiceMock;
        private readonly Mock<ICustomerService> _customerServiceMock;
        private readonly Mock<IAddressService> _addressServiceMock;
        private readonly IMemoryCache _memoryCache;
        private readonly IConfiguration _configuration;
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

            _service = new EventsSortingService(
                _loggerMock.Object, _invoiceServiceMock.Object, _customerServiceMock.Object, _addressServiceMock.Object, _memoryCache, _configuration);
        }

        [Fact]
        public async void EnsureDependencies_ShouldProcessOnlyMatchingDependencies()
        {
            // Arrange
            // Tworzymy główny event INVOICELINE dla INVOICEID=1
            string firstInvoiceAggregateId = "1";
            string secondInvoiceAggregateId = "2";
            var mainEvent = TestHelpers.CreateEvent("INVOICELINE", firstInvoiceAggregateId, "line1");

            // Przygotowujemy bufor z dwoma eventami:
            // 1. INVOICELINE dla INVOICEID=2 (nie powinien być uwzględniony)
            // 2. INVOICE dla INVOICEID=1 (powinien być uwzględniony)
            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            
            // Symulujemy kolejność eventów w buforze
            consumerBufferMock.SetupSequence(c => c.Consume(It.IsAny<TimeSpan>()))
                .Returns(TestHelpers.CreateConsumeResult("INVOICELINE", secondInvoiceAggregateId)) // Niepowiązany INVOICELINE
                .Returns(TestHelpers.CreateConsumeResult("INVOICE", firstInvoiceAggregateId)) // Powiązany INVOICE
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
            Assert.Equal(2, result.Count); // Powinny być 2 eventy: INVOICE i INVOICELINE dla ID=1

            // Sprawdzamy czy mamy właściwy INVOICE
            var invoice = result.Find(e => TestHelpers.ExtractEventType(e.Payload) == "INVOICE");
            Assert.NotNull(invoice);
            Assert.Equal(firstInvoiceAggregateId, TestHelpers.ExtractAggregateId(invoice.Payload));

            // Sprawdzamy czy mamy właściwy INVOICELINE
            var invoiceLine = result.Find(e => TestHelpers.ExtractEventType(e.Payload) == "INVOICELINE");
            Assert.NotNull(invoiceLine);
            Assert.Equal(firstInvoiceAggregateId, TestHelpers.ExtractAggregateId(invoiceLine.Payload));

            // Sprawdzamy czy nie ma niepowiązanego INVOICELINE
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