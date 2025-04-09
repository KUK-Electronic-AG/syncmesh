using KUK.ChinookSync.Contexts;
using KUK.ChinookSync.Models.NewSchema.Mapping;
using KUK.Common.MigrationLogic.Interfaces;

namespace KUK.ChinookSync.DataMigrations
{
    public class MigrateAddresses : IDataMigrationBase<Chinook1DataChangesContext, Chinook2Context>
    {
        public string MigrationName => "MigrateAddresses";

        public void Up(Chinook1DataChangesContext oldContext, Chinook2Context newContext)
        {
            // As a prerequisite we create unique index on Addresses table (database migraton should handle it)

            // Get all existing addresses to the dictionary (key: composite key, value: AddressId)
            var existingAddressesDict = GetExistingAddressesDictionary(newContext);

            var customerAddressesCompositeKeys = oldContext.Customers
                      .Select(c => new AddressCompositeKey(
                        c.Address,
                        c.City,
                        c.State,
                        c.Country,
                        c.PostalCode)
                      )
                      .Distinct()
                      .ToList();

            var invoiceAddressesCompositeKeys = oldContext.Invoices
                      .Select(i => new AddressCompositeKey(
                        i.BillingAddress,
                        i.BillingCity,
                        i.BillingState,
                        i.BillingCountry,
                        i.BillingPostalCode)
                      )
                      .Distinct()
                      .ToList();

            var allAddressCompositeKeys = customerAddressesCompositeKeys
              .Union(invoiceAddressesCompositeKeys)
              .Distinct()
              .ToList();

            var addressMappings = new List<AddressMapping>();

            foreach (var compositeKey in allAddressCompositeKeys)
            {
                if (!existingAddressesDict.TryGetValue(compositeKey, out Guid newAddressId))
                {
                    // Address doesn't exist in new database - add new address
                    var newAddressRecord = new Models.NewSchema.Address
                    {
                        AddressId = Guid.NewGuid(),
                        Street = compositeKey.Street,
                        City = compositeKey.City,
                        State = compositeKey.State,
                        Country = compositeKey.Country,
                        PostalCode = compositeKey.PostalCode
                    };
                    newContext.Addresses.Add(newAddressRecord);
                    newContext.SaveChanges();

                    newAddressId = newAddressRecord.AddressId;
                    existingAddressesDict.Add(compositeKey, newAddressId);
                }
                else
                {
                    newAddressId = existingAddressesDict[compositeKey];
                }

                var addressMapping = new AddressMapping
                {
                    AddressCompositeKeyStreet = compositeKey.Street,
                    AddressCompositeKeyCity = compositeKey.City,
                    AddressCompositeKeyState = compositeKey.State,
                    AddressCompositeKeyCountry = compositeKey.Country,
                    AddressCompositeKeyPostalCode = compositeKey.PostalCode,
                    NewAddressId = newAddressId,
                    MappingTimestamp = DateTime.UtcNow
                };

                addressMappings.Add(addressMapping);
            }

            newContext.AddressMappings.AddRange(addressMappings);
            newContext.SaveChanges();
        }

        private Dictionary<AddressCompositeKey, Guid> GetExistingAddressesDictionary(Chinook2Context newContext)
        {
            return newContext.Addresses
                      .ToList()
                      .ToDictionary(
                          a => new AddressCompositeKey(a.Street, a.City, a.State, a.Country, a.PostalCode), // Composite key
                          a => a.AddressId
              );
        }
    }
}