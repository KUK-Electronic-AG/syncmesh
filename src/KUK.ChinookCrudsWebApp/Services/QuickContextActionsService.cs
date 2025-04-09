using KUK.ChinookCrudsWebApp.Services.Interfaces;
using KUK.ChinookSync.Contexts;
using KUK.Common.Utilities;
using Microsoft.EntityFrameworkCore;

namespace KUK.ChinookCrudsWebApp.Services
{
    public class QuickContextActionsService : IQuickContextActionsService
    {
        private readonly Chinook1DataChangesContext _oldSchemaContext;
        private readonly Chinook2Context _newSchemaContext;

        public QuickContextActionsService(
            Chinook1DataChangesContext oldSchemaContext,
            Chinook2Context newSchemaContext)
        {
            _oldSchemaContext = oldSchemaContext;
            _newSchemaContext = newSchemaContext;
        }

        public async Task<ServiceActionStatus> QuickCreateOldCustomer()
        {
            var newCustomer = new ChinookSync.Models.OldSchema.Customer
            {
                FirstName = "John (From old database - quick create)",
                LastName = "Doe",
                Address = "123 Main St",
                City = "Anytown",
                State = "Anystate",
                Country = "Anycountry",
                PostalCode = "12345",
                Phone = "123-456-7890",
                Email = "john.doe@example.com"
            };

            return await CreateEntityAsync(_oldSchemaContext, newCustomer, "Customer created successfully.");
        }

        public async Task<ServiceActionStatus> QuickCreateOldInvoice()
        {
            var newInvoice = new ChinookSync.Models.OldSchema.Invoice
            {
                CustomerId = 1,
                InvoiceDate = DateTime.UtcNow,
                BillingAddress = "123 Main St",
                BillingCity = "Anytown",
                BillingState = "Anystate",
                BillingCountry = "Anycountry",
                BillingPostalCode = "12345",
                Total = 100.00m,
                TestValue3 = 123.456m,
            };

            return await CreateEntityAsync(_oldSchemaContext, newInvoice, "Old Invoice created successfully.");
        }

        public async Task<ServiceActionStatus> QuickCreateNewCustomer()
        {
            ChinookSync.Models.NewSchema.Address address = _newSchemaContext.Addresses.FirstOrDefault();
            if (address == null)
            {
                throw new InvalidOperationException("There are no addresses so we cannot quickly create new customer");
            }

            var newCustomer = new ChinookSync.Models.NewSchema.Customer
            {
                FirstName = "Jane (from new database - quick create)",
                LastName = "Doe",
                AddressId = address.AddressId,
                Phone = "987-654-3210",
                Email = "jane.doe@example.com"
            };

            return await CreateEntityAsync(_newSchemaContext, newCustomer, "New Customer created successfully.");
        }

        public async Task<ServiceActionStatus> QuickCreateNewInvoice()
        {
            ChinookSync.Models.NewSchema.Address address = _newSchemaContext.Addresses.FirstOrDefault();
            if (address == null)
            {
                throw new InvalidOperationException("There are no addresses so we cannot quickly create new invoice");
            }

            ChinookSync.Models.NewSchema.Customer customer = _newSchemaContext.Customers.FirstOrDefault();
            if (customer == null)
            {
                throw new InvalidOperationException("There are no customers so we cannot quickly create new invoice");
            }

            var newInvoice = new ChinookSync.Models.NewSchema.Invoice
            {
                CustomerId = customer.CustomerId,
                InvoiceDate = DateTime.UtcNow,
                BillingAddressId = address.AddressId,
                Total = 200.00m
            };

            return await CreateEntityAsync(_newSchemaContext, newInvoice, "New Invoice created successfully.");
        }

        public async Task<ServiceActionStatus> QuickCreateNewAddress()
        {
            var newAddress = new ChinookSync.Models.NewSchema.Address
            {
                Street = "456 Main St",
                City = "Newtown",
                State = "Newstate",
                Country = "Newcountry",
                PostalCode = "67890"
            };

            return await CreateEntityAsync(_newSchemaContext, newAddress, "New Address created successfully.");
        }

        private async Task<ServiceActionStatus> CreateEntityAsync<T>(DbContext context, T entity, string successMessage) where T : class
        {
            try
            {
                context.Set<T>().Add(entity);
                await context.SaveChangesAsync();
                return new ServiceActionStatus { Success = true, Message = successMessage };
            }
            catch (DbUpdateException dbEx)
            {
                return new ServiceActionStatus { Success = false, Message = $"Database update error: {dbEx.Message}" };
            }
            catch (Exception ex)
            {
                return new ServiceActionStatus { Success = false, Message = $"Unhandled exception: {ex.Message}" };
            }
        }
    }
}
