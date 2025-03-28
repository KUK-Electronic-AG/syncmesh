using KUK.KafkaProcessor.Services;
using KUK.KafkaProcessor.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace KUK.UnitTests
{
    public class ExtractSnapshotPropertiesTests
    {
        private readonly EventsSortingService _service;
        private readonly IConfiguration _configuration;
        private readonly Mock<ILogger<EventsSortingService>> _loggerMock;

        public ExtractSnapshotPropertiesTests()
        {
            _loggerMock = new Mock<ILogger<EventsSortingService>>();
            var invoiceServiceMock = new Mock<IInvoiceService>();
            var customerServiceMock = new Mock<ICustomerService>();
            var addressServiceMock = new Mock<IAddressService>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            // Konfiguracja zawiera wymagany klucz dla cache.
            _configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { "InternalKafkaProcessorParameters:MemoryCacheExpirationInSeconds", "60" }
            }).Build();
            _service = new EventsSortingService(
                _loggerMock.Object, invoiceServiceMock.Object, customerServiceMock.Object, addressServiceMock.Object, memoryCache, _configuration);
        }

        [Fact]
        public void ExtractAggregateId_ForSnapshotPayload_ReturnsEmpty()
        {
            string payload = @"
            {
                ""source"": {
                    ""version"": ""2.5.4.Final"",
                    ""connector"": ""mysql"",
                    ""name"": ""old_to_new"",
                    ""ts_ms"": 1742551092765,
                    ""snapshot"": ""true"",
                    ""db"": ""db_chinook1"",
                    ""sequence"": null,
                    ""table"": ""Track"",
                    ""server_id"": 0,
                    ""gtid"": null,
                    ""file"": ""mysql-bin.000001"",
                    ""pos"": 157,
                    ""row"": 0,
                    ""thread"": null,
                    ""query"": null
                },
                ""ts_ms"": 1742551092767,
                ""databaseName"": ""db_chinook1"",
                ""schemaName"": null,
                ""ddl"": ""DROP TABLE IF EXISTS `db_chinook1`.`Track`"",
                ""tableChanges"": [
                    {
                        ""type"": ""DROP"",
                        ""id"": ""db_chinook1.Track"",
                        ""table"": null
                    }
                ]
            }";
            string result = _service.ExtractAggregateId(payload);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void ExtractEventType_ForSnapshotPayload_ReturnsEmpty()
        {
            string payload = @"
            {
                ""source"": {
                    ""version"": ""2.5.4.Final"",
                    ""connector"": ""mysql"",
                    ""name"": ""old_to_new"",
                    ""ts_ms"": 1742551092432,
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
                ""ts_ms"": 1742551092721,
                ""databaseName"": """",
                ""schemaName"": null,
                ""ddl"": ""SET character_set_server=utf8mb4, collation_server=utf8mb4_0900_ai_ci"",
                ""tableChanges"": []
            }";
            string result = _service.ExtractEventType(payload);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void ExtractEventSource_ForSnapshotPayload_ReturnsEmpty()
        {
            string payload = @"
            {
                ""source"": {
                    ""version"": ""2.5.4.Final"",
                    ""connector"": ""mysql"",
                    ""name"": ""old_to_new"",
                    ""ts_ms"": 1742551092432,
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
                ""ts_ms"": 1742551092721,
                ""databaseName"": """",
                ""schemaName"": null,
                ""ddl"": ""SET character_set_server=utf8mb4, collation_server=utf8mb4_0900_ai_ci"",
                ""tableChanges"": []
            }";
            string result = _service.ExtractEventSource(payload);
            Assert.Equal(string.Empty, result);
        }
    }
}
