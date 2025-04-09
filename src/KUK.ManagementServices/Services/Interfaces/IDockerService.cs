using KUK.Common.Utilities;
using KUK.ManagementServices.Utilities;

namespace KUK.ManagementServices.Services.Interfaces
{
    public interface IDockerService
    {
        Task<ServiceActionStatus> StartContainerAsync(string containerName);
        Task<ServiceActionStatus> StopContainerAsync(string containerName);
        Task<ServiceActionStatus> RestartContainerAsync(string containerName);
        Task<ServiceActionStatus> RemoveContainerAsync(string containerName);
        Task<Dictionary<string, string>> GetContainerStatusesAsync(string[] containerNames);
        Task<ServiceActionStatus> CopyCnfFileAsync(string sourcePath, string destinationPath);
        Task<ServiceActionStatus> ChangeCnfFilePermissionsAsync(string filePath, string permissions);
        Task<ServiceActionStatus> ExecuteSqlScriptAsync(string scriptPath);
    }
}
