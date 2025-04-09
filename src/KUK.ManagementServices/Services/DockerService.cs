using System.Diagnostics;
using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;
using KUK.Common;
using KUK.Common.Services;
using KUK.Common.Utilities;
using KUK.ManagementServices.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace KUK.ManagementServices.Services
{
    public class DockerService : IDockerService
    {
        private readonly DockerClient _client;
        private readonly AppSettingsConfig _appSettingsConfig;
        private readonly ILogger<DockerService> _logger;
        private readonly IUtilitiesService _utilitiesService;

        private const string OLD_DATABASE_CONTAINER_NAME = "mysql80";

        public DockerService(
            AppSettingsConfig appSettingsConfig,
            ILogger<DockerService> logger,
            IUtilitiesService utilitiesService)
        {
            _client = new DockerClientConfiguration(new Uri("npipe://./pipe/docker_engine")).CreateClient();
            _appSettingsConfig = appSettingsConfig;
            _logger = logger;
            _utilitiesService = utilitiesService;
        }

        public async Task<ServiceActionStatus> StartContainerAsync(string containerName)
        {
            try
            {
                var containerExists = await ContainerExistsAsync(containerName);

                if (containerExists)
                {
                    await _client.Containers.StartContainerAsync(containerName, new ContainerStartParameters());
                    return new ServiceActionStatus { Success = true, Message = "Container started successfully." };
                }
                else
                {
                    var workingDirectory = GetDockerComposeDirectory();

                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = "docker-compose",
                        Arguments = $"up -d {containerName}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = workingDirectory
                    };

                    using (var process = new Process { StartInfo = processStartInfo })
                    {
                        process.Start();
                        string output = await process.StandardOutput.ReadToEndAsync();
                        string error = await process.StandardError.ReadToEndAsync();
                        process.WaitForExit();

                        if (process.ExitCode == 0)
                        {
                            return new ServiceActionStatus { Success = true, Message = "Container started successfully." };
                        }
                        else
                        {
                            return new ServiceActionStatus { Success = false, Message = $"Error: {error}" };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return new ServiceActionStatus { Success = false, Message = $"Error: {ex.Message}" };
            }
        }

        public async Task<ServiceActionStatus> StopContainerAsync(string containerName)
        {
            try
            {
                await _client.Containers.StopContainerAsync(containerName, new ContainerStopParameters());
                return new ServiceActionStatus { Success = true, Message = "Container stopped successfully." };
            }
            catch (Exception ex)
            {
                return new ServiceActionStatus { Success = false, Message = $"Error: {ex.Message}" };
            }
        }

        public async Task<ServiceActionStatus> RestartContainerAsync(string containerName)
        {
            try
            {
                // Stop the container
                var stopStatus = await StopContainerAsync(containerName);
                if (!stopStatus.Success)
                {
                    return new ServiceActionStatus { Success = false, Message = $"Failed to stop container: {stopStatus.Message}" };
                }

                // Start the container
                var startStatus = await StartContainerAsync(containerName);
                if (!startStatus.Success)
                {
                    return new ServiceActionStatus { Success = false, Message = $"Failed to start container: {startStatus.Message}" };
                }

                return new ServiceActionStatus { Success = true, Message = "Container restarted successfully." };
            }
            catch (Exception ex)
            {
                return new ServiceActionStatus { Success = false, Message = $"Error: {ex.Message}" };
            }
        }

        public async Task<ServiceActionStatus> RemoveContainerAsync(string containerName)
        {
            try
            {
                await _client.Containers.RemoveContainerAsync(containerName, new ContainerRemoveParameters { Force = true, RemoveVolumes = true });
                return new ServiceActionStatus { Success = true, Message = "Container removed successfully." };
            }
            catch (Docker.DotNet.DockerContainerNotFoundException ex)
            {
                return new ServiceActionStatus { Success = false, Message = $"Error: {ex.Message}" };
            }
            catch (Exception ex)
            {
                return new ServiceActionStatus { Success = false, Message = $"Error: {ex.Message}" };
            }
        }

        public async Task<Dictionary<string, string>> GetContainerStatusesAsync(string[] containerNames)
        {
            var containers = await _client.Containers.ListContainersAsync(new ContainersListParameters { All = true });
            var statuses = new Dictionary<string, string>();

            foreach (var name in containerNames)
            {
                var container = containers.FirstOrDefault(c => c.Names.Contains("/" + name));
                statuses[name] = container?.State ?? "Not Found";
            }

            return statuses;
        }

        public async Task<ServiceActionStatus> ExecuteSqlScriptAsync(string scriptPath)
        {
            try
            {
                string environmentDestination = _utilitiesService.GetEnvironmentDestinationString();
                string oldDatabaseRootPasswordEnvironmentVariableName = $"DebeziumWorker_{environmentDestination}_OldDatabaseRootPassword";

                var mySqlRootPassword = Environment.GetEnvironmentVariable(oldDatabaseRootPasswordEnvironmentVariableName);
                if (string.IsNullOrEmpty(mySqlRootPassword))
                {
                    throw new InvalidOperationException($"Environment variable {oldDatabaseRootPasswordEnvironmentVariableName} cannot be found");
                }

                var execCreateResponse = await _client.Exec.ExecCreateContainerAsync(OLD_DATABASE_CONTAINER_NAME, new ContainerExecCreateParameters
                {
                    AttachStdout = true,
                    AttachStderr = true,
                    Env = new List<string> { $"MYSQL_PWD={mySqlRootPassword}" },
                    Cmd = new[] { "sh", "-c", $"mysql -u root < {scriptPath}" }
                });

                using (var execStartResponse = await _client.Exec.StartWithConfigContainerExecAsync(execCreateResponse.ID, new ContainerExecStartParameters()))
                {
                    var output = await ReadStreamAsync(execStartResponse);
                    return new ServiceActionStatus
                    {
                        Success = string.IsNullOrEmpty(output),
                        Message = string.IsNullOrEmpty(output) ? "SQL script executed successfully." : output
                    };
                }
            }
            catch (Exception ex)
            {
                return new ServiceActionStatus { Success = false, Message = $"Error: {ex.Message}" };
            }
        }

        public async Task<ServiceActionStatus> CopyCnfFileAsync(string sourcePath, string destinationPath)
        {
            try
            {
                var execCreateResponse = await _client.Exec.ExecCreateContainerAsync(OLD_DATABASE_CONTAINER_NAME, new ContainerExecCreateParameters
                {
                    AttachStdout = true,
                    AttachStderr = true,
                    Cmd = new[] { "cp", sourcePath, destinationPath }
                });

                using (var execStartResponse = await _client.Exec.StartWithConfigContainerExecAsync(execCreateResponse.ID, new ContainerExecStartParameters()))
                {
                    var output = await ReadStreamAsync(execStartResponse);
                    return new ServiceActionStatus
                    {
                        Success = string.IsNullOrEmpty(output),
                        Message = string.IsNullOrEmpty(output) ? "CNF file copied successfully." : output
                    };
                }
            }
            catch (Exception ex)
            {
                return new ServiceActionStatus { Success = false, Message = $"Error: {ex.Message}" };
            }
        }

        public async Task<ServiceActionStatus> ChangeCnfFilePermissionsAsync(string filePath, string permissions)
        {
            try
            {
                var execCreateResponse = await _client.Exec.ExecCreateContainerAsync(OLD_DATABASE_CONTAINER_NAME, new ContainerExecCreateParameters
                {
                    AttachStdout = true,
                    AttachStderr = true,
                    Cmd = new[] { "chmod", permissions, filePath }
                });

                using (var execStartResponse = await _client.Exec.StartWithConfigContainerExecAsync(execCreateResponse.ID, new ContainerExecStartParameters()))
                {
                    var output = await ReadStreamAsync(execStartResponse);
                    return new ServiceActionStatus
                    {
                        Success = string.IsNullOrEmpty(output),
                        Message = string.IsNullOrEmpty(output) ? "CNF file permissions changed successfully." : output
                    };
                }
            }
            catch (Exception ex)
            {
                return new ServiceActionStatus { Success = false, Message = $"Error: {ex.Message}" };
            }
        }

        private string GetDockerComposeDirectory()
        {
            // REMARK: We may consider moving this method to AppSettingsConfigExtensions.AddAppSettingsConfig

            try
            {
                var baseDirectory = new DirectoryInfo(AppContext.BaseDirectory);
                var defaultDirectory = Path.Combine(baseDirectory.Parent.Parent.Parent.Parent.Parent.FullName, "docker");
                if (Directory.Exists(defaultDirectory) && File.Exists(Path.Combine(defaultDirectory, "docker-compose.yml")))
                {
                    return defaultDirectory;
                }
            }
            catch
            {
            }

            try
            {
                var dockerComposeDirectory = _appSettingsConfig.DockerComposeDirectory;
                if (!string.IsNullOrEmpty(dockerComposeDirectory)
                    && Directory.Exists(dockerComposeDirectory)
                    && File.Exists(Path.Combine(dockerComposeDirectory, "docker-compose.yml")))
                {
                    return dockerComposeDirectory;
                }
            }
            catch
            {
            }

            throw new InvalidOperationException(
                "Cannot find path with docker-compose.yml. Try setting it in DockerComposeDirectory in your appsettings.json file.");
        }

        private async Task<bool> ContainerExistsAsync(string containerName)
        {
            try
            {
                var containers = await _client.Containers.ListContainersAsync(new ContainersListParameters { All = true });
                return containers.Any(c => c.Names.Contains($"/{containerName}"));
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<string> ReadStreamAsync(MultiplexedStream stream)
        {
            var buffer = new byte[1024];
            var output = new StringBuilder();
            MultiplexedStream.ReadResult readResult;

            while ((readResult = await stream.ReadOutputAsync(buffer, 0, buffer.Length, default)).Count > 0)
            {
                output.Append(Encoding.UTF8.GetString(buffer, 0, readResult.Count));
            }

            return output.ToString();
        }
    }
}