using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KUK.Common.ModelsOldSchema
{
    [Table("ProcessedMessages")]
    public class ProcessedMessagesOld
    {
        [Key]
        public string UniqueIdentifier { get; set; }
    }
}
