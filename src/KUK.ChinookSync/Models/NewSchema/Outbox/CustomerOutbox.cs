using System.ComponentModel.DataAnnotations.Schema;
using KUK.Common.Models.Outbox;

namespace KUK.ChinookSync.Models.NewSchema.Outbox
{
    [Table("CustomerOutbox")]
    public class CustomerOutbox : BaseOutbox
    {
    }
}
