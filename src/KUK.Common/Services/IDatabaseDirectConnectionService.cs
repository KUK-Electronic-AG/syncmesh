using MySql.Data.MySqlClient;
using Npgsql;

namespace KUK.Common.Services
{
    public interface IDatabaseDirectConnectionService
    {
        MySqlConnectionStringBuilder GetOldDatabaseConnection(bool nullifyDatabaseName = false);
        NpgsqlConnectionStringBuilder GetNewDatabaseConnection(string databaseName = "defaultdb");
    }
}
