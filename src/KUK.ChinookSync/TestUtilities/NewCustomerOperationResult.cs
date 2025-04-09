namespace KUK.ChinookSync.TestUtilities
{
    public class NewCustomerOperationResult
    {
        public string GuidValueInFirstName { get; set; }
        public Guid Index { get; set; }

        public override string ToString()
        {
            return $"GuidValueInFirstName={GuidValueInFirstName}, Index={Index}";
        }
    }
}
