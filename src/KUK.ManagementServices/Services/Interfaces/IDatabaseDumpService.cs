using KUK.Common.Utilities;
using KUK.ManagementServices.Utilities;

namespace KUK.ManagementServices.Services.Interfaces
{
    public interface IDatabaseDumpService
    {
        Task<ServiceActionStatus> CreateDatabaseBackupAsync(
            string containerName,
            string databaseName,
            string backupFilePath,
            string username,
            string password);

        Task<ServiceActionStatus> CopyBackupToHostAsync(
            string containerName,
            string containerBackupPath,
            string hostBackupPath);

        Task<ServiceActionStatus> CopyBackupToContainerAsync(
            string hostBackupPath,
            string containerName,
            string containerBackupPath);

        BinLogPositionResult GetBinlogPositionFromDump(string dumpFilePath);

        Task<ServiceActionStatus> RestoreDatabaseBackupAsync(
            string containerName,
            string containerBackupPath,
            string databaseName,
            string username,
            string password);
    }
}
