using System.ComponentModel.DataAnnotations;

namespace KUK.ChinookCrudsWebApp.ViewModels
{
    public class NewInvoiceCreateViewModel
    {
        [Required]
        public Guid CustomerId { get; set; }

        [Required]
        public DateTime InvoiceDate { get; set; }

        [Required]
        public Guid BillingAddressId { get; set; }

        [Required]
        public decimal Total { get; set; }

        public List<NewInvoiceLineCreateViewModel> InvoiceLines { get; set; }
    }
}
