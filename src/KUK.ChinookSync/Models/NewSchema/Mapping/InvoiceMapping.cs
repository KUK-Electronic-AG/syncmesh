using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KUK.ChinookSync.Models.NewSchema.Mapping
{
    [Table("InvoiceMappings")]
    public class InvoiceMapping
    {
        [Key] // Klucz główny
        [DatabaseGenerated(DatabaseGeneratedOption.None)] // Don't generate ID on the db side, we set OldInvoiceId in the code
        public int OldInvoiceId { get; set; }

        [Required]
        public Guid NewInvoiceId { get; set; }

        [Required]
        public DateTime MappingTimestamp { get; set; }
    }
}
