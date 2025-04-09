using KUK.Common.Utilities;
using KUK.ManagementServices.Utilities;

namespace KUK.ManagementServices.Services.Interfaces
{
    public interface ISchemaInitializerService
    {
        ServiceActionStatus InitializeSchema(WhichDatabaseEnum database, bool asRoot = false);
        ServiceActionStatus InitializeSchemaAndTables(WhichDatabaseEnum database, bool asRoot = false);
        Task<ServiceActionStatus> InitializeSchemaTablesAndData(WhichDatabaseEnum database, string userName);
        ServiceActionStatus DeleteSchema(WhichDatabaseEnum database, bool asRoot = false);
        ServiceActionStatus DeleteTables(WhichDatabaseEnum database, bool asRoot = false);
        ServiceActionStatus DeleteData(WhichDatabaseEnum database, bool asRoot = false);
    }
}
