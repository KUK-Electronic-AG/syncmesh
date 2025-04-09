namespace KUK.ChinookSync.TestUtilities
{
    public static class TestData
    {
        public static ChinookSync.Models.OldSchema.Customer OldCustomer => new ChinookSync.Models.OldSchema.Customer
        {
            FirstName = "John (from TestData)",
            LastName = "Doe",
            Address = "123 Main St",
            City = "Anytown",
            Country = "USA",
            Email = "john.doe@example.com"
        };

        public static ChinookSync.Models.NewSchema.Customer NewCustomer => new ChinookSync.Models.NewSchema.Customer
        {
            FirstName = "Jane (from TestData)",
            LastName = "Smith",
            AddressId = Guid.Empty,
            Email = "jane.smith@example.com"
        };

        public static ChinookSync.Models.OldSchema.Invoice OldInvoice => new ChinookSync.Models.OldSchema.Invoice
        {
            CustomerId = 1,
            InvoiceDate = DateTime.UtcNow,
            BillingAddress = "123 Main St",
            BillingCity = "Anytown",
            BillingCountry = "USA",
            Total = 100.00m,
            TestValue3 = 123.456m,
        };

        public static ChinookSync.Models.NewSchema.Invoice NewInvoice => new ChinookSync.Models.NewSchema.Invoice
        {
            CustomerId = Guid.Empty,
            InvoiceDate = DateTime.UtcNow,
            BillingAddressId = Guid.Empty,
            Total = 200.00m
        };
    }

}
