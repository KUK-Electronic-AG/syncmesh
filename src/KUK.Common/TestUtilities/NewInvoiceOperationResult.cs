namespace KUK.Common.TestUtilities
{
    public class NewInvoiceOperationResult
    {
        public decimal DecimalInTotal { get; set; }
        public Guid Index { get; set; }

        public override string ToString()
        {
            return $"DecimalInTotal={DecimalInTotal}, Index={Index}";
        }
    }
}
