using System;
using System.Collections.Generic;
using KUK.KafkaProcessor.EventProcessing;
using KUK.KafkaProcessor.Services;
using KUK.KafkaProcessor.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace KUK.UnitTests
{
    public class ExtractDependencyAggregateIdTests
    {
        private readonly EventsSortingService _service;
        private readonly IConfiguration _configuration;
        private readonly Mock<ILogger<EventsSortingService>> _loggerMock;

        public ExtractDependencyAggregateIdTests()
        {
            _loggerMock = new Mock<ILogger<EventsSortingService>>();
            var invoiceServiceMock = new Mock<IInvoiceService>();
            var customerServiceMock = new Mock<ICustomerService>();
            var addressServiceMock = new Mock<IAddressService>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            // Upewniamy się, że konfiguracja zawiera niezbędne parametry, m.in. dla cache.
            _configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { "InternalKafkaProcessorParameters:MemoryCacheExpirationInSeconds", "60" }
            }).Build();
            _service = new EventsSortingService(
                _loggerMock.Object, invoiceServiceMock.Object, customerServiceMock.Object, addressServiceMock.Object, memoryCache, _configuration);
        }

        [Fact]
        public void ExtractDependencyAggregateId_ReturnsInnerValue_WhenPropertyExists_CaseInsensitive()
        {
            // Przykładowy payload dla INVOICELINE z inner payload zawierającym "InvoiceId"
            string payload = @"{
                ""event_id"": 38,
                ""aggregate_id"": 431,
                ""aggregate_type"": ""INVOICELINE"",
                ""event_type"": ""CREATED"",
                ""payload"": {""TrackId"": 2, ""Quantity"": 2, ""InvoiceId"": 431, ""UnitPrice"": 2.00, ""InvoiceLineId"": 2278},
                ""unique_identifier"": ""1f096bd5-0599-11f0-8018-2acab67196ed"",
                ""created_at"": 1742481565733,
                ""__deleted"": ""false"",
                ""__op"": ""c"",
                ""__source_ts_ms"": 1742481565739,
                ""__source_table"": ""invoiceline_outbox"",
                ""__source_name"": ""old_to_new"",
                ""__query"": ""INSERT INTO `InvoiceLine` (`InvoiceId`, `Quantity`, `TrackId`, `UnitPrice`)\r\nVALUES (431, 2, 2, 2)""
            }";

            string result = _service.ExtractDependencyAggregateId(payload);
            Assert.Equal("431", result);
        }

        [Fact]
        public void ExtractDependencyAggregateId_ReturnsInnerValue_WithMapping()
        {
            string payload = @"{
                ""event_id"": 38,
                ""aggregate_id"": 431,
                ""aggregate_type"": ""INVOICELINE"",
                ""event_type"": ""CREATED"",
                ""payload"": {""TrackId"": 2, ""Quantity"": 2, ""InvoiceId"": 431, ""UnitPrice"": 2.00, ""InvoiceLineId"": 2278},
                ""unique_identifier"": ""1f096bd5-0599-11f0-8018-2acab67196ed"",
                ""created_at"": 1742481565733,
                ""__deleted"": ""false"",
                ""__op"": ""c"",
                ""__source_ts_ms"": 1742481565739,
                ""__source_table"": ""invoiceline_outbox"",
                ""__source_name"": ""old_to_new"",
                ""__query"": ""INSERT INTO `InvoiceLine` (`InvoiceId`, `Quantity`, `TrackId`, `UnitPrice`)\r\nVALUES (431, 2, 2, 2)""
            }";

            string result = _service.ExtractDependencyAggregateId(payload);
            Assert.Equal("431", result);
        }

        [Fact]
        public void ExtractDependencyAggregateId_FallsBackToOuterAggregateId_WhenInnerNotFound()
        {
            // Payload, w którym inner payload nie zawiera właściwości odpowiadającej dependency.
            string payload = @"{
                ""event_id"": 38,
                ""aggregate_id"": 999,
                ""aggregate_type"": ""INVOICELINE"",
                ""event_type"": ""CREATED"",
                ""payload"": ""{\""SomeOtherField\"": 123}"",
                ""unique_identifier"": ""some-guid"",
                ""created_at"": 1742481565733,
                ""__deleted"": ""false"",
                ""__op"": ""c"",
                ""__source_ts_ms"": 1742481565739,
                ""__source_table"": ""invoiceline_outbox"",
                ""__source_name"": ""old_to_new"",
                ""__query"": ""query""
            }";
            string result = _service.ExtractDependencyAggregateId(payload);
            Assert.Equal("999", result);
        }

        [Fact]
        public void ExtractDependencyAggregateId_ReturnsEmpty_WhenPayloadInvalid()
        {
            // Nieprawidłowy JSON
            string payload = "not a valid json";
            string result = _service.ExtractDependencyAggregateId(payload);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void ExtractDependencyAggregateId_ReturnsDefaultOuter_WhenNoInnerPayload()
        {
            // Payload bez właściwości "payload"
            string payload = @"{
                ""event_id"": 38,
                ""aggregate_id"": 777,
                ""aggregate_type"": ""INVOICELINE"",
                ""event_type"": ""CREATED"",
                ""unique_identifier"": ""guid"",
                ""created_at"": 1742481565733,
                ""__deleted"": ""false"",
                ""__op"": ""c"",
                ""__source_ts_ms"": 1742481565739,
                ""__source_table"": ""invoiceline_outbox"",
                ""__source_name"": ""old_to_new"",
                ""__query"": ""query""
            }";
            // Brak inner payload → powinniśmy zwrócić outer aggregate_id ("777").
            string result = _service.ExtractDependencyAggregateId(payload);
            Assert.Equal("777", result);
        }

        [Fact]
        public void ExtractDependencyAggregateId_ForSnapshotPayload_ReturnsEmpty()
        {
            // Payload snapshotu
            string payload = @"{
                ""source"": {
                    ""version"": ""2.5.4.Final"",
                    ""connector"": ""mysql"",
                    ""name"": ""old_to_new"",
                    ""ts_ms"": 1742481546609,
                    ""snapshot"": ""true"",
                    ""db"": """",
                    ""sequence"": null,
                    ""table"": null,
                    ""server_id"": 0,
                    ""gtid"": null,
                    ""file"": ""mysql-bin.000001"",
                    ""pos"": 157,
                    ""row"": 0,
                    ""thread"": null,
                    ""query"": null
                },
                ""ts_ms"": 1742481546838,
                ""databaseName"": """",
                ""schemaName"": null,
                ""ddl"": ""SET character_set_server=utf8mb4, collation_server=utf8mb4_0900_ai_ci"",
                ""tableChanges"": []
            }";
            // Brak aggregate_id i inner payload → metoda powinna zwrócić pusty string.
            string result = _service.ExtractDependencyAggregateId(payload);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void ExtractAggregateId_ForSnapshotPayload_ReturnsEmptyInsteadOfThrowing()
        {
            // Arrange: przykładowy payload snapshotu
            string snapshotPayload = @"{
                ""source"": {
                    ""version"": ""2.5.4.Final"",
                    ""connector"": ""mysql"",
                    ""name"": ""old_to_new"",
                    ""ts_ms"": 1742481546609,
                    ""snapshot"": ""true"",
                    ""db"": """",
                    ""sequence"": null,
                    ""table"": null,
                    ""server_id"": 0,
                    ""gtid"": null,
                    ""file"": ""mysql-bin.000001"",
                    ""pos"": 157,
                    ""row"": 0,
                    ""thread"": null,
                    ""query"": null
                },
                ""ts_ms"": 1742481546838,
                ""databaseName"": """",
                ""schemaName"": null,
                ""ddl"": ""SET character_set_server=utf8mb4, collation_server=utf8mb4_0900_ai_ci"",
                ""tableChanges"": []
            }";

            // Act
            string result = _service.ExtractAggregateId(snapshotPayload);

            // Assert – dla snapshotu oczekujemy pustego ciągu
            Assert.Equal(string.Empty, result);
        }

    }
}
