using Confluent.Kafka;
using KUK.ChinookSync.Services.Domain.Interfaces;
using KUK.KafkaProcessor.EventProcessing;
using KUK.KafkaProcessor.Services;
using KUK.KafkaProcessor.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;

namespace KUK.ChinookUnitTests
{
    public class EventsSortingServiceTests
    {
        [Fact]
        public void GetPriority_ShouldReturnCorrectPriority()
        {
            // Arrange
            var priorityLists = new List<List<PriorityDependency>>
            {
                new List<PriorityDependency> 
                { 
                    new PriorityDependency("Invoice", "InvoiceId"), 
                    new PriorityDependency("InvoiceLine", "InvoiceId") 
                },
                new List<PriorityDependency> 
                { 
                    new PriorityDependency("A", "Id"), 
                    new PriorityDependency("B", "Id"), 
                    new PriorityDependency("C", "Id") 
                }
            };

            var jsonInvoice = JObject.Parse(@"{ ""aggregate_type"": ""Invoice"" }");
            var jsonInvoiceLine = JObject.Parse(@"{ ""aggregate_type"": ""InvoiceLine"" }");
            var jsonCustomer = JObject.Parse(@"{ ""aggregate_type"": ""Customer"" }");

            var loggerMock = new Mock<ILogger<EventsSortingService>>();
            var invoiceServiceMock = new Mock<IInvoiceService>();
            var customerServiceMock = new Mock<ICustomerService>();
            var addressServiceMock = new Mock<IAddressService>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var domainDependencyServiceMock = new Mock<IDomainDependencyService>();

            var configuration = BuildInMemorySettings();
            var sortingService = new EventsSortingService(loggerMock.Object, memoryCache, configuration, domainDependencyServiceMock.Object);

            // Act
            var priorityInvoice = sortingService.GetPriority(jsonInvoice, priorityLists);
            var priorityInvoiceLine = sortingService.GetPriority(jsonInvoiceLine, priorityLists);
            var priorityCustomer = sortingService.GetPriority(jsonCustomer, priorityLists);

            // Assert
            Assert.Equal((0, 0), priorityInvoice);     // "Invoice": index 0
            Assert.Equal((0, 1), priorityInvoiceLine); // "InvoiceLine": index 1
            Assert.Equal((int.MaxValue, int.MaxValue), priorityCustomer); // "Customer" not found
        }

        [Fact]
        public void SortEvents_ShouldSortEventsAccordingToPriority()
        {
            // Arrange
            var priorityLists = new List<List<PriorityDependency>>
            {
                new List<PriorityDependency> 
                { 
                    new PriorityDependency("Invoice", "InvoiceId"), 
                    new PriorityDependency("InvoiceLine", "InvoiceId") 
                },
                new List<PriorityDependency> 
                { 
                    new PriorityDependency("A", "Id"), 
                    new PriorityDependency("B", "Id"), 
                    new PriorityDependency("C", "Id") 
                }
            };

            var eventsToProcess = new List<EventMessage>
            {
                new EventMessage {
                    Timestamp = new DateTime(2025, 03, 14, 15, 35, 13, 555),
                    Payload = @"{ ""event_id"": ""1"", ""aggregate_id"": ""496"", ""aggregate_type"": ""Invoice"" }"
                },
                new EventMessage {
                    Timestamp = new DateTime(2025, 03, 14, 15, 35, 14, 000),
                    Payload = @"{ ""event_id"": ""2"", ""aggregate_id"": ""496"", ""aggregate_type"": ""InvoiceLine"" }"
                },
                new EventMessage {
                    Timestamp = new DateTime(2025, 03, 14, 15, 35, 15, 000),
                    Payload = @"{ ""event_id"": ""3"", ""aggregate_id"": ""123"", ""aggregate_type"": ""Customer"" }"
                },
                new EventMessage {
                    Timestamp = new DateTime(2025, 03, 14, 15, 35, 16, 000),
                    Payload = @"{ ""event_id"": ""4"", ""aggregate_id"": ""496"", ""aggregate_type"": ""InvoiceLine"" }"
                },
            };

            var loggerMock = new Mock<ILogger<EventsSortingService>>();
            var invoiceServiceMock = new Mock<IInvoiceService>();
            var customerServiceMock = new Mock<ICustomerService>();
            var addressServiceMock = new Mock<IAddressService>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var configuration = BuildInMemorySettings();
            var domainDependencyServiceMock = new Mock<IDomainDependencyService>();
            var sortingService = new EventsSortingService(loggerMock.Object, memoryCache, configuration, domainDependencyServiceMock.Object);

            // Act
            var sortedEvents = sortingService.SortEvents(eventsToProcess, priorityLists, 0);

            // Assert
            Assert.Equal(4, sortedEvents.Count);

            // We check that for aggregate_id "496" the first event is Invoice, then InvoiceLine
            var eventsFor496 = sortedEvents.Where(e => JObject.Parse(e.Payload)["aggregate_id"].ToString() == "496").ToList();
            Assert.Equal(3, eventsFor496.Count);
            Assert.Equal("Invoice", JObject.Parse(eventsFor496[0].Payload)["aggregate_type"].ToString());
            Assert.Equal("InvoiceLine", JObject.Parse(eventsFor496[1].Payload)["aggregate_type"].ToString());
            Assert.Equal("InvoiceLine", JObject.Parse(eventsFor496[2].Payload)["aggregate_type"].ToString());
        }

        [Fact]
        public async Task EnsureDependenciesAsync_NoDependentEvents_NoChange()
        {
            // Arrange
            var service = TestHelpers.CreateEventSortingService();

            var eventsToProcess = new List<EventMessage>
            {
                TestHelpers.CreateEvent("CUSTOMER", "123"),
                TestHelpers.CreateEvent("INVOICE", "456")
            };

            var priorityLists = new List<List<PriorityDependency>>
            {
                new() { new PriorityDependency("CUSTOMER", "CustomerId") },
                new() { new PriorityDependency("INVOICE", "InvoiceId") }
            };

            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var deferredKafkaEvents = new List<EventMessage>();
            var cancellationToken = new CancellationToken();

            // Act
            var result = await service.EnsureDependenciesAsync(eventsToProcess, priorityLists, consumerBufferMock.Object, consumedResults, deferredKafkaEvents, cancellationToken);

            // Assert
            Assert.Equal(eventsToProcess.Count, result.Count);
            Assert.Equal(eventsToProcess, result);
        }

        [Fact]
        public async Task EnsureDependenciesAsync_DependencyExistsInBuffer_NoWaiting()
        {
            // Arrange
            var service = TestHelpers.CreateEventSortingService();

            var eventsToProcess = new List<EventMessage>
            {
                TestHelpers.CreateEvent("INVOICE", "111"),
                TestHelpers.CreateEvent("INVOICELINE", "111", "222")
            };

            var priorityLists = new List<List<PriorityDependency>>
            {
                new() { 
                    new PriorityDependency("INVOICE", "InvoiceId"),
                    new PriorityDependency("INVOICELINE", "InvoiceId")
                }
            };

            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var deferredKafkaEvents = new List<EventMessage>();
            var cancellationToken = new CancellationToken();

            // Act
            var result = await service.EnsureDependenciesAsync(eventsToProcess, priorityLists, consumerBufferMock.Object, consumedResults, deferredKafkaEvents, cancellationToken);

            // Assert
            Assert.Contains(result, e => TestHelpers.ExtractEventType(e.Payload) == "INVOICE");
            Assert.Contains(result, e => TestHelpers.ExtractEventType(e.Payload) == "INVOICELINE");
        }

        [Fact]
        public async Task EnsureDependenciesAsync_DependencyExistsInCache_NoWaiting()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            memoryCache.Set("INVOICE:111", true);

            var service = TestHelpers.CreateEventSortingService(memoryCache: memoryCache);

            var eventsToProcess = new List<EventMessage>
            {
                TestHelpers.CreateEvent("INVOICELINE", "111", "222")
            };

            var priorityLists = new List<List<PriorityDependency>>
            {
                new() { 
                    new PriorityDependency("INVOICE", "InvoiceId"),
                    new PriorityDependency("INVOICELINE", "InvoiceId")
                }
            };

            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var deferredKafkaEvents = new List<EventMessage>();
            var cancellationToken = new CancellationToken();

            // Act
            var result = await service.EnsureDependenciesAsync(eventsToProcess, priorityLists, consumerBufferMock.Object, consumedResults, deferredKafkaEvents, cancellationToken);

            // Assert
            Assert.Single(result);
            Assert.Equal("INVOICELINE", TestHelpers.ExtractEventType(result[0].Payload));
        }

        [Fact]
        public async Task EnsureDependenciesAsync_WaitsForDependency_ThenProcessesEvent()
        {
            // Arrange
            var service = TestHelpers.CreateEventSortingService();
            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();

            var eventsToProcess = new List<EventMessage>
            {
                TestHelpers.CreateEvent("INVOICELINE", "111", "222")
            };

            var priorityLists = new List<List<PriorityDependency>>
            {
                new() { 
                    new PriorityDependency("INVOICE", "InvoiceId"),
                    new PriorityDependency("INVOICELINE", "InvoiceId")
                }
            };

            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var deferredKafkaEvents = new List<EventMessage>();
            var cancellationToken = new CancellationToken();

            consumerBufferMock
                .Setup(c => c.Consume(It.IsAny<TimeSpan>()))
                .Returns(() => TestHelpers.CreateConsumeResult("INVOICE", "111"));

            // Act
            var result = await service.EnsureDependenciesAsync(eventsToProcess, priorityLists, consumerBufferMock.Object, consumedResults, deferredKafkaEvents, cancellationToken);

            // Assert
            Assert.Contains(result, e => TestHelpers.ExtractEventType(e.Payload) == "INVOICELINE");
            Assert.Contains(result, e => TestHelpers.ExtractEventType(e.Payload) == "INVOICE");
        }

        [Fact]
        public async Task EnsureDependenciesAsync_DependencyTimeout_LogsWarning()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<EventsSortingService>>();
            var service = TestHelpers.CreateEventSortingService(logger: loggerMock.Object);
            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();

            var eventsToProcess = new List<EventMessage>
            {
                TestHelpers.CreateEvent("INVOICELINE", "111", "222")
            };

            var priorityLists = new List<List<PriorityDependency>>
            {
                new() { 
                    new PriorityDependency("INVOICE", "InvoiceId"),
                    new PriorityDependency("INVOICELINE", "InvoiceId")
                }
            };

            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var deferredKafkaEvents = new List<EventMessage>();
            var cancellationToken = new CancellationToken();

            consumerBufferMock.Setup(c => c.Consume(It.IsAny<TimeSpan>())).Returns(() => null);

            // Act
            var result = await service.EnsureDependenciesAsync(eventsToProcess, priorityLists, consumerBufferMock.Object, consumedResults, deferredKafkaEvents, cancellationToken);

            // Assert
            loggerMock.Verify(x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Warning),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Dependency not satisfied")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)), Times.AtLeastOnce);
        }

        [Fact]
        public void SortEvents_WhenInvoiceHasLaterTimestamp_ShouldStillComeBeforeInvoiceLine()
        {
            // Arrange
            var priorityLists = new List<List<PriorityDependency>>
            {
                new List<PriorityDependency> 
                { 
                    new PriorityDependency("Invoice", "InvoiceId"), 
                    new PriorityDependency("InvoiceLine", "InvoiceId") 
                }
            };

            var eventsToProcess = new List<EventMessage>
            {
                new EventMessage {
                    Timestamp = new DateTime(2025, 03, 14, 15, 35, 14, 000), // Invoice comes later in time
                    Payload = @"{ ""event_id"": ""1"", ""aggregate_id"": ""496"", ""aggregate_type"": ""Invoice"" }"
                },
                new EventMessage {
                    Timestamp = new DateTime(2025, 03, 14, 15, 35, 13, 555), // InvoiceLine comes earlier in time
                    Payload = @"{ ""event_id"": ""2"", ""aggregate_id"": ""496"", ""aggregate_type"": ""InvoiceLine"" }"
                }
            };

            var loggerMock = new Mock<ILogger<EventsSortingService>>();
            var invoiceServiceMock = new Mock<IInvoiceService>();
            var customerServiceMock = new Mock<ICustomerService>();
            var addressServiceMock = new Mock<IAddressService>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var configuration = BuildInMemorySettings();
            var domainDependencyServiceMock = new Mock<IDomainDependencyService>();
            var sortingService = new EventsSortingService(loggerMock.Object, memoryCache, configuration, domainDependencyServiceMock.Object);

            // Act
            var sortedEvents = sortingService.SortEvents(eventsToProcess, priorityLists, 0);

            // Assert
            Assert.Equal(2, sortedEvents.Count);
            // Invoice should come first even though its timestamp is later
            Assert.Equal("Invoice", JObject.Parse(sortedEvents[0].Payload)["aggregate_type"].ToString());
            Assert.Equal("InvoiceLine", JObject.Parse(sortedEvents[1].Payload)["aggregate_type"].ToString());
        }

        private IConfiguration BuildInMemorySettings()
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "InternalKafkaProcessorParameters:MemoryCacheExpirationInSeconds", "60" },
                    { "InternalKafkaProcessorParameters:EventSortingServiceMaxWaitTimeInSeconds", "5" },
                    { "InternalKafkaProcessorParameters:EventSortingServiceAdditionalResultConsumeTimeInMilliseconds", "100" },
                    { "InternalKafkaProcessorParameters:EventSortingServiceDelayInMilliseconds", "50" }
                })
                .Build();
        }
    }
}
