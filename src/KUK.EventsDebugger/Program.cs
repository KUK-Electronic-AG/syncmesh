using KUK.EventsDebugger;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using Npgsql;

namespace EventsDebugger
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");

            ApplicationDestinationMode mode = GetOnlineOrDockerMode();

            IConfiguration config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile($"appsettings.{mode}.json", optional: false, reloadOnChange: true)
                .Build();

            string oldConnectionString = config["Databases:OldDatabase:ConnectionString"];
            string newConnectionString = config["Databases:NewDatabase:ConnectionString"];

            string environmentDestination = GetEnvironmentDestinationString();
            var oldPassword = GetDatabasePassword(environmentDestination, "Old");
            var newPassword = GetDatabasePassword(environmentDestination, "New");

            oldConnectionString = oldConnectionString.Replace("{password}", oldPassword);
            newConnectionString = newConnectionString.Replace("{password}", newPassword);

            Console.WriteLine("Old Database Connection String: " + oldConnectionString);
            Console.WriteLine("New Database Connection String: " + newConnectionString);

            var mysqlEntries = GetMySQLOutboxEntries(oldConnectionString);
            var postgresEntries = GetPostgresOutboxEntries(newConnectionString);

            var allEntries = mysqlEntries
                .Concat(postgresEntries)
                .OrderBy(entry => entry.CreatedAt)
                .ToList();

            Console.WriteLine("Sorted entries from all the tables:");
            var result = new List<string>();
            result.Add("SourceDatabase|SourceTable|EventId|AggregateId|AggregateType|EventType|Payload|UniqueIdentifier|CreatedAt");
            foreach (var entry in allEntries)
            {
                Console.WriteLine($"[{entry.CreatedAt}] {entry.SourceTable} - Entry: {entry}");
                result.Add($"{entry.SourceDatabase}|{entry.SourceTable}|{entry.EventId}|{entry.AggregateId}|{entry.AggregateType}|{entry.EventType}|{entry.Payload}|{entry.UniqueIdentifier}|{entry.CreatedAt}");
            }

            if (!Directory.Exists(@"C:\EventsDebugger"))
            {
                Directory.CreateDirectory(@"C:\EventsDebugger");
            }

            File.WriteAllLines(@$"C:\EventsDebugger\events-{DateTime.Now.ToString("yyyyMMdd-HHmmss-fffff")}.csv", result);
        }

        static List<OutboxEntry> GetMySQLOutboxEntries(string connectionString)
        {
            var entries = new List<OutboxEntry>();
            var tables = new string[] { "customer_outbox", "invoice_outbox", "invoiceline_outbox" };

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                foreach (var table in tables)
                {
                    string query = $"SELECT event_id, aggregate_id, aggregate_type, event_type, payload, unique_identifier, created_at FROM {table}";
                    using (var command = new MySqlCommand(query, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var temp = reader["event_id"].ToString();

                                var entry = new OutboxEntry
                                {
                                    EventId = reader["event_id"].ToString(),
                                    AggregateId = reader["aggregate_id"].ToString(),
                                    AggregateType = reader.GetString("aggregate_type"),
                                    EventType = reader.GetString("event_type"),
                                    Payload = reader.GetString("payload"),
                                    UniqueIdentifier = reader.GetString("unique_identifier"),
                                    CreatedAt = reader.GetDateTime("created_at"),
                                    SourceTable = table,
                                    SourceDatabase = "OLD"
                                };
                                entries.Add(entry);
                            }
                        }
                    }
                }
            }
            return entries;
        }

        static List<OutboxEntry> GetPostgresOutboxEntries(string connectionString)
        {
            var entries = new List<OutboxEntry>();
            var tables = new string[] { "AddressOutbox", "CustomerOutbox", "InvoiceLineOutbox", "InvoiceOutbox" };

            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                foreach (var table in tables)
                {
                    string query = $"SELECT event_id, aggregate_id, aggregate_type, event_type, payload, unique_identifier, created_at FROM \"{table}\"";
                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var entry = new OutboxEntry
                                {
                                    EventId = reader["event_id"].ToString(),
                                    AggregateId = reader["aggregate_id"].ToString(),
                                    AggregateType = reader.GetString(reader.GetOrdinal("aggregate_type")),
                                    EventType = reader.GetString(reader.GetOrdinal("event_type")),
                                    Payload = reader.GetString(reader.GetOrdinal("payload")),
                                    UniqueIdentifier = reader.GetString(reader.GetOrdinal("unique_identifier")),
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                    SourceTable = table,
                                    SourceDatabase = "NEW"
                                };
                                entries.Add(entry);
                            }
                        }
                    }
                }
            }
            return entries;
        }

        private static string GetDatabasePassword(string environmentDestination, string databaseName)
        {
            var databaseRootPasswordKey = $"DebeziumWorker_{environmentDestination}_{databaseName}DatabaseRootPassword";
            var oldDatabaseRootPassword = Environment.GetEnvironmentVariable(databaseRootPasswordKey);
            if (string.IsNullOrEmpty(oldDatabaseRootPassword))
            {
                throw new InvalidOperationException(
                    $"{databaseName} Database Root password is not set for {databaseRootPasswordKey}. " +
                    $"Expected environment variable to be present.");
            }
            return oldDatabaseRootPassword;
        }

        private static string GetEnvironmentDestinationString()
        {
            // REMARK: This is code duplication from UtilitiesService.GetEnvironmentDestinationString
            var environmentVariableName = "DebeziumWorker_EnvironmentDestination";
            string value = Environment.GetEnvironmentVariable(environmentVariableName);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Environment variable {environmentVariableName} is not set. Suggested values are Docker or Online.");
            }
            return value;
        }

        public static ApplicationDestinationMode GetOnlineOrDockerMode()
        {
            // Check if this is for Docker or Online run
            var environmentDestinationVariableName = "DebeziumWorker_EnvironmentDestination";
            string environmentDestination = Environment.GetEnvironmentVariable(environmentDestinationVariableName);
            if (string.IsNullOrWhiteSpace(environmentDestination))
                throw new ArgumentException($"Variable {environmentDestination} is not set in environment variables.");
            switch (environmentDestination.ToLower())
            {
                case "docker":
                    return ApplicationDestinationMode.Docker;
                case "online":
                    return ApplicationDestinationMode.Online;
                default:
                    throw new InvalidOperationException(
                        $"Unknown environment variable value for {environmentDestination}. " +
                        $"Found {environmentDestination}. Expected: Docker or Online");
            }
        }

        public enum ApplicationDestinationMode
        {
            Docker,
            Online
        }
    }
}
