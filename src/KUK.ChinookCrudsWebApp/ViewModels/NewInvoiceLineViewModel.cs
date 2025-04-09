namespace KUK.ChinookCrudsWebApp.ViewModels
{
    public class NewInvoiceLineViewModel
    {
        public Guid InvoiceLineId { get; set; }
        public Guid InvoiceId { get; set; }
        public int TrackId { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
    }
}
