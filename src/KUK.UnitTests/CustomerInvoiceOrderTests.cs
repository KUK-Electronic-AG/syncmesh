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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace KUK.UnitTests
{
    public class CustomerInvoiceOrderTests
    {
        private readonly Mock<ILogger<EventsSortingService>> _loggerMock;
        private readonly Mock<IInvoiceService> _invoiceServiceMock;
        private readonly Mock<ICustomerService> _customerServiceMock;
        private readonly Mock<IAddressService> _addressServiceMock;
        private readonly IMemoryCache _memoryCache;
        private readonly IConfiguration _configuration;
        private readonly EventsSortingService _service;

        public CustomerInvoiceOrderTests()
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
        public async Task InvoiceBeforeCustomer_ShouldFail_WhenNoCustomerMapping()
        {
            // Arrange
            // Symulujemy sytuację z logów, gdzie faktura przychodzi przed klientem
            string customerId = "60";

            // Tworzenie eventu INVOICE, który odnosi się do CUSTOMER o ID=60
            var invoiceEvent = TestHelpers.CreateEvent("INVOICE", "414");

            // Ręcznie modyfikujemy payload, aby zawierał powiązanie z CustomerId=60
            var payload = Newtonsoft.Json.Linq.JObject.Parse(invoiceEvent.Payload);
            var innerPayload = Newtonsoft.Json.Linq.JObject.Parse(payload["payload"].ToString());
            innerPayload["CustomerId"] = customerId;
            payload["payload"] = innerPayload.ToString();
            invoiceEvent.Payload = payload.ToString();

            // Event CustomerEvent, który pojawi się później
            var customerEvent = TestHelpers.CreateEvent("CUSTOMER", customerId);

            // Kolejność eventów: najpierw INVOICE, potem CUSTOMER
            var eventsToProcess = new List<EventMessage> { invoiceEvent };
            var deferredKafkaEvents = new List<EventMessage>();

            // Konsument, który po jakimś czasie zwróci event CUSTOMER
            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            
            // Nie ma jeszcze mappingu klienta w systemie
            _invoiceServiceMock.Setup(s => s.MappingExists(It.Is<int>(id => id.ToString() == customerId)))
                .ReturnsAsync(false);

            // Lista priorytetów, gdzie CUSTOMER jest przed INVOICE
            var priorityLists = new List<List<PriorityDependency>> 
            { 
                new List<PriorityDependency> 
                { 
                    new PriorityDependency("CUSTOMER", "CustomerId"), 
                    new PriorityDependency("INVOICE", "CustomerId") 
                } 
            };

            // Act
            // Ta operacja powinna spowodować, że invoice będzie czekał na customer
            var result = await _service.EnsureDependenciesAsync(
                eventsToProcess,
                priorityLists,
                consumerBufferMock.Object,
                consumedResults,
                deferredKafkaEvents,
                CancellationToken.None);

            // Assert
            // Oczekujemy, że faktura nie została przetworzona poprawnie, bo brakuje customer mapping
            bool cacheHit = _memoryCache.TryGetValue("CUSTOMER:60", out _);
            Assert.False(cacheHit, "Nie powinno być wpisu w cache dla 'CUSTOMER:60', gdy mappingu nie ma w systemie.");
            
            // Teraz sprawdzimy czy wywołano External Check - powinno być wywołane
            _customerServiceMock.Verify(
                s => s.MappingExists(It.Is<int>(id => id.ToString() == customerId)), 
                Times.AtLeastOnce, 
                "Powinno być wywołane sprawdzenie MappingExists dla CustomerId=60");
        }

        [Fact]
        public async Task InvoiceWithCustomer_InWrongOrder_ShouldBeDeferred()
        {
            // Arrange
            string customerId = "60";
            
            // Tworzenie eventu INVOICE, który odnosi się do CUSTOMER o ID=60
            var invoiceEvent = TestHelpers.CreateEvent("INVOICE", "414");
            var payload = Newtonsoft.Json.Linq.JObject.Parse(invoiceEvent.Payload);
            var innerPayload = Newtonsoft.Json.Linq.JObject.Parse(payload["payload"].ToString());
            innerPayload["CustomerId"] = customerId;
            payload["payload"] = innerPayload.ToString();
            invoiceEvent.Payload = payload.ToString();
            
            // Event CUSTOMER, który pojawi się później w buforze
            var customerEvent = TestHelpers.CreateEvent("CUSTOMER", customerId);
            
            // W buforze konsumenta będzie czekał event CUSTOMER
            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            consumerBufferMock.Setup(c => c.Consume(It.IsAny<TimeSpan>()))
                .Returns(TestHelpers.CreateConsumeResult("CUSTOMER", customerId));
            
            var eventsToProcess = new List<EventMessage> { invoiceEvent };
            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var deferredKafkaEvents = new List<EventMessage>();
            
            // Lista priorytetów, gdzie CUSTOMER jest przed INVOICE
            var priorityLists = new List<List<PriorityDependency>> 
            { 
                new List<PriorityDependency> 
                { 
                    new PriorityDependency("CUSTOMER", "CustomerId"), 
                    new PriorityDependency("INVOICE", "CustomerId") 
                } 
            };
            
            // Act
            var result = await _service.EnsureDependenciesAsync(
                eventsToProcess,
                priorityLists,
                consumerBufferMock.Object,
                consumedResults,
                deferredKafkaEvents,
                CancellationToken.None);
            
            // Assert
            // Test pokaże, że obecna implementacja nie obsługuje poprawnie tej sytuacji,
            // ponieważ nie mamy jeszcze logiki, która obsługuje zależności między CUSTOMER a INVOICE
            
            // Sprawdzamy czy CUSTOMER został znaleziony i dodany do buforów
            bool foundCustomer = result.Exists(e => TestHelpers.ExtractEventType(e.Payload) == "CUSTOMER" && 
                                                  TestHelpers.ExtractAggregateId(e.Payload) == customerId);
            
            // Sprawdzamy czy INVOICE jest w wynikach
            bool invoiceStillExists = result.Exists(e => TestHelpers.ExtractEventType(e.Payload) == "INVOICE");
            
            // W idealnym scenariuszu customer powinien być dodany, a invoice powinien istnieć
            // Jednak nasz test pokaże, że obecna implementacja nie obsługuje tego poprawnie
            Assert.True(foundCustomer, "Customer event powinien być pobrany z bufora i dodany do listy eventów");
            Assert.True(invoiceStillExists, "Invoice event powinien pozostać w buforze eventów");
            
            // Sprawdzamy czy customer został oznaczony w cache
            bool customerInCache = _memoryCache.TryGetValue("CUSTOMER:60", out _);
            Assert.True(customerInCache, "Customer powinien być zapisany w cache po znalezieniu go w buforze");
        }

        [Fact]
        public async Task MultipleEvents_InWrongOrder_ShouldBeHandledCorrectly()
        {
            // Arrange
            string customerId = "60";
            string invoiceId = "414";
            string invoiceLineId = "100";

            // Tworzenie eventu INVOICE, który odnosi się do CUSTOMER o ID=60
            var invoiceEvent = TestHelpers.CreateEvent("INVOICE", invoiceId);
            var payload = Newtonsoft.Json.Linq.JObject.Parse(invoiceEvent.Payload);
            var innerPayload = Newtonsoft.Json.Linq.JObject.Parse(payload["payload"].ToString());
            innerPayload["CustomerId"] = customerId;
            payload["payload"] = innerPayload.ToString();
            invoiceEvent.Payload = payload.ToString();

            // Tworzenie eventu INVOICELINE, który odnosi się do INVOICE o ID=60
            var invoiceLineEvent = TestHelpers.CreateEvent("INVOICELINE", invoiceId, invoiceLineId);

            // Event CUSTOMER, który pojawi się później w buforze
            var customerEvent = TestHelpers.CreateEvent("CUSTOMER", customerId);

            // W buforze konsumenta będzie czekał event CUSTOMER
            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            consumerBufferMock.Setup(c => c.Consume(It.IsAny<TimeSpan>()))
                .Returns(TestHelpers.CreateConsumeResult("CUSTOMER", customerId));

            var eventsToProcess = new List<EventMessage> { invoiceEvent, invoiceLineEvent };
            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var deferredKafkaEvents = new List<EventMessage>();

            // Lista priorytetów, gdzie CUSTOMER jest przed INVOICE
            var priorityLists = TestHelpers.GetFullPriorityList();

            // Act
            var result = await _service.EnsureDependenciesAsync(
                eventsToProcess,
                priorityLists,
                consumerBufferMock.Object,
                consumedResults,
                deferredKafkaEvents,
                CancellationToken.None);

            // Assert
            // Test pokaże, że obecna implementacja nie obsługuje poprawnie tej sytuacji,
            // ponieważ nie mamy jeszcze logiki, która obsługuje zależności między CUSTOMER a INVOICE

            // Sprawdzamy czy CUSTOMER został znaleziony i dodany do buforów
            bool foundCustomer = result.Exists(e => TestHelpers.ExtractEventType(e.Payload) == "CUSTOMER" &&
                                                  TestHelpers.ExtractAggregateId(e.Payload) == customerId);
            // Sprawdzamy czy INVOICE jest w wynikach
            bool invoiceStillExists = result.Exists(e => TestHelpers.ExtractEventType(e.Payload) == "INVOICE");

            // W idealnym scenariuszu customer powinien być dodany, a invoice powinien istnieć
            // Jednak nasz test pokaże, że obecna implementacja nie obsługuje tego poprawnie
            Assert.True(foundCustomer, "Customer event powinien być pobrany z bufora i dodany do listy eventów");
            Assert.True(invoiceStillExists, "Invoice event powinien pozostać w buforze eventów");

            // Sprawdzamy czy customer został oznaczony w cache
            bool customerInCache = _memoryCache.TryGetValue("CUSTOMER:60", out _);
            Assert.True(customerInCache, "Customer powinien być zapisany w cache po znalezieniu go w buforze");

            // Act
            List<EventMessage> sortedEvents = _service.SortEvents(result, priorityLists, 0);

            // Assert
            var firstPayloadJson = JObject.Parse(sortedEvents[0].Payload.ToString());
            var secondPayloadJson = JObject.Parse(sortedEvents[1].Payload.ToString());
            var thirdPayloadJson = JObject.Parse(sortedEvents[2].Payload.ToString());
            Assert.Equal("CUSTOMER", firstPayloadJson["event_type"]?.ToString());
            Assert.Equal("INVOICE", secondPayloadJson["event_type"]?.ToString());
            Assert.Equal("INVOICELINE", thirdPayloadJson["event_type"]?.ToString());
        }

        [Fact]
        public void SortEvents_ShouldSortAccordingToPriority()
        {
            // Arrange
            var priorityList = TestHelpers.GetFullPriorityList();

            string customerId = "60";
            string invoiceId = "414";
            string invoiceLineId = "100";

            // Tworzenie eventu INVOICE, który odnosi się do CUSTOMER o ID=60
            var invoiceEvent = TestHelpers.CreateEvent("INVOICE", invoiceId);
            var payload = Newtonsoft.Json.Linq.JObject.Parse(invoiceEvent.Payload);
            var innerPayload = Newtonsoft.Json.Linq.JObject.Parse(payload["payload"].ToString());
            innerPayload["CustomerId"] = customerId;
            payload["payload"] = innerPayload.ToString();
            invoiceEvent.Payload = payload.ToString();

            // Tworzenie eventu INVOICELINE, który odnosi się do INVOICE o ID=60
            var invoiceLineEvent = TestHelpers.CreateEvent("INVOICELINE", invoiceId, invoiceLineId);

            // Event CUSTOMER, który pojawi się później w buforze
            var customerEvent = TestHelpers.CreateEvent("CUSTOMER", customerId);

            // All events
            var events = new List<EventMessage> { invoiceEvent, invoiceLineEvent, customerEvent };

            // Act
            var result = _service.SortEvents(events, priorityList, bufferId: 0);

            // Assert
            var firstPayloadJson = JObject.Parse(result[0].Payload.ToString());
            var secondPayloadJson = JObject.Parse(result[1].Payload.ToString());
            var thirdPayloadJson = JObject.Parse(result[2].Payload.ToString());
            Assert.Equal("CUSTOMER", firstPayloadJson["event_type"]?.ToString());
            Assert.Equal("INVOICE", secondPayloadJson["event_type"]?.ToString());
            Assert.Equal("INVOICELINE", thirdPayloadJson["event_type"]?.ToString());
        }

        [Fact]
        public void ExtractCustomerDependencyFromInvoice_ShouldReturnCustomerId()
        {
            // Arrange
            // Tworzymy payloady dla Invoice zawierające CustomerId podobnie jak w logach

            // Przygotuj wewnętrzny payload jako obiekt
            var innerPayload = new
            {
                Total = 100.00,
                InvoiceId = 414,
                CustomerId = 60,
                BillingCity = "Anytown",
                InvoiceDate = "2025-03-26 12:24:56.000000",
                BillingState = (string)null,
                BillingAddress = "123 Main St (added from nested query)",
                BillingCountry = "USA",
                BillingPostalCode = (string)null
            };

            // Serializuj wewnętrzny payload jako string JSON
            string innerPayloadJson = JsonConvert.SerializeObject(innerPayload);

            // Przygotuj zewnętrzny payload jako obiekt
            var outerPayload = new
            {
                event_id = 2,
                aggregate_id = 414,
                aggregate_type = "INVOICE",
                event_type = "CREATED",
                payload = innerPayloadJson, // tu idzie string, a nie obiekt
                unique_identifier = Guid.NewGuid().ToString(),
                created_at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                __deleted = "false",
                __op = "c",
                __source_ts_ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                __source_table = "invoice_outbox",
                __source_name = "old_to_new",
                __query = (string)null
            };

            // Serializuj zewnętrzny payload jako JSON
            string fullPayload = JsonConvert.SerializeObject(outerPayload);

            // Act - wywołujemy nową metodę ExtractDependencyId
            string customerId = _service.ExtractDependencyId(fullPayload, "CustomerId");

            // Assert
            // Sprawdzamy, czy poprawnie wyciągnięto CustomerId z payloadu
            Assert.Equal("60", customerId);
            
            // Tutaj sprawdzamy, czy bieżąca implementacja jest w stanie identyfikować zależność 
            // między INVOICE a CUSTOMER - teraz używamy nowej klasy PriorityDependency
            var priorityLists = new List<List<PriorityDependency>> 
            { 
                new List<PriorityDependency> 
                { 
                    new PriorityDependency("CUSTOMER", "CustomerId"), 
                    new PriorityDependency("INVOICE", "CustomerId") 
                } 
            };
            
            bool isDependentEvent = _service.IsDependentEvent("INVOICE", priorityLists);
            Assert.True(isDependentEvent, "INVOICE powinien być traktowany jako zależny od CUSTOMER");
            
            // Sprawdzamy też, czy metoda GetDependency zwraca właściwą zależność dla INVOICE
            PriorityDependency dependency = _service.GetDependency("INVOICE", priorityLists);
            Assert.NotNull(dependency);
            Assert.Equal("CUSTOMER", dependency.Type);
            Assert.Equal("CustomerId", dependency.IdField);
        }
        
        [Fact]
        public void SimulateNoCustomerMappingError_FromLogs()
        {
            // Arrange
            string customerId = "60";
            string invoiceId = "414";

            // Utwórz wewnętrzny payload (Invoice)
            var innerPayload = new
            {
                Total = 100.00,
                InvoiceId = 414,
                CustomerId = 60,
                BillingCity = "Anytown",
                InvoiceDate = "2025-03-26 12:24:56.000000",
                BillingState = (string)null,
                BillingAddress = "123 Main St (added from nested query)",
                BillingCountry = "USA",
                BillingPostalCode = (string)null
            };

            string innerPayloadJson = JsonConvert.SerializeObject(innerPayload);

            // Utwórz zewnętrzny payload (event)
            var outerPayload = new
            {
                event_id = 2,
                aggregate_id = 414,
                aggregate_type = "INVOICE",
                event_type = "CREATED",
                payload = innerPayloadJson, // string wewnętrzny
                unique_identifier = Guid.NewGuid().ToString(),
                created_at = 1742991896429,
                __deleted = "false",
                __op = "c",
                __source_ts_ms = 1742991896446,
                __source_table = "invoice_outbox",
                __source_name = "old_to_new",
                __query = @"INSERT INTO db_chinook1.Invoice (CustomerId, InvoiceDate, BillingAddress, BillingCity, BillingState, BillingCountry, BillingPostalCode, Total, TestValue3)
                    VALUES (
                        (SELECT CustomerId FROM db_chinook1.Customer WHERE Email = 'john.doe@example.com' LIMIT 1),
                        NOW(),
                        '123 Main St (added from nested query)',
                        'Anytown',
                        NULL,
                        'USA',
                        NULL,
                        100.00,
                        0.00
                    )"
            };

            string fullInvoicePayload = JsonConvert.SerializeObject(outerPayload);

            // Stwórz obiekt EventMessage z tego payloadu
            var invoiceEvent = new EventMessage
            {
                Timestamp = DateTime.UtcNow,
                Payload = fullInvoicePayload
            };
            
            // Lista priorytetów, gdzie CUSTOMER jest przed INVOICE (jak powinno być)
            var priorityLists = new List<List<PriorityDependency>> 
            { 
                new List<PriorityDependency> 
                { 
                    new PriorityDependency("CUSTOMER", "CustomerId"), 
                    new PriorityDependency("INVOICE", "CustomerId") 
                } 
            };
            
            // Act
            string extractedCustomerId = _service.ExtractDependencyId(fullInvoicePayload, "CustomerId");
            PriorityDependency dependency = _service.GetDependency("INVOICE", priorityLists);
            
            // Assert
            Assert.Equal("60", extractedCustomerId);
            Assert.NotNull(dependency);
            Assert.Equal("CUSTOMER", dependency.Type);
            Assert.Equal("CustomerId", dependency.IdField);
        }

        /*
        [Fact]
        public async Task CustomerEvent_ShouldHaveAddressDependency()
        {
            // Arrange
            string customerId = "60";
            string addressId = "CREATE_NEW_ADDRESS"; // Przykładowy identyfikator adresu
            
            // Tworzenie eventu CUSTOMER z wymaganym identyfikatorem adresu
            var customerEvent = TestHelpers.CreateEvent("CUSTOMER", customerId);
            var payload = Newtonsoft.Json.Linq.JObject.Parse(customerEvent.Payload);
            payload["AddressId"] = addressId; // Dodajemy identyfikator adresu do payloadu
            customerEvent.Payload = payload.ToString();

            var eventsToProcess = new List<EventMessage> { customerEvent };
            var deferredKafkaEvents = new List<EventMessage>();
            var consumedResults = new List<ConsumeResult<Ignore, string>>();

            // Act
            var result = await _service.EnsureDependenciesAsync(
                eventsToProcess,
                priorityLists,
                consumerBufferMock.Object,
                consumedResults,
                deferredKafkaEvents,
                CancellationToken.None);

            // Assert
            // Sprawdzamy, czy identyfikator adresu został poprawnie przetworzony
            bool addressDependencyExists = result.Exists(e => TestHelpers.ExtractEventType(e.Payload) == "ADDRESS" && 
                                                              TestHelpers.ExtractAggregateId(e.Payload) == addressId);
            Assert.True(addressDependencyExists, "Adres powinien być przetworzony jako zależność dla zdarzenia CUSTOMER.");
        }
        */
    }
} 