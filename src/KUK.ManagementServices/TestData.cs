namespace KUK.ManagementServices
{
    public static class TestData
    {
        public static Common.ModelsOldSchema.Customer OldCustomer => new Common.ModelsOldSchema.Customer
        {
            FirstName = "John (from TestData)",
            LastName = "Doe",
            Address = "123 Main St",
            City = "Anytown",
            Country = "USA",
            Email = "john.doe@example.com"
        };

        public static Common.ModelsNewSchema.Customer NewCustomer => new Common.ModelsNewSchema.Customer
        {
            FirstName = "Jane (from TestData)",
            LastName = "Smith",
            AddressId = Guid.Empty,
            Email = "jane.smith@example.com"
        };

        public static Common.ModelsOldSchema.Invoice OldInvoice => new Common.ModelsOldSchema.Invoice
        {
            CustomerId = 1,
            InvoiceDate = DateTime.UtcNow,
            BillingAddress = "123 Main St",
            BillingCity = "Anytown",
            BillingCountry = "USA",
            Total = 100.00m,
            TestValue3 = 123.456m,
        };

        public static Common.ModelsNewSchema.Invoice NewInvoice => new Common.ModelsNewSchema.Invoice
        {
            CustomerId = Guid.Empty,
            InvoiceDate = DateTime.UtcNow,
            BillingAddressId = Guid.Empty,
            Total = 200.00m
        };
    }

}
