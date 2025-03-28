using System.ComponentModel.DataAnnotations;

namespace KUK.ChinookCruds.ViewModels
{
    public class OldInvoiceDetailsViewModel
    {
        [Display(Name = "Invoice ID")]
        public int InvoiceId { get; set; }

        [Display(Name = "Customer ID")]
        public int CustomerId { get; set; }

        [Display(Name = "Invoice Date")]
        public DateTime InvoiceDate { get; set; }

        [Display(Name = "Billing Address")]
        public string BillingAddress { get; set; }

        [Display(Name = "Billing City")]
        public string BillingCity { get; set; }

        [Display(Name = "Billing State")]
        public string? BillingState { get; set; }

        [Display(Name = "Billing Country")]
        public string BillingCountry { get; set; }

        [Display(Name = "Billing Postal Code")]
        public string? BillingPostalCode { get; set; }

        [Display(Name = "Total Amount")]
        public decimal Total { get; set; }

        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        public List<OldInvoiceLineViewModel> InvoiceLines { get; set; }
    }
}
