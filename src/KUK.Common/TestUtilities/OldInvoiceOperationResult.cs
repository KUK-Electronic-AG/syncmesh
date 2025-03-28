namespace KUK.Common.TestUtilities
{
    public class OldInvoiceOperationResult
    {
        public decimal DecimalInTotal { get; set; }
        public int Index { get; set; }

        public override string ToString()
        {
            return $"DecimalInTotal={DecimalInTotal}, Index={Index}";
        }
    }
}
