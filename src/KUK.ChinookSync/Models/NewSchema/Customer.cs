namespace KUK.ChinookSync.Models.NewSchema
{
    public class Customer
    {
        public Guid CustomerId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public Guid AddressId { get; set; }
        public string? Phone { get; set; }
        public string Email { get; set; }

        public Address Address { get; set; }
        public virtual ICollection<Invoice> Invoices { get; set; }

        public override string ToString()
        {
            return $"CustomerId={CustomerId}, FirstName={FirstName}, LastName={LastName}, AddressId={AddressId}, Email={Email}";
        }
    }
}
