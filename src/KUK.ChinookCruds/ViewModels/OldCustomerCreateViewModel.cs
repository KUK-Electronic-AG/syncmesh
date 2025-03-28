using System.ComponentModel.DataAnnotations;

namespace KUK.ChinookCruds.ViewModels
{
    public class OldCustomerCreateViewModel
    {
        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required]
        public string Address { get; set; }

        [Required]
        public string City { get; set; }

        public string? State { get; set; }

        [Required]
        public string Country { get; set; }

        public string? PostalCode { get; set; }

        public string? Phone { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
