using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KUK.Common.Models.OldSchema
{
    [Table("ProcessedMessages")]
    public class ProcessedMessagesOld
    {
        [Key]
        public string UniqueIdentifier { get; set; }
    }
}
