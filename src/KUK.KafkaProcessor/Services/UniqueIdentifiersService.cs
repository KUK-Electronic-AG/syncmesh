using KUK.Common.Contexts;
using KUK.Common.Models.OldSchema;
using KUK.Common.Models.Outbox;
using KUK.KafkaProcessor.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace KUK.KafkaProcessor.Services
{
    public class UniqueIdentifiersService : IUniqueIdentifiersService
    {
        private readonly IOldDataChangesContext _oldContext;
        private readonly NewDbContext _newContext;
        private readonly ILogger<UniqueIdentifiersService> _logger;

        public UniqueIdentifiersService(
            IOldDataChangesContext oldContext,
            NewDbContext newContext,
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

        public async Task MarkIdentifierAsProcessedInOldDatabase(string identifier, IOldDataChangesContext oldContext)
        {
            oldContext.ProcessedMessages.Add(new ProcessedMessagesOld { UniqueIdentifier = identifier });
            await oldContext.SaveChangesAsync();
        }

        public async Task MarkIdentifierAsProcessedInNewDatabase(string identifier, NewDbContext newContext)
        {
            newContext.ProcessedMessages.Add(new ProcessedMessagesNew { UniqueIdentifier = identifier });
            await newContext.SaveChangesAsync();
        }
    }
}
