namespace KUK.Common.MigrationLogic.Interfaces
{
    public interface ITriggersCreationService<TOld, TNew>
    {
        Task CreateTriggers();
        string GenerateTrigger(
            string triggerName,
            string triggerEvent,
            string tableName,
            string outboxTable,
            string aggregateColumn,
            string aggregateType,
            string eventType,
            Dictionary<string, string> payloadMapping,
            string rowAlias);
    }
}
