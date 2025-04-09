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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KUK.ChinookUnitTests
{
    public class CustomerInvoiceOrderTests
    {
        private readonly Mock<ILogger<EventsSortingService>> _loggerMock;
        private readonly Mock<IInvoiceService> _invoiceServiceMock;
        private readonly Mock<ICustomerService> _customerServiceMock;
        private readonly Mock<IAddressService> _addressServiceMock;
        private readonly IMemoryCache _memoryCache;
        private readonly IConfiguration _configuration;
        private readonly IDomainDependencyService _domainDependencyService;
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

            var domainDependencyServiceLoggerMock = new Mock<ILogger<DomainDependencyService>>();
            _domainDependencyService = new DomainDependencyService(domainDependencyServiceLoggerMock.Object, _invoiceServiceMock.Object, _customerServiceMock.Object, _addressServiceMock.Object);
            _service = new EventsSortingService(
                _loggerMock.Object, _memoryCache, _configuration, _domainDependencyService);
        }

        [Fact]
        public async Task InvoiceBeforeCustomer_ShouldFail_WhenNoCustomerMapping()
        {
            // Arrange
            // Simulating a situation from logs where invoice arrives before customer
            string customerId = "60";

            // Creating INVOICE event that refers to CUSTOMER with ID=60
            var invoiceEvent = TestHelpers.CreateEvent("INVOICE", "414");

            // Manually modifying the payload to contain a reference to CustomerId=60
            var payload = Newtonsoft.Json.Linq.JObject.Parse(invoiceEvent.Payload);
            var innerPayload = Newtonsoft.Json.Linq.JObject.Parse(payload["payload"].ToString());
            innerPayload["CustomerId"] = customerId;
            payload["payload"] = innerPayload.ToString();
            invoiceEvent.Payload = payload.ToString();

            // CustomerEvent that will appear later
            var customerEvent = TestHelpers.CreateEvent("CUSTOMER", customerId);

            // Event order: first INVOICE, then CUSTOMER
            var eventsToProcess = new List<EventMessage> { invoiceEvent };
            var deferredKafkaEvents = new List<EventMessage>();

            // Consumer that will return CUSTOMER event after some time
            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            
            // Customer mapping does not exist in the system yet
            _invoiceServiceMock.Setup(s => s.MappingExists(It.Is<int>(id => id.ToString() == customerId)))
                .ReturnsAsync(false);

            // Priority list where CUSTOMER comes before INVOICE
            var priorityLists = new List<List<PriorityDependency>> 
            { 
                new List<PriorityDependency> 
                { 
                    new PriorityDependency("CUSTOMER", "CustomerId"), 
                    new PriorityDependency("INVOICE", "CustomerId") 
                } 
            };

            // Act
            // This operation should cause the invoice to wait for customer
            var result = await _service.EnsureDependenciesAsync(
                eventsToProcess,
                priorityLists,
                consumerBufferMock.Object,
                consumedResults,
                deferredKafkaEvents,
                CancellationToken.None);

            // Assert
            // We expect that the invoice was not processed correctly because customer mapping is missing
            bool cacheHit = _memoryCache.TryGetValue("CUSTOMER:60", out _);
            Assert.False(cacheHit, "There should be no cache entry for 'CUSTOMER:60' when mapping is not in the system.");
            
            // Now we'll check if External Check was called - it should be called
            _customerServiceMock.Verify(
                s => s.MappingExists(It.Is<int>(id => id.ToString() == customerId)), 
                Times.AtLeastOnce, 
                "MappingExists check should be called for CustomerId=60");
        }

        [Fact]
        public async Task InvoiceWithCustomer_InWrongOrder_ShouldBeDeferred()
        {
            // Arrange
            string customerId = "60";
            
            // Creating INVOICE event that refers to CUSTOMER with ID=60
            var invoiceEvent = TestHelpers.CreateEvent("INVOICE", "414");
            var payload = Newtonsoft.Json.Linq.JObject.Parse(invoiceEvent.Payload);
            var innerPayload = Newtonsoft.Json.Linq.JObject.Parse(payload["payload"].ToString());
            innerPayload["CustomerId"] = customerId;
            payload["payload"] = innerPayload.ToString();
            invoiceEvent.Payload = payload.ToString();
            
            // CUSTOMER event that will appear later in the buffer
            var customerEvent = TestHelpers.CreateEvent("CUSTOMER", customerId);
            
            // The consumer buffer will have a pending CUSTOMER event
            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            consumerBufferMock.Setup(c => c.Consume(It.IsAny<TimeSpan>()))
                .Returns(TestHelpers.CreateConsumeResult("CUSTOMER", customerId));
            
            var eventsToProcess = new List<EventMessage> { invoiceEvent };
            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var deferredKafkaEvents = new List<EventMessage>();
            
            // Priority list where CUSTOMER comes before INVOICE
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
            // The test will show that the current implementation doesn't handle this situation correctly,
            // because we don't yet have logic that handles dependencies between CUSTOMER and INVOICE
            
            // Check if CUSTOMER was found and added to buffers
            bool foundCustomer = result.Exists(e => TestHelpers.ExtractEventType(e.Payload) == "CUSTOMER" && 
                                                  TestHelpers.ExtractAggregateId(e.Payload) == customerId);
            
            // Check if INVOICE is in the results
            bool invoiceStillExists = result.Exists(e => TestHelpers.ExtractEventType(e.Payload) == "INVOICE");
            
            // In an ideal scenario, customer should be added and invoice should exist
            // However, our test will show that the current implementation doesn't handle this correctly
            Assert.True(foundCustomer, "Customer event should be retrieved from buffer and added to the event list");
            Assert.True(invoiceStillExists, "Invoice event should remain in the event buffer");
            
            // Check if customer was marked in cache
            bool customerInCache = _memoryCache.TryGetValue("CUSTOMER:60", out _);
            Assert.True(customerInCache, "Customer should be saved in cache after finding it in the buffer");
        }

        [Fact]
        public async Task MultipleEvents_InWrongOrder_ShouldBeHandledCorrectly()
        {
            // Arrange
            string customerId = "60";
            string invoiceId = "414";
            string invoiceLineId = "100";

            // Creating INVOICE event that refers to CUSTOMER with ID=60
            var invoiceEvent = TestHelpers.CreateEvent("INVOICE", invoiceId);
            var payload = Newtonsoft.Json.Linq.JObject.Parse(invoiceEvent.Payload);
            var innerPayload = Newtonsoft.Json.Linq.JObject.Parse(payload["payload"].ToString());
            innerPayload["CustomerId"] = customerId;
            payload["payload"] = innerPayload.ToString();
            invoiceEvent.Payload = payload.ToString();

            // Creating INVOICELINE event that refers to INVOICE with ID=60
            var invoiceLineEvent = TestHelpers.CreateEvent("INVOICELINE", invoiceId, invoiceLineId);

            // CUSTOMER event that will appear later in the buffer
            var customerEvent = TestHelpers.CreateEvent("CUSTOMER", customerId);

            // The consumer buffer will have a pending CUSTOMER event
            var consumerBufferMock = new Mock<IConsumer<Ignore, string>>();
            consumerBufferMock.Setup(c => c.Consume(It.IsAny<TimeSpan>()))
                .Returns(TestHelpers.CreateConsumeResult("CUSTOMER", customerId));

            var eventsToProcess = new List<EventMessage> { invoiceEvent, invoiceLineEvent };
            var consumedResults = new List<ConsumeResult<Ignore, string>>();
            var deferredKafkaEvents = new List<EventMessage>();

            // Priority list where CUSTOMER comes before INVOICE
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
            // The test will show that the current implementation doesn't handle this situation correctly,
            // because we don't yet have logic that handles dependencies between CUSTOMER and INVOICE

            // Check if CUSTOMER was found and added to buffers
            bool foundCustomer = result.Exists(e => TestHelpers.ExtractEventType(e.Payload) == "CUSTOMER" &&
                                                  TestHelpers.ExtractAggregateId(e.Payload) == customerId);
            // Check if INVOICE is in the results
            bool invoiceStillExists = result.Exists(e => TestHelpers.ExtractEventType(e.Payload) == "INVOICE");

            // In an ideal scenario, customer should be added and invoice should exist
            // However, our test will show that the current implementation doesn't handle this correctly
            Assert.True(foundCustomer, "Customer event should be retrieved from buffer and added to the event list");
            Assert.True(invoiceStillExists, "Invoice event should remain in the event buffer");

            // Check if customer was marked in cache
            bool customerInCache = _memoryCache.TryGetValue("CUSTOMER:60", out _);
            Assert.True(customerInCache, "Customer should be saved in cache after finding it in the buffer");

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
        public void SortEvents_ShouldSortInCorrectOrder_WithDependencies()
        {
            // Arrange
            int bufferId = 1;
            var priorityList = TestHelpers.GetFullPriorityList();
            string customerId = "60";
            string invoiceId = "414";
            string invoiceLineId = "100";

            // Creating INVOICE event that refers to CUSTOMER with ID=60
            var invoiceEvent = TestHelpers.CreateEvent("INVOICE", invoiceId);
            var payload = Newtonsoft.Json.Linq.JObject.Parse(invoiceEvent.Payload);
            var innerPayload = Newtonsoft.Json.Linq.JObject.Parse(payload["payload"].ToString());
            innerPayload["CustomerId"] = customerId;
            payload["payload"] = innerPayload.ToString();
            invoiceEvent.Payload = payload.ToString();

            // Creating INVOICELINE event that refers to INVOICE with ID=60
            var invoiceLineEvent = TestHelpers.CreateEvent("INVOICELINE", invoiceId, invoiceLineId);

            // CUSTOMER event that will appear later in the buffer
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
            // We create payloads for Invoice containing CustomerId, similarly as in logs

            // Prepare inner payload as object
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

            // Serialize inner payload as JSON string
            string innerPayloadJson = JsonConvert.SerializeObject(innerPayload);

            // Prepare outer payload as object
            var outerPayload = new
            {
                event_id = 2,
                aggregate_id = 414,
                aggregate_type = "INVOICE",
                event_type = "CREATED",
                payload = innerPayloadJson, // here goes string, not object
                unique_identifier = Guid.NewGuid().ToString(),
                created_at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                __deleted = "false",
                __op = "c",
                __source_ts_ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                __source_table = "invoice_outbox",
                __source_name = "old_to_new",
                __query = (string)null
            };

            // Serialize inner payload as JSO
            string fullPayload = JsonConvert.SerializeObject(outerPayload);

            // Act - we call ExtractDependencyId method
            string customerId = _service.ExtractDependencyId(fullPayload, "CustomerId");

            // Assert
            // We check if CustomerId was correctly extracted from payload
            Assert.Equal("60", customerId);

            // Here we check if current implementation is able to identify dependency
            // between INVOICE and CUSTOMER - we use PriorityDependency class
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

            // We also check if GetDependency method returns correct INVOICE dependency
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

            // Create inner payload (Invoice)
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

            // Create outer payload (event)
            var outerPayload = new
            {
                event_id = 2,
                aggregate_id = 414,
                aggregate_type = "INVOICE",
                event_type = "CREATED",
                payload = innerPayloadJson, // inner string
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

            // Create EventMessage object from this payload
            var invoiceEvent = new EventMessage
            {
                Timestamp = DateTime.UtcNow,
                Payload = fullInvoicePayload
            };

            // List of priorities where CUSTOMER is before INVOICE (as it should be)
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
    }
} 