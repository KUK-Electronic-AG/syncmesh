using KUK.Common.Utilities;

namespace KUK.Common.ModelsOldSchema
{
    public class Invoice
    {
        public int InvoiceId { get; set; }
        public int CustomerId { get; set; }
        [UnixEpochDateTimeConverter(3)]
        public DateTime InvoiceDate { get; set; }
        public string BillingAddress { get; set; }
        public string BillingCity { get; set; }
        public string? BillingState { get; set; }
        public string BillingCountry { get; set; }
        public string? BillingPostalCode { get; set; }
        [DecimalConverter(2)]
        public decimal Total { get; set; }

        [DecimalConverter(3)]
        public decimal TestValue3 { get; set; }

        public virtual Customer Customer { get; set; }

        public virtual ICollection<InvoiceLine> InvoiceLines { get; set; }

        public override string ToString()
        {
            return $"InvoiceId={InvoiceId}, CustomerId={CustomerId}, InvoideDate={InvoiceDate}, BillingAddress={BillingAddress}, BillingCity={BillingCity}, BillingState={BillingState}, BillingCountry={BillingCountry}, BillingPostalCode={BillingPostalCode}, Total={Total}, Customer={Customer}";
        }
    }
}
