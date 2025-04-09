using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KUK.ChinookSync.Models.NewSchema.Mapping
{
    [Table("AddressMappings", Schema = "public")]
    public class AddressMapping
    {
        [Key]
        [Column("address_mapping_id")]
        public Guid AddressMappingId { get; set; }

        [Column("address_composite_key_street")]
        public string? AddressCompositeKeyStreet { get; set; }

        [Column("address_composite_key_city")]
        public string? AddressCompositeKeyCity { get; set; }

        [Column("address_composite_key_state")]
        public string? AddressCompositeKeyState { get; set; }

        [Column("address_composite_key_country")]
        public string? AddressCompositeKeyCountry { get; set; }

        [Column("address_composite_key_postal_code")]
        public string? AddressCompositeKeyPostalCode { get; set; }

        [Required]
        [Column("new_address_id")]
        public Guid NewAddressId { get; set; }
        public Address NewAddress { get; set; }

        [Required]
        [Column("mapping_timestamp")]
        public DateTime MappingTimestamp { get; set; }
    }
}