using System.ComponentModel.DataAnnotations.Schema;
using KUK.Common.Utilities;

namespace KUK.ChinookSync.Models.NewSchema
{
    public class Invoice
    {
        public Guid InvoiceId { get; set; }
        public Guid CustomerId { get; set; }
        [UnixEpochDateTimeConverter(6)]
        public DateTime InvoiceDate { get; set; }
        public Guid BillingAddressId { get; set; }
        [DecimalConverter(2)]
        public decimal Total { get; set; }
        [DecimalConverter(3)] // required for JSON converter to read this value from Debezium
        [Column(TypeName = "decimal(18,3)")] // required for MySQL 8.0 initialization so that we have more precision in the db
        public decimal TestValue3 { get; set; }

        public Customer Customer { get; set; }
        public Address BillingAddress { get; set; }

        public virtual ICollection<InvoiceLine> InvoiceLines { get; set; }

        public override string ToString()
        {
            return $"InvoiceId={InvoiceId}, CustomerId={CustomerId}, InvoiceDate={InvoiceDate}, BillingAddressId={BillingAddressId}, Total={Total}";
        }
    }
}
