using KUK.ManagementServices.Services.Interfaces;
using KUK.ManagementServices.Utilities;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace KUK.ManagementServices.Services
{
    public class DatabaseDumpService : IDatabaseDumpService
    {
        private readonly ILogger<DatabaseDumpService> _logger;

        public DatabaseDumpService(ILogger<DatabaseDumpService> logger)
        {
            _logger = logger;
        }

        public async Task<ServiceActionStatus> CreateDatabaseBackupAsync(
            string containerName,
            string databaseName,
            string containerBackupPath,
            string username,
            string password)
        {
            var backupCommand = $"docker exec {containerName} sh -c \"mysqldump --master-data=2 -u {username} -p{password} {databaseName} > {containerBackupPath}\"";
            var result = await ExecuteShellCommandAsync(backupCommand);
            return result.Result ? new ServiceActionStatus { Success = true, Message = "Backup created successfully in container" }
                          : new ServiceActionStatus { Success = false, Message = $"Failed to create backup in container. Error: {result.Error}" };
        }

        public async Task<ServiceActionStatus> CopyBackupToHostAsync(
            string containerName,
            string containerBackupPath,
            string hostBackupPath)
        {
            var copyCommand = $"docker cp {containerName}:{containerBackupPath} {hostBackupPath}";
            var result = await ExecuteShellCommandAsync(copyCommand);
            return result.Result ? new ServiceActionStatus { Success = true, Message = "Backup copied to host successfully" }
                          : new ServiceActionStatus { Success = false, Message = $"Failed to copy backup to host. Error: {result.Error}" };
        }

        public async Task<ServiceActionStatus> CopyBackupToContainerAsync(
            string hostBackupPath,
            string containerName,
            string containerBackupPath)
        {
            var copyCommand = $"docker cp {hostBackupPath} {containerName}:{containerBackupPath}";
            var result = await ExecuteShellCommandAsync(copyCommand);
            return result.Result ? new ServiceActionStatus { Success = true, Message = "Backup copied to container successfully" }
                          : new ServiceActionStatus { Success = false, Message = $"Failed to copy backup to container. Error: {result.Error}" };
        }

        public BinLogPositionResult GetBinlogPositionFromDump(string dumpFilePath)
        {
            var lines = File.ReadAllLines(dumpFilePath);
            foreach (var line in lines)
            {
                if (line.StartsWith("-- CHANGE MASTER TO"))
                {
                    var parts = line.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    var binlogFile = parts.FirstOrDefault(p => p.Contains("MASTER_LOG_FILE")).Split('=')[1].Trim().Trim('\'');
                    var binlogPosition = long.Parse(parts.FirstOrDefault(p => p.Contains("MASTER_LOG_POS")).Split('=')[1].Trim());
                    var result = new BinLogPositionResult() { BinLogFile = binlogFile, BinlogPosition = binlogPosition };
                    return result;
                }
            }
            throw new Exception("Binlog position not found in dump file");
        }

        public async Task<ServiceActionStatus> RestoreDatabaseBackupAsync(
            string containerName,
            string containerBackupPath,
            string databaseName,
            string username,
            string password)
        {
            // Create schema (it doesn't exist at this point)
            var createSchemaCommand = $"docker exec {containerName} sh -c \"mysql -u {username} -p{password} -e 'CREATE DATABASE IF NOT EXISTS {databaseName};'\"";
            var result = await ExecuteShellCommandAsync(createSchemaCommand);
            if (!result.Result) return new ServiceActionStatus { Success = false, Message = $"Failed to create schema in container. Error: {result.Error}" };

            // Restore from backup to just created schema
            var restoreCommand = $"docker exec -i {containerName} sh -c \"mysql -u {username} -p{password} {databaseName} < {containerBackupPath}\"";
            result = await ExecuteShellCommandAsync(restoreCommand);
            return result.Result ? new ServiceActionStatus { Success = true, Message = "Backup restored successfully" }
                          : new ServiceActionStatus { Success = false, Message = $"Failed to restore backup. Error: {result.Error}" };
        }

        private async Task<ExecuteShellCommandResult> ExecuteShellCommandAsync(string command)
        {
            try
            {
                var processInfo = new ProcessStartInfo("cmd.exe", $"/c {command}")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using (var process = new Process { StartInfo = processInfo })
                {
                    process.Start();
                    await process.WaitForExitAsync();
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();

                    if (process.ExitCode != 0)
                    {
                        _logger.LogError($"Command failed with error: {error}");
                        return new ExecuteShellCommandResult() { Result = false, Error = error };
                    }

                    _logger.LogInformation($"Command output: {output}");
                    return new ExecuteShellCommandResult() { Result = true, Output = output };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception while executing command: {ex.Message}");
                return new ExecuteShellCommandResult() { Result = false, Error = ex.ToString() };
            }
        }
    }
}
