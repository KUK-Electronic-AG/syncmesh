using System.ComponentModel.DataAnnotations.Schema;

namespace KUK.Common.ModelsNewSchema.Outbox
{
    [Table("InvoiceOutbox")]
    public class InvoiceOutbox : BaseOutbox
    {
    }
}
