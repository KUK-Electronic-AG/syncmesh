namespace KUK.ChinookCrudsWebApp.ViewModels
{
    public class NewInvoiceViewModel
    {
        public Guid InvoiceId { get; set; }
        public DateTime InvoiceDate { get; set; }
        public decimal Total { get; set; }
    }
}
