namespace KUK.IntegrationTests
{
    public class ConditionCheck
    {
        public Func<Task<string>> Condition { get; set; }
        public string Description { get; set; }
    }
}
