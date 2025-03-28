namespace KUK.ChinookCruds.ViewModels
{
    public class NewAddressDetailsViewModel
    {
        public Guid AddressId { get; set; }
        public string Street { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Country { get; set; }
        public string PostalCode { get; set; }

        public List<NewCustomerViewModel> Customers { get; set; }
        public List<NewInvoiceViewModel> Invoices { get; set; }
    }
}
