using System.ComponentModel.DataAnnotations;

namespace KUK.ChinookCrudsWebApp.ViewModels
{
    public class NewCustomerEditViewModel
    {
        [Required]
        public Guid CustomerId { get; set; }

        [Required]
        [StringLength(50)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(50)]
        public string LastName { get; set; }

        [Required]
        public Guid AddressId { get; set; }

        [Phone]
        public string Phone { get; set; }

        [EmailAddress]
        public string Email { get; set; }
    }
}
