using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KUK.Common.ModelsNewSchema.Mapping
{
    [Table("CustomerMappings")]
    public class CustomerMapping
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)] // Don't generate ID on the db side, we set OldCustomerId in the code
        public int OldCustomerId { get; set; }

        [Required]
        public Guid NewCustomerId { get; set; }

        [Required]
        public DateTime MappingTimestamp { get; set; }
    }
}
