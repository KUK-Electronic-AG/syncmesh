using System.ComponentModel.DataAnnotations;

namespace KUK.ChinookCrudsWebApp.ViewModels
{
    public class OldInvoiceLineCreateViewModel
    {
        [Required]
        public int TrackId { get; set; }

        [Required]
        public decimal UnitPrice { get; set; }

        [Required]
        public int Quantity { get; set; }
    }
}
