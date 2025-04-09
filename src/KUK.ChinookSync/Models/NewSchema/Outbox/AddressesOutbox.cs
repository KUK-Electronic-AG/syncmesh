using System.ComponentModel.DataAnnotations.Schema;
using KUK.Common.Models.Outbox;

namespace KUK.ChinookSync.Models.NewSchema.Outbox
{
    [Table("AddressOutbox")]
    public class AddressOutbox : BaseOutbox
    {
    }
}
