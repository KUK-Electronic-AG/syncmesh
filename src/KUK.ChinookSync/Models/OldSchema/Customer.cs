namespace KUK.ChinookSync.Models.OldSchema
{
    public class Customer
    {
        public int CustomerId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string? State { get; set; }
        public string Country { get; set; }
        public string? PostalCode { get; set; }
        public string? Phone { get; set; }
        public string Email { get; set; }

        public virtual ICollection<Invoice> Invoices { get; set; }

        public override string ToString()
        {
            return $"CustomerId={CustomerId}, FirstName={FirstName}, LastName={LastName}, Address={Address}, City={City}, Country={Country}, Email={Email}";
        }
    }
}
