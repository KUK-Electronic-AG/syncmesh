namespace KUK.Common.MigrationLogic.Interfaces
{
    public interface ICustomTriggersCreationService<TOld, TNew>
    {
        List<string> SetupTriggers();
    }
}
