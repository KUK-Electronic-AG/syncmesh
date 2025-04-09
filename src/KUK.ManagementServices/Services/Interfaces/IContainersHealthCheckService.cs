using KUK.Common.Utilities;

namespace KUK.ManagementServices.Services.Interfaces
{
    public interface IContainersHealthCheckService
    {
        Task<ServiceActionStatus> WaitForContainersAsync(List<string> containerNames, TimeSpan timeout);
        Task<ServiceActionStatus> WaitForServicesInContainersAsync(List<string> containerNames, TimeSpan timeout);
        Task<bool> IsKafkaHealthyAsync(string bootstrapServers);
        Task<bool> IsSchemaRegistryHealthyAsync(string url);
        Task<bool> IsKafkaConnectHealthyAsync(string url);
        Task<bool> IsControlCenterHealthyAsync(string url);
        Task<bool> IsMySqlHealthyAsync(string connectionString);
        Task<bool> IsPostgresHealthyAsync(string connectionString);
        Task<bool> IsDebeziumHealthyAsync(string url);
    }
}
