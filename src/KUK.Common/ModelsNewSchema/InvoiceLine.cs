namespace KUK.Common.ModelsNewSchema
{
    public class InvoiceLine
    {
        public Guid InvoiceLineId { get; set; }
        public Guid InvoiceId { get; set; }
        public int TrackId { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }

        public Invoice Invoice { get; set; }

        public override string ToString()
        {
            return $"InvoiceLineId={InvoiceLineId}, InvoiceId={InvoiceId}, TrackId={TrackId}, UnitPrice={UnitPrice}, Quantity={Quantity}";
        }
    }
}
