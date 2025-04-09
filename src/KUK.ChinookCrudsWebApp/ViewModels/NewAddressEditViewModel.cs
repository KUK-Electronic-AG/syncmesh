using System.ComponentModel.DataAnnotations;

namespace KUK.ChinookCrudsWebApp.ViewModels
{
    public class NewAddressEditViewModel
    {
        [Required]
        public Guid AddressId { get; set; }

        [Required]
        [StringLength(100)]
        public string Street { get; set; }

        [Required]
        [StringLength(50)]
        public string City { get; set; }

        [StringLength(50)]
        public string State { get; set; }

        [Required]
        [StringLength(50)]
        public string Country { get; set; }

        [StringLength(20)]
        public string PostalCode { get; set; }
    }
}
