using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace KUK.Common.ModelsNewSchema.Outbox
{
    [Index(nameof(UniqueIdentifier))]
    public abstract class BaseOutbox
    {
        [Column("event_id")]
        public Guid EventId { get; set; }

        [Column("aggregate_id")]
        public Guid AggregateId { get; set; }

        [Column("aggregate_type")]
        public string AggregateType { get; set; }

        [Column("event_type")]
        public string EventType { get; set; }

        [Column("payload")]
        public string Payload { get; set; }

        [Column("unique_identifier")]
        public string UniqueIdentifier { get; set; }

        [Column("created_at")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime CreatedAt { get; set; }
    }
}