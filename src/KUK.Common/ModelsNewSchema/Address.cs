namespace KUK.Common.ModelsNewSchema
{
    public class Address
    {
        public Guid AddressId { get; set; }
        public string Street { get; set; }
        public string City { get; set; }
        public string? State { get; set; }
        public string Country { get; set; }
        public string? PostalCode { get; set; }

        public virtual ICollection<Customer> Customers { get; set; }
        public virtual ICollection<Invoice> Invoices { get; set; }

        public override string ToString()
        {
            return $"AddressId={AddressId}, Street={Street}, City={City}, State={State}, Country={Country}, PostalCode={PostalCode}";
        }
    }
}
