using System.Reflection;
using System.Text;
using KUK.Common.Contexts;
using KUK.Common.MigrationLogic.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KUK.Common.MigrationLogic
{
    public class TriggersCreationService<TOld, TNew> : ITriggersCreationService<TOld, TNew>
    {
        private readonly NewDbContext _newContext;
        private readonly ILogger<TriggersCreationService<TOld, TNew>> _logger;

        public TriggersCreationService(
            NewDbContext newContext,
            ILogger<TriggersCreationService<TOld, TNew>> logger)
        {
            _newContext = newContext;
            _logger = logger;
        }

        public async Task CreateTriggers()
        {
            try
            {
                var sql = GetEmbeddedResource();
                int result = await _newContext.Database.ExecuteSqlRawAsync(sql);
                _logger.LogInformation($"Creating triggers result: {result}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating triggers: {ex}");
                throw;
            }

        }

        public string GenerateTrigger(
            string triggerName,
            string triggerEvent,
            string tableName,
            string outboxTable,
            string aggregateColumn,
            string aggregateType,
            string eventType,
            Dictionary<string, string> payloadMapping,
            string rowAlias)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"CREATE TRIGGER {triggerName}");
            sb.AppendLine($"AFTER {triggerEvent}");
            sb.AppendLine($"ON {tableName} FOR EACH ROW");
            sb.AppendLine("BEGIN");
            sb.AppendLine("    DECLARE unique_identifier VARCHAR(36);");
            sb.AppendLine("    SET unique_identifier = UUID();");
            sb.AppendLine();

            // Build the JSON_OBJECT expression dynamically based on payloadMapping.
            var jsonParts = new List<string>();
            foreach (var kvp in payloadMapping)
            {
                // Each pair produces something like: 'CustomerId', NEW.CustomerId
                jsonParts.Add($"'{kvp.Key}', {rowAlias}.{kvp.Value}");
            }
            var jsonObjectExpression = $"JSON_OBJECT({string.Join(", ", jsonParts)})";

            sb.AppendLine($"    INSERT INTO {outboxTable} (aggregate_id, aggregate_type, event_type, payload, unique_identifier)");
            sb.AppendLine("    VALUES(");
            sb.AppendLine($"        {rowAlias}.{aggregateColumn},");
            sb.AppendLine($"        '{aggregateType}',");
            sb.AppendLine($"        '{eventType}',");
            sb.AppendLine($"        {jsonObjectExpression},");
            sb.AppendLine("        unique_identifier");
            sb.AppendLine("    );");
            sb.AppendLine("END");

            return sb.ToString();
        }

        private string GetEmbeddedResource()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "KUK.Common.Assets.TriggersForNewDatabase.sql";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                string result = reader.ReadToEnd();
                return result;
            }
        }
    }
}
