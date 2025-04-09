using System.ComponentModel.DataAnnotations.Schema;
using KUK.Common.Models.Outbox;

namespace KUK.ChinookSync.Models.NewSchema.Outbox
{
    [Table("InvoiceOutbox")]
    public class InvoiceOutbox : BaseOutbox
    {
    }
}
