using System.ComponentModel.DataAnnotations.Schema;

namespace KUK.Common.ModelsNewSchema.Outbox
{
    [Table("CustomerOutbox")]
    public class CustomerOutbox : BaseOutbox
    {
    }
}
