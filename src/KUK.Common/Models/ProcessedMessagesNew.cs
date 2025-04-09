using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KUK.Common.Models.Outbox
{
    [Table("ProcessedMessages")]
    public class ProcessedMessagesNew
    {
        [Key]
        public string UniqueIdentifier { get; set; }
    }
}
