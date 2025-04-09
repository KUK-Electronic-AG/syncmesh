using KUK.ChinookSync.Contexts;
using KUK.ChinookSync.Services.Management.Interfaces;
using KUK.ChinookSync.TestUtilities;
using KUK.Common.Contexts;
using KUK.Common.Utilities;
using Microsoft.EntityFrameworkCore;

namespace KUK.ChinookSync.Services.Management
{
    public class DatabaseOperationsService : IDatabaseOperationsService
    {
        private readonly Chinook1DataChangesContext _oldSchemaContext;
        private readonly Chinook2Context _newSchemaContext;
        private readonly ILogger<DatabaseOperationsService> _logger;

        private const string FIRST_NAME_FOR_CUSTOMER_ADDED_USING_SUBQUERY = "John (added from subquery)";
        private const string BILLING_ADDRESS_FOR_INVOICE_ADDED_USING_NESTED_QUERY = "123 Main St (added from nested query)";

        private const string NEW_CITY = "New city";
        private const string NEW_EMAIL = "new.email@test.com";
        private const string NEW_COUNTRY = "New country";

        private const int NEW_QUANTITY_FOR_FIRST_INVOICE_LINE = 123123;
        private const int NEW_QUANTITY_FOR_SECOND_INVOICE_LINE = 321321;

        // REMARK: We may need only of of these two constructors

        public DatabaseOperationsService(
            string oldDatabaseConnectionString,
            string newDatabaseConnectionString,
            ILogger<DatabaseOperationsService> logger)
        {
            var oldOptions = new DbContextOptionsBuilder<Chinook1DataChangesContext>()
                .UseMySQL(oldDatabaseConnectionString)
                .Options;
            _oldSchemaContext = new Chinook1DataChangesContext(oldOptions);

            var newOptions = new DbContextOptionsBuilder<NewDbContext>()
                .UseNpgsql(newDatabaseConnectionString)
                .Options;
            _newSchemaContext = new Chinook2Context(newOptions);

            _logger = logger;
        }

        public DatabaseOperationsService(
            Chinook1DataChangesContext oldSchemaContext,
            Chinook2Context newSchemaContext,
            ILogger<DatabaseOperationsService> logger)
        {
            _oldSchemaContext = oldSchemaContext;
            _newSchemaContext = newSchemaContext;
            _logger = logger;
        }

        public async Task<OldCustomerOperationResult> CreateCustomerInOldDatabaseAsync()
        {
            ChinookSync.Models.OldSchema.Customer customer = TestData.OldCustomer;
            var guidValue = Guid.NewGuid().ToString();
            customer.FirstName = guidValue;

            await _oldSchemaContext.Customers.AddAsync(customer);
            await _oldSchemaContext.SaveChangesAsync();
            var result = new OldCustomerOperationResult()
            {
                GuidValueInFirstName = guidValue,
                Index = customer.CustomerId
            };
            return result;
        }

        public async Task<OldInvoiceOperationResult> CreateInvoiceInOldDatabaseAsync()
        {
            ChinookSync.Models.OldSchema.Invoice invoice = TestData.OldInvoice;
            var random = new Random();
            // Generate a random decimal value between 0 and 10,000 with two decimal places
            var randomDecialInTotal = Math.Round((decimal)(random.NextDouble() * 10000), 2);
            invoice.Total = randomDecialInTotal;

            invoice.InvoiceLines = new List<KUK.ChinookSync.Models.OldSchema.InvoiceLine>()
            {
                new ChinookSync.Models.OldSchema.InvoiceLine() { InvoiceId = invoice.InvoiceId, Quantity = 1, UnitPrice = 1, TrackId = 1 },
                new ChinookSync.Models.OldSchema.InvoiceLine() { InvoiceId = invoice.InvoiceId, Quantity = 2, UnitPrice = 2, TrackId = 2 }
            };

            await _oldSchemaContext.Invoices.AddAsync(invoice);
            await _oldSchemaContext.SaveChangesAsync();
            var result = new OldInvoiceOperationResult()
            {
                DecimalInTotal = randomDecialInTotal,
                Index = invoice.InvoiceId
            };
            return result;
        }

        public async Task<NewCustomerOperationResult> CreateCustomerInNewDatabaseAsync()
        {
            ChinookSync.Models.NewSchema.Customer customer = TestData.NewCustomer;
            customer.AddressId = _newSchemaContext.Addresses.First().AddressId;
            var guidValue = Guid.NewGuid().ToString();
            customer.FirstName = guidValue;

            await _newSchemaContext.Customers.AddAsync(customer);
            await _newSchemaContext.SaveChangesAsync();
            var result = new NewCustomerOperationResult()
            {
                GuidValueInFirstName = guidValue,
                Index = customer.CustomerId
            };
            return result;
        }

        public async Task<NewInvoiceOperationResult> CreateInvoiceInNewDatabaseAsync()
        {
            ChinookSync.Models.NewSchema.Invoice invoice = TestData.NewInvoice;
            invoice.CustomerId = _newSchemaContext.Customers.First().CustomerId;
            invoice.BillingAddressId = _newSchemaContext.Addresses.First().AddressId;
            var random = new Random();
            // Generate a random decimal value between 0 and 10,000 with two decimal places
            var randomDecialInTotal = Math.Round((decimal)(random.NextDouble() * 10000), 2);
            invoice.Total = randomDecialInTotal;

            invoice.InvoiceLines = new List<KUK.ChinookSync.Models.NewSchema.InvoiceLine>()
            {
                new ChinookSync.Models.NewSchema.InvoiceLine() { InvoiceId = invoice.InvoiceId, Quantity = 1, UnitPrice = 1, TrackId = 1 },
                new ChinookSync.Models.NewSchema.InvoiceLine() { InvoiceId = invoice.InvoiceId, Quantity = 2, UnitPrice = 2, TrackId = 2 }
            };

            await _newSchemaContext.Invoices.AddAsync(invoice);
            await _newSchemaContext.SaveChangesAsync();
            var result = new NewInvoiceOperationResult()
            {
                DecimalInTotal = randomDecialInTotal,
                Index = invoice.InvoiceId
            };
            return result;
        }

        public async Task<string> CustomerExistsInNewDatabaseAsync(OldCustomerOperationResult customerResult)
        {
            var customer = await _newSchemaContext.Customers
                .FirstOrDefaultAsync(c => c.FirstName == customerResult.GuidValueInFirstName);
            return customer != null ? string.Empty : "CustomerExistsInNewDatabaseAsync: " + customerResult.GuidValueInFirstName;
        }

        public async Task<string> InvoiceExistsInNewDatabaseAsync(OldInvoiceOperationResult invoiceResult)
        {
            var invoice = await _newSchemaContext.Invoices
                .FirstOrDefaultAsync(i => i.Total == invoiceResult.DecimalInTotal);
            return invoice != null ? string.Empty : "InvoiceExistsInNewDatabaseAsync: " + invoiceResult.DecimalInTotal.ToString();
        }

        public async Task<string> CustomerExistsInOldDatabaseAsync(NewCustomerOperationResult customerResult)
        {
            var customer = await _oldSchemaContext.Customers
                .FirstOrDefaultAsync(c => c.FirstName == customerResult.GuidValueInFirstName);
            return customer != null ? string.Empty : "CustomerExistsInOldDatabaseAsync: " + customerResult.GuidValueInFirstName;
        }

        public async Task<string> InvoiceExistsInOldDatabaseAsync(NewInvoiceOperationResult invoiceResult)
        {
            var invoice = await _oldSchemaContext.Invoices
                .FirstOrDefaultAsync(i => i.Total == invoiceResult.DecimalInTotal);
            return invoice != null ? string.Empty : "InvoiceExistsInOldDatabaseAsync: " + invoiceResult.DecimalInTotal.ToString();
        }

        public async Task<ServiceActionStatus> AddColumnToCustomerTableInOldDatabase()
        {
            try
            {
                string addColumnQuery = "ALTER TABLE Customer ADD AddedColumn NVARCHAR(255) NULL";
                await _oldSchemaContext.Database.ExecuteSqlRawAsync(addColumnQuery);
                return new ServiceActionStatus { Success = true, Message = $"Column 'AddedColumn' was added successfully to the old database." };
            }
            catch (Exception ex)
            {
                return new ServiceActionStatus { Success = false, Message = $"Unhandled exception adding new column: {ex.Message}" };
            }
        }

        /// <remarks>
        /// We need this method to check if subquery will be fully populated to preprod.
        /// </remarks>
        public async Task<ServiceActionStatus> AddNewCustomerUsingSubquery()
        {
            try
            {
                string addCustomerQuery = $@"
                    INSERT INTO db_chinook1.Customer (CustomerId, FirstName, LastName, Address, City, State, Country, PostalCode, Phone, Email)
                    VALUES (
                        (SELECT MAX(CustomerId) + 1 FROM (SELECT CustomerId FROM db_chinook1.Customer) AS temp),
                        '{FIRST_NAME_FOR_CUSTOMER_ADDED_USING_SUBQUERY}',
                        'Doe',
                        '123 Main St',
                        'Anytown',
                        NULL,
                        (SELECT BillingCountry FROM db_chinook1.Invoice WHERE InvoiceId = 1),
                        NULL,
                        NULL,
                        'john.doe@example.com'
                    );";
                await _oldSchemaContext.Database.ExecuteSqlRawAsync(addCustomerQuery);
                return new ServiceActionStatus { Success = true, Message = $"New customer successfully added using subquery to the old database." };
            }
            catch (Exception ex)
            {
                return new ServiceActionStatus
                {
                    Success = false,
                    Message = $"Unhandled exception executing {nameof(AddNewCustomerUsingSubquery)}: {ex.Message}"
                };
            }
        }

        /// <remarks>
        /// We need this method to check if nested query will be fully populated to preprod.
        /// </remarks>
        public async Task<ServiceActionStatus> AddNewInvoiceUsingNestedQuery()
        {
            try
            {
                string addInvoiceQuery = $@"
                    INSERT INTO db_chinook1.Invoice (CustomerId, InvoiceDate, BillingAddress, BillingCity, BillingState, BillingCountry, BillingPostalCode, Total, TestValue3)
                    VALUES (
                        (SELECT CustomerId FROM db_chinook1.Customer WHERE Email = 'john.doe@example.com' LIMIT 1),
                        NOW(),
                        '{BILLING_ADDRESS_FOR_INVOICE_ADDED_USING_NESTED_QUERY}',
                        'Anytown',
                        NULL,
                        'USA',
                        NULL,
                        100.00,
                        0.00
                    );";
                await _oldSchemaContext.Database.ExecuteSqlRawAsync(addInvoiceQuery);
                return new ServiceActionStatus { Success = true, Message = $"New customer successfully added using nested query to the old database." };
            }
            catch (Exception ex)
            {
                return new ServiceActionStatus
                {
                    Success = false,
                    Message = $"Unhandled exception executing {nameof(AddNewInvoiceUsingNestedQuery)}: {ex.Message}"
                };
            }
        }

        public async Task<string> CustomerAddedWithSubqueryExistsInNewDatabase()
        {
            var customer = await _newSchemaContext.Customers
                .FirstOrDefaultAsync(c => c.FirstName == FIRST_NAME_FOR_CUSTOMER_ADDED_USING_SUBQUERY);
            return customer != null ? string.Empty : "CustomerAddedWithSubqueryExistsInNewDatabase: " + FIRST_NAME_FOR_CUSTOMER_ADDED_USING_SUBQUERY;
        }

        public async Task<string> InvoiceAddedWithNestedQueryExistsInNewDatabase()
        {
            var customer = await _newSchemaContext.Addresses
                .FirstOrDefaultAsync(c => c.Street == BILLING_ADDRESS_FOR_INVOICE_ADDED_USING_NESTED_QUERY);
            return customer != null ? string.Empty : "InvoiceAddedWithNestedQueryExistsInNewDatabase: " + BILLING_ADDRESS_FOR_INVOICE_ADDED_USING_NESTED_QUERY;
        }

        public async Task<bool> UpdateCustomerCityInOldDatabaseAsync(OldCustomerOperationResult createdCustomerInOldDatabase)
        {
            try
            {
                var oldCustomer = await _oldSchemaContext.Customers.FirstOrDefaultAsync(c => c.FirstName == createdCustomerInOldDatabase.GuidValueInFirstName);
                if (oldCustomer == null)
                {
                    throw new InvalidOperationException($"Cannot find customer in old database with first name as {createdCustomerInOldDatabase.GuidValueInFirstName}");
                }
                oldCustomer.City = NEW_CITY;
                _oldSchemaContext.Customers.Update(oldCustomer);
                await _oldSchemaContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception updating customer in old database for name as {createdCustomerInOldDatabase.GuidValueInFirstName}. Exception: {ex}");
                throw;
            }
            
            return true;
        }

        public async Task<bool> UpdateCustomerEmailAndAddressInNewDatabaseAsync(OldCustomerOperationResult createdCustomerInOldDatabase)
        {
            try
            {
                var newCustomer = await _newSchemaContext.Customers.Include(c => c.Address).FirstOrDefaultAsync(c => c.FirstName == createdCustomerInOldDatabase.GuidValueInFirstName);
                if (newCustomer == null)
                {
                    throw new InvalidOperationException($"Cannot find customer in new database with first name as {createdCustomerInOldDatabase.GuidValueInFirstName}");
                }
                newCustomer.Email = NEW_EMAIL;
                newCustomer.Address.Country = NEW_COUNTRY;
                _newSchemaContext.Customers.Update(newCustomer);
                await _newSchemaContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception updating customer in new database for name as {createdCustomerInOldDatabase.GuidValueInFirstName}. Exception: {ex}");
                throw;
            }
            
            return true;
        }

        public async Task<string> CustomerCityUpdatedInOldDatabaseAsync(OldCustomerOperationResult createdCustomerInOldDatabase)
        {
            var foundCustomer = await _oldSchemaContext.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.FirstName == createdCustomerInOldDatabase.GuidValueInFirstName);
            if (foundCustomer == null)
            {
                throw new InvalidOperationException($"Cannot find updated customer in old database with first name as {createdCustomerInOldDatabase.GuidValueInFirstName}");
            }
            return foundCustomer != null && foundCustomer.City == NEW_CITY
                ? string.Empty : "CustomerCityUpdatedInOldDatabaseAsync" + createdCustomerInOldDatabase.GuidValueInFirstName;
        }

        public async Task<string> CustomerCityUpdatedInNewDatabaseAsync(OldCustomerOperationResult createdCustomerInOldDatabase)
        {
            // We need AsNoTracking to get the latest version of the city value
            var foundCustomer = await _newSchemaContext.Customers.AsNoTracking().Include(c => c.Address).SingleOrDefaultAsync(c => c.FirstName == createdCustomerInOldDatabase.GuidValueInFirstName);
            if (foundCustomer == null)
            {
                throw new InvalidOperationException($"Cannot find updated customer in new database with first name as {createdCustomerInOldDatabase.GuidValueInFirstName}");
            }
            return foundCustomer != null && foundCustomer.Address != null && foundCustomer.Address.City == NEW_CITY
                ? string.Empty : "CustomerCityUpdatedInNewDatabaseAsync" + createdCustomerInOldDatabase.GuidValueInFirstName;
        }

        public async Task<string> CustomerCityEmailAndCountryUpdatedInOldDatabaseAsync(OldCustomerOperationResult createdCustomerInOldDatabase)
        {
            var foundCustomer = await _oldSchemaContext.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.FirstName == createdCustomerInOldDatabase.GuidValueInFirstName);
            if (foundCustomer == null)
            {
                throw new InvalidOperationException($"Cannot find updated customer in old database with first name as {createdCustomerInOldDatabase.GuidValueInFirstName}");
            }
            return foundCustomer != null && foundCustomer.City == NEW_CITY && foundCustomer.Email == NEW_EMAIL && foundCustomer.Country == NEW_COUNTRY
                ? string.Empty : "CustomerCityEmailAndCountryUpdatedInOldDatabaseAsync: " + createdCustomerInOldDatabase.GuidValueInFirstName;
        }

        public async Task<string> CustomerCityEmailAndCountryUpdatedInNewDatabaseAsync(OldCustomerOperationResult createdCustomerInOldDatabase)
        {
            var foundCustomer = await _newSchemaContext.Customers.AsNoTracking().Include(c => c.Address).SingleOrDefaultAsync(c => c.FirstName == createdCustomerInOldDatabase.GuidValueInFirstName);
            if (foundCustomer == null)
            {
                throw new InvalidOperationException($"Cannot find updated customer in new database with first name as {createdCustomerInOldDatabase.GuidValueInFirstName}");
            }
            return foundCustomer != null && foundCustomer.Address != null && foundCustomer.Address.City == NEW_CITY && foundCustomer.Email == NEW_EMAIL && foundCustomer.Address.Country == NEW_COUNTRY
                ? string.Empty : "CustomerCityEmailAndCountryUpdatedInNewDatabaseAsync: " + createdCustomerInOldDatabase.GuidValueInFirstName;
        }

        public async Task<bool> DeleteCustomerInOldDatabaseAsync(OldCustomerOperationResult createdCustomerInOldDatabase)
        {
            var foundCustomer = await _oldSchemaContext.Customers.Include(c => c.Invoices).SingleOrDefaultAsync(c => c.FirstName == createdCustomerInOldDatabase.GuidValueInFirstName);
            if (foundCustomer == null)
            {
                throw new InvalidOperationException($"Cannot find updated customer in old database with first name as {createdCustomerInOldDatabase.GuidValueInFirstName}");
            }
            _oldSchemaContext.Invoices.RemoveRange(foundCustomer.Invoices);
            _oldSchemaContext.Customers.Remove(foundCustomer);
            await _oldSchemaContext.SaveChangesAsync();
            return true;
        }

        public async Task<string> FirstCustomerDeletedInOldDatabaseAsync(OldCustomerOperationResult createdCustomerInOldDatabase)
        {
            var foundCustomer = await _oldSchemaContext.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.FirstName == createdCustomerInOldDatabase.GuidValueInFirstName);
            return foundCustomer == null ? string.Empty : "FirstCustomerDeletedInOldDatabaseAsync: " + createdCustomerInOldDatabase.GuidValueInFirstName;
        }

        public async Task<string> FirstCustomerDeletedInNewDatabaseAsync(OldCustomerOperationResult createdCustomerInOldDatabase)
        {
            var foundCustomer = await _newSchemaContext.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.FirstName == createdCustomerInOldDatabase.GuidValueInFirstName);
            return foundCustomer == null ? string.Empty : "FirstCustomerDeletedInNewDatabaseAsync: " + createdCustomerInOldDatabase.GuidValueInFirstName;
        }

        public async Task<bool> DeleteCustomerInNewDatabaseAsync(NewCustomerOperationResult createdCustomerInNewDatabase)
        {
            var foundCustomer = await _newSchemaContext.Customers.Include(c => c.Invoices).SingleOrDefaultAsync(c => c.FirstName == createdCustomerInNewDatabase.GuidValueInFirstName);
            if (foundCustomer == null)
            {
                throw new InvalidOperationException($"Cannot find updated customer in old database with first name as {createdCustomerInNewDatabase.GuidValueInFirstName}");
            }
            _newSchemaContext.Invoices.RemoveRange(foundCustomer.Invoices);
            _newSchemaContext.Customers.Remove(foundCustomer);
            await _newSchemaContext.SaveChangesAsync();
            return true;
        }

        public async Task<string> SecondCustomerDeletedInOldDatabaseAsync(NewCustomerOperationResult createdCustomerInNewDatabase)
        {
            var foundCustomer = await _oldSchemaContext.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.FirstName == createdCustomerInNewDatabase.GuidValueInFirstName);
            return foundCustomer == null ? string.Empty : "SecondCustomerDeletedInOldDatabaseAsync: " + createdCustomerInNewDatabase.GuidValueInFirstName;
        }

        public async Task<string> SecondCustomerDeletedInNewDatabaseAsync(NewCustomerOperationResult createdCustomerInNewDatabase)
        {
            var foundCustomer = await _newSchemaContext.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.FirstName == createdCustomerInNewDatabase.GuidValueInFirstName);
            return foundCustomer == null ? string.Empty : "SecondCustomerDeletedInNewDatabaseAsync" + createdCustomerInNewDatabase.GuidValueInFirstName;
        }

        public async Task<bool> UpdateInvoiceLineInOldDatabaseAsync(OldInvoiceOperationResult createdInvoiceInOldDatabase)
        {
            var firstInvoiceLine = await FindFirstInvoiceLineInOldDatabase(createdInvoiceInOldDatabase, useNoTracking: false);
            firstInvoiceLine.Quantity = NEW_QUANTITY_FOR_FIRST_INVOICE_LINE;
            _oldSchemaContext.InvoiceLines.Update(firstInvoiceLine);
            await _oldSchemaContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateInvoiceLineInNewDatabaseAsync(NewInvoiceOperationResult createdInvoiceInNewDatabase)
        {
            var firstInvoiceLine = await FindSecondInvoiceLineInNewDatabase(createdInvoiceInNewDatabase, useNoTracking: false);
            firstInvoiceLine.Quantity = NEW_QUANTITY_FOR_SECOND_INVOICE_LINE;
            _newSchemaContext.InvoiceLines.Update(firstInvoiceLine);
            await _newSchemaContext.SaveChangesAsync();
            return true;
        }

        public async Task<string> FirstInvoiceLineUpdatedInOldDatabaseAsync(OldInvoiceOperationResult createdInvoiceInOldDatabase)
        {
            var firstInvoiceLine = await FindFirstInvoiceLineInOldDatabase(createdInvoiceInOldDatabase, useNoTracking: true);
            return firstInvoiceLine.Quantity == NEW_QUANTITY_FOR_FIRST_INVOICE_LINE ? string.Empty : "FirstInvoiceLineUpdatedInOldDatabaseAsync " + createdInvoiceInOldDatabase.DecimalInTotal;
        }

        public async Task<string> FirstInvoiceLineUpdatedInNewDatabaseAsync(OldInvoiceOperationResult createdInvoiceInOldDatabase)
        {
            var firstInvoiceLine = await FindFirstInvoiceLineInNewDatabase(createdInvoiceInOldDatabase);
            return firstInvoiceLine.Quantity == NEW_QUANTITY_FOR_FIRST_INVOICE_LINE ? string.Empty : "FirstInvoiceLineUpdatedInNewDatabaseAsync " + createdInvoiceInOldDatabase.DecimalInTotal + ", firstInvoiceLine.Quantity = " + firstInvoiceLine.Quantity;
        }

        public async Task<string> SecondInvoiceLineUpdatedInOldDatabaseAsync(NewInvoiceOperationResult createdInvoiceInNewDatabase)
        {
            var secondInvoiceLine = await FindSecondInvoiceLineInOldDatabase(createdInvoiceInNewDatabase);
            return secondInvoiceLine.Quantity == NEW_QUANTITY_FOR_SECOND_INVOICE_LINE ? string.Empty : "SecondInvoiceLineUpdatedInOldDatabaseAsync " + createdInvoiceInNewDatabase.DecimalInTotal;
        }

        public async Task<string> SecondInvoiceLineUpdatedInNewDatabaseAsync(NewInvoiceOperationResult createdInvoiceInNewDatabase)
        {
            var secondInvoiceLine = await FindSecondInvoiceLineInNewDatabase(createdInvoiceInNewDatabase, useNoTracking: true);
            return secondInvoiceLine.Quantity == NEW_QUANTITY_FOR_SECOND_INVOICE_LINE ? string.Empty : "SecondInvoiceLineUpdatedInNewDatabaseAsync " + createdInvoiceInNewDatabase.DecimalInTotal;
        }

        private async Task<ChinookSync.Models.OldSchema.InvoiceLine> FindFirstInvoiceLineInOldDatabase(
            OldInvoiceOperationResult createdInvoiceInOldDatabase,
            bool useNoTracking)
        {
            IQueryable<ChinookSync.Models.OldSchema.Invoice> query = _oldSchemaContext.Invoices.Include(i => i.InvoiceLines);
            if (useNoTracking)
            {
                query = query.AsNoTracking();
            }

            var foundInvoice = await query.SingleOrDefaultAsync(i => i.Total == createdInvoiceInOldDatabase.DecimalInTotal);
            if (foundInvoice == null)
            {
                throw new InvalidOperationException($"Cannot find invoice for updating its line. Total: {createdInvoiceInOldDatabase.DecimalInTotal}");
            }
            var firstInvoiceLine = foundInvoice.InvoiceLines.OrderBy(il => il.InvoiceLineId).First(i => i.UnitPrice == 1);
            return firstInvoiceLine;
        }

        private async Task<ChinookSync.Models.NewSchema.InvoiceLine> FindFirstInvoiceLineInNewDatabase(OldInvoiceOperationResult createdInvoiceInOldDatabase)
        {
            var foundInvoice = await _newSchemaContext.Invoices.AsNoTracking().Include(i => i.InvoiceLines).SingleOrDefaultAsync(i => i.Total == createdInvoiceInOldDatabase.DecimalInTotal);
            if (foundInvoice == null)
            {
                throw new InvalidOperationException($"Cannot find invoice for updating its line. Total: {createdInvoiceInOldDatabase.DecimalInTotal}");
            }
            var firstInvoiceLine = foundInvoice.InvoiceLines.OrderBy(il => il.InvoiceLineId).First(i => i.UnitPrice == 1);
            return firstInvoiceLine;
        }

        private async Task<ChinookSync.Models.OldSchema.InvoiceLine> FindSecondInvoiceLineInOldDatabase(NewInvoiceOperationResult createdInvoiceInNewDatabase)
        {
            var foundInvoice = await _oldSchemaContext.Invoices.AsNoTracking().Include(i => i.InvoiceLines).SingleOrDefaultAsync(i => i.Total == createdInvoiceInNewDatabase.DecimalInTotal);
            if (foundInvoice == null)
            {
                throw new InvalidOperationException($"Cannot find invoice for updating its line. Total: {createdInvoiceInNewDatabase.DecimalInTotal}");
            }
            var firstInvoiceLine = foundInvoice.InvoiceLines.First(i => i.UnitPrice == 1);
            return firstInvoiceLine;
        }

        private async Task<ChinookSync.Models.NewSchema.InvoiceLine> FindSecondInvoiceLineInNewDatabase(
            NewInvoiceOperationResult createdInvoiceInNewDatabase, bool useNoTracking)
        {
            IQueryable<ChinookSync.Models.NewSchema.Invoice> query = _newSchemaContext.Invoices.Include(i => i.InvoiceLines);
            if (useNoTracking)
            {
                query = query.AsNoTracking();
            }

            var foundInvoice = await query.SingleOrDefaultAsync(i => i.Total == createdInvoiceInNewDatabase.DecimalInTotal);
            if (foundInvoice == null)
            {
                throw new InvalidOperationException($"Cannot find invoice for updating its line. Total: {createdInvoiceInNewDatabase.DecimalInTotal}");
            }

            // Retrieve the invoice line - in this method we retrieve the first invoice line that meets the condition (UnitPrice == 1)
            var invoiceLine = foundInvoice.InvoiceLines.First(i => i.UnitPrice == 1);
            return invoiceLine;
        }
    }
}
