using System.ComponentModel.DataAnnotations.Schema;

namespace KUK.Common.ModelsNewSchema.Outbox
{
    [Table("InvoiceLineOutbox")]
    public class InvoiceLineOutbox : BaseOutbox
    {
    }
}
