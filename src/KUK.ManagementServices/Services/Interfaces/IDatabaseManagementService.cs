using KUK.Common;
using KUK.ManagementServices.Utilities;

namespace KUK.ManagementServices.Services.Interfaces
{
    public interface IDatabaseManagementService
    {
        Task<DbStatuses> CheckDatabasesStatus(DatabasesBasicConfiguration dbConfig);
        Task<bool> CheckDatabaseConnectionAsync(AppSettingsConfig appSettingsConfig, WhichDatabaseEnum whichDatabaseEnum, string userName = "");
        Task<bool> CheckDatabaseConnectionAsync(DatabasesBasicConfiguration databasesBasicConfiguration, WhichDatabaseEnum whichDatabaseEnum, string userName = "");
        Task<bool> CheckSchemaExistsAsync(string connectionString, string schemaName, WhichDatabaseEnum whichDatabaseEnum);
        Task<bool> CheckSchemaHasDataAsync(string connectionString, string schemaName, string tableName, WhichDatabaseEnum whichDatabaseEnum);
    }

}
