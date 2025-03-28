namespace KUK.KafkaProcessor.Commands
{
    public interface IEventCommandFactory
    {
        IEventCommand GetCommand(string source, string table, string operation);
    }
}
