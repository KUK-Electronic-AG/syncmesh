using KUK.Common.Contexts;
using KUK.Common.ModelsNewSchema.Outbox;
using KUK.Common.ModelsOldSchema;
using KUK.KafkaProcessor.Services.Interfaces;

namespace KUK.KafkaProcessor.Services
{
    public class UniqueIdentifiersService : IUniqueIdentifiersService
    {
        private readonly Chinook1DataChangesContext _oldContext;
        private readonly Chinook2Context _newContext;
        private readonly ILogger<UniqueIdentifiersService> _logger;

        public UniqueIdentifiersService(
            Chinook1DataChangesContext oldContext,
            Chinook2Context newContext,
            ILogger<UniqueIdentifiersService> logger)
        {
            _oldContext = oldContext;
            _newContext = newContext;
            _logger = logger;
        }

        public bool IdentifierExists(string identifier)
        {
            ProcessedMessagesOld? foundElementOld = _oldContext.ProcessedMessages.SingleOrDefault(pm => pm.UniqueIdentifier == identifier);
            ProcessedMessagesNew? foundElementNew = _newContext.ProcessedMessages.SingleOrDefault(pm => pm.UniqueIdentifier == identifier);
            return foundElementOld != null || foundElementNew != null;
        }

        public async Task MarkIdentifierAsProcessedInOldDatabase(string identifier, Chinook1DataChangesContext oldContext)
        {
            oldContext.ProcessedMessages.Add(new ProcessedMessagesOld { UniqueIdentifier = identifier });
            await oldContext.SaveChangesAsync();
        }

        public async Task MarkIdentifierAsProcessedInNewDatabase(string identifier, Chinook2Context newContext)
        {
            newContext.ProcessedMessages.Add(new ProcessedMessagesNew { UniqueIdentifier = identifier });
            await newContext.SaveChangesAsync();
        }
    }
}
