using System.ComponentModel.DataAnnotations;

namespace KUK.ChinookCruds.ViewModels
{
    public class OldInvoiceCreateViewModel
    {
        [Required]
        public int CustomerId { get; set; }

        [Required]
        public DateTime InvoiceDate { get; set; }

        [Required]
        public string BillingAddress { get; set; }

        [Required]
        public string BillingCity { get; set; }

        public string? BillingState { get; set; }

        [Required]
        public string BillingCountry { get; set; }

        public string? BillingPostalCode { get; set; }

        [Required]
        public decimal Total { get; set; }

        public List<OldInvoiceLineCreateViewModel> InvoiceLines { get; set; } = new List<OldInvoiceLineCreateViewModel>();
    }
}
