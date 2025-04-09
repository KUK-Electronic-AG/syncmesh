using KUK.ChinookSync.Contexts;
using KUK.ChinookSync.Models.NewSchema.Mapping;
using KUK.Common.MigrationLogic.Interfaces;

namespace KUK.ChinookSync.DataMigrations
{
    public class MigrateInvoices : IDataMigrationBase<Chinook1DataChangesContext, Chinook2Context>
    {
        public string MigrationName => "MigrateInvoices";

        public void Up(Chinook1DataChangesContext oldContext, Chinook2Context newContext)
        {
            var existingAddressesDict = GetExistingAddressesDictionary(newContext);

            var oldInvoices = oldContext.Invoices
                      .Select(i => new
                      {
                          i.InvoiceId, // Get InvoiceId from old database, assuming that old database has InvoiceId of int type
                          i.CustomerId,
                          InvoiceDate = DateTime.SpecifyKind(i.InvoiceDate, DateTimeKind.Utc),
                          BillingAddress = new
                          {
                              i.BillingAddress,
                              i.BillingCity,
                              i.BillingState,
                              i.BillingCountry,
                              i.BillingPostalCode
                          },
                          i.Total
                      })
              .ToList();

            var invoiceMappings = new List<InvoiceMapping>();
            var invoices = new List<Models.NewSchema.Invoice>();

            foreach (var oldInvoice in oldInvoices)
            {
                var newInvoiceId = Guid.NewGuid();

                // Get customer mapping to get NewCustomerId based on OldCustomerId
                var customerMapping = newContext.CustomerMappings.FirstOrDefault(cm => cm.OldCustomerId == oldInvoice.CustomerId);
                if (customerMapping == null)
                {
                    // Customer Migration should be run before invoice migrations. Otherwise we would have exception here
                    throw new InvalidOperationException(
                        $"CustomerMapping not found for OldCustomerId: {oldInvoice.CustomerId}. " +
                        $"Ensure Customer migration is run before Invoice migration.");
                }
                Guid newCustomerId = customerMapping.NewCustomerId;

                var newInvoice = new Models.NewSchema.Invoice
                {
                    InvoiceId = newInvoiceId,
                    CustomerId = newCustomerId,
                    InvoiceDate = oldInvoice.InvoiceDate,
                    BillingAddressId = GetAddressIdFromDictionary(
                        existingAddressesDict,
                        oldInvoice.BillingAddress.BillingAddress,
                        oldInvoice.BillingAddress.BillingCity,
                        oldInvoice.BillingAddress.BillingState,
                        oldInvoice.BillingAddress.BillingCountry,
                        oldInvoice.BillingAddress.BillingPostalCode
                      ),
                    Total = oldInvoice.Total
                };
                invoices.Add(newInvoice);

                // Dodaj mapowanie faktury
                invoiceMappings.Add(new InvoiceMapping
                {
                    OldInvoiceId = oldInvoice.InvoiceId, // We assume old database has InvoiceId of int type
                    NewInvoiceId = newInvoiceId,
                    MappingTimestamp = DateTime.UtcNow
                });
            }

            newContext.Invoices.AddRange(invoices);
            newContext.InvoiceMappings.AddRange(invoiceMappings);
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
            return Guid.Empty; // Or handle appropriately if address not found
        }

    }
}