namespace KUK.Common.TestUtilities
{
    public class OldCustomerOperationResult
    {
        public string GuidValueInFirstName { get; set; }
        public int Index { get; set; }

        public override string ToString()
        {
            return $"GuidValueInFirstName={GuidValueInFirstName}, Index={Index}";
        }
    }
}
