using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using Npgsql;

namespace KUK.Common.Services
{
    public class DatabaseDirectConnectionService : IDatabaseDirectConnectionService
    {
        private readonly IConfiguration _configuration;

        public DatabaseDirectConnectionService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public MySqlConnectionStringBuilder GetOldDatabaseConnection(bool nullifyDatabaseName = false)
        {
            string environmentDestination = Environment.GetEnvironmentVariable("DebeziumWorker_EnvironmentDestination");
            string envVariableOldDebeziumPassword = $"DebeziumWorker_{environmentDestination}_OldDatabaseDebeziumPassword";
            var newDatabasePassword = Environment.GetEnvironmentVariable(envVariableOldDebeziumPassword);
            var serverName = _configuration["Databases:OldDatabase:ServerName"];
            var serverPort = _configuration["Databases:OldDatabase:ServerPort"];
            var oldDatabaseConnectionString = _configuration["Databases:OldDatabase:ConnectionString"]
                .Replace("{password}", newDatabasePassword)
                .Replace("{serverName}", serverName)
                .Replace("{serverPort}", serverPort);
            var connectionStringBuilder = new MySqlConnectionStringBuilder(oldDatabaseConnectionString);
            if (nullifyDatabaseName)
            {
                connectionStringBuilder.Database = null;
            }
            return connectionStringBuilder;
        }

        public NpgsqlConnectionStringBuilder GetNewDatabaseConnection(string databaseName = "defaultdb")
        {
            string environmentDestination = Environment.GetEnvironmentVariable("DebeziumWorker_EnvironmentDestination");
            string envVariableNewDebeziumPassword = $"DebeziumWorker_{environmentDestination}_NewDatabaseDebeziumPassword";
            var newDatabasePassword = Environment.GetEnvironmentVariable(envVariableNewDebeziumPassword);
            var serverName = _configuration["Databases:NewDatabase:ServerName"];
            var serverPort = _configuration["Databases:NewDatabase:ServerPort"];
            var newDatabaseConnectionString = _configuration["Databases:NewDatabase:ConnectionString"]
                .Replace("{password}", newDatabasePassword)
                .Replace("{serverName}", serverName)
                .Replace("{serverPort}", serverPort);
            var connectionStringBuilder = new NpgsqlConnectionStringBuilder(newDatabaseConnectionString);
            connectionStringBuilder.Database = databaseName; // By default Postgres uses username as database name, hence we need different one
            return connectionStringBuilder;
        }
    }
}
