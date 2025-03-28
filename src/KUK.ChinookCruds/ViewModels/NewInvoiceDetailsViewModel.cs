namespace KUK.ChinookCruds.ViewModels
{
    public class NewInvoiceDetailsViewModel
    {
        public Guid InvoiceId { get; set; }
        public Guid CustomerId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime InvoiceDate { get; set; }
        public Guid BillingAddressId { get; set; }
        public decimal Total { get; set; }
        public List<NewInvoiceLineViewModel> InvoiceLines { get; set; }
    }
}
