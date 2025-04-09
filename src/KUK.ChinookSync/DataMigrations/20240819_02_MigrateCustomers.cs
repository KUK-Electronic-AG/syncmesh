using KUK.ChinookSync.Contexts;
using KUK.ChinookSync.Models.NewSchema.Mapping;
using KUK.Common.MigrationLogic.Interfaces;

namespace KUK.ChinookSync.DataMigrations
{
    public class MigrateCustomers : IDataMigrationBase<Chinook1DataChangesContext, Chinook2Context>
    {
        public string MigrationName => "MigrateCustomers";

        public void Up(Chinook1DataChangesContext oldContext, Chinook2Context newContext)
        {
            // 1. Get dictionary of existing addresses (deduplication)
            var existingAddressesDict = GetExistingAddressesDictionary(newContext);

            var oldCustomers = oldContext.Customers.ToList();
            var customerMappings = new List<CustomerMapping>();
            var customers = new List<Models.NewSchema.Customer>();

            foreach (var oldCustomer in oldCustomers)
            {
                var newCustomerId = Guid.NewGuid();

                var newCustomer = new Models.NewSchema.Customer
                {
                    CustomerId = newCustomerId,
                    FirstName = oldCustomer.FirstName,
                    LastName = oldCustomer.LastName,
                    AddressId = GetAddressIdFromDictionary(
                        existingAddressesDict,
                        oldCustomer.Address,
                        oldCustomer.City,
                        oldCustomer.State,
                        oldCustomer.Country,
                        oldCustomer.PostalCode
                    ),
                    Phone = oldCustomer.Phone,
                    Email = oldCustomer.Email
                };
                customers.Add(newCustomer);

                customerMappings.Add(new CustomerMapping
                {
                    OldCustomerId = oldCustomer.CustomerId,
                    NewCustomerId = newCustomerId,
                    MappingTimestamp = DateTime.UtcNow
                });
            }

            newContext.Customers.AddRange(customers);
            newContext.CustomerMappings.AddRange(customerMappings);
            newContext.SaveChanges();
        }

        private Dictionary<AddressCompositeKey, Guid> GetExistingAddressesDictionary(Chinook2Context newContext)
        {
            return newContext.Addresses
                .ToList()
                .ToDictionary(
                    a => new AddressCompositeKey(a.Street, a.City, a.State, a.Country, a.PostalCode),
                    a => a.AddressId
                );
        }

        private Guid GetAddressIdFromDictionary(
            Dictionary<AddressCompositeKey, Guid> addressDict,
            string street, string city, string state, string country, string postalCode)
        {
            var compositeKey = new AddressCompositeKey(street, city, state, country, postalCode);
            if (addressDict.TryGetValue(compositeKey, out Guid addressId))
            {
                return addressId;
            }
            return Guid.Empty; // Or handle appropriately if address not found (shouldn't happen if MigrateAddresses ran first)
        }
    }
}
