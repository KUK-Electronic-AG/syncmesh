using KUK.Common.Contexts;
using KUK.Common.MigrationLogic.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace KUK.Common.MigrationLogic
{
    public class TriggersCreationService : ITriggersCreationService
    {
        private readonly Chinook2Context _newContext;
        private readonly ILogger<DataMigrationRunner> _logger;

        public TriggersCreationService(
            Chinook2Context newContext,
            ILogger<DataMigrationRunner> logger)
        {
            _newContext = newContext;
            _logger = logger;
        }

        public async Task CreateTriggers()
        {
            var sql = GetEmbeddedResource();
            int result = await _newContext.Database.ExecuteSqlRawAsync(sql);
            _logger.LogInformation($"Creating triggers result: {result}");

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
