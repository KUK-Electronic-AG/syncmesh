using KUK.Common.Contexts;
using KUK.Common.ModelsNewSchema.Mapping;
using KUK.Common.Utilities;
using KUK.KafkaProcessor.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KUK.KafkaProcessor.Services
{
    public class InvoiceService : IInvoiceService
    {
        private readonly IDbContextFactory<Chinook1DataChangesContext> _oldContextFactory;
        private readonly IDbContextFactory<Chinook2Context> _newContextFactory;
        private readonly ILogger<InvoiceService> _logger;
        private readonly IUniqueIdentifiersService _uniqueIdentifiersService;

        public InvoiceService(
            IDbContextFactory<Chinook1DataChangesContext> oldContextFactory,
            IDbContextFactory<Chinook2Context> newContextFactory,
            ILogger<InvoiceService> logger,
            IUniqueIdentifiersService uniqueIdentifiersService)
        {
            _oldContextFactory = oldContextFactory;
            _newContextFactory = newContextFactory;
            _logger = logger;
            _uniqueIdentifiersService = uniqueIdentifiersService;
        }

        public async Task AddToNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            _logger.LogDebug($"[DATABASE] [START] InvoiceService.AddToNewDatabase: SyncId: {syncId}. UniqueIdentifier: {uniqueIdentifier}");

            using (var newContext = _newContextFactory.CreateDbContext())
            {
                var settings = new JsonSerializerSettings();
                settings.ConfigureConverters();

                var oldInvoice = JsonConvert.DeserializeObject<Common.ModelsOldSchema.Invoice>(eventBody.ToString(), settings);

                if (oldInvoice == null)
                {
                    _logger.LogWarning($"[DATABASE] AddToNewDatabase: Deserializing old invoice has not succeeded for eventBody: {eventBody}");
                    return;
                }

                // Check if mapping exists for invoice
                var existingInvoiceMapping = await newContext.InvoiceMappings
                    .FirstOrDefaultAsync(im => im.OldInvoiceId == oldInvoice.InvoiceId);

                if (existingInvoiceMapping != null)
                {
                    _logger.LogDebug($"[DATABASE] AddToNewDatabase: InvoiceMapping already exists for OldInvoiceId: {oldInvoice.InvoiceId}. SyncId: {syncId}. Skipping add.");
                    return;
                }

                // Further processing
                using (var transaction = await newContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        // 1. Mapping BillingAddress
                        Guid billingAddressId = await GetOrAddAddressId(
                            oldInvoice.BillingAddress,
                            oldInvoice.BillingCity,
                            oldInvoice.BillingState,
                            oldInvoice.BillingCountry,
                            oldInvoice.BillingPostalCode,
                            newContext);

                        // 2. Mapping customer (CustomerMapping)
                        var customerMapping = await newContext.CustomerMappings
                            .FirstOrDefaultAsync(cm => cm.OldCustomerId == oldInvoice.CustomerId);

                        Guid newCustomerId;
                        if (customerMapping == null)
                        {
                            _logger.LogError($"[DATABASE] AddToNewDatabase: No customer mapping for OldCustomerId: {oldInvoice.CustomerId}. SyncId: {syncId}");
                            throw new InvalidOperationException($"No customer mapping for OldCustomerId: {oldInvoice.CustomerId}. SyncId: {syncId}");
                        }
                        newCustomerId = customerMapping.NewCustomerId;

                        // 3. Create new invoice from GUID ID
                        var newInvoiceId = Guid.NewGuid();
                        var newInvoice = new Common.ModelsNewSchema.Invoice
                        {
                            InvoiceId = newInvoiceId,
                            CustomerId = newCustomerId,
                            InvoiceDate = oldInvoice.InvoiceDate.ToUniversalTime(),
                            BillingAddressId = billingAddressId,
                            Total = oldInvoice.Total,
                            TestValue3 = oldInvoice.TestValue3
                        };

                        newContext.Invoices.Add(newInvoice);
                        await newContext.SaveChangesAsync();

                        // 4. Adding invoice mapping
                        newContext.InvoiceMappings.Add(new InvoiceMapping
                        {
                            OldInvoiceId = oldInvoice.InvoiceId,
                            NewInvoiceId = newInvoiceId,
                            MappingTimestamp = DateTime.UtcNow
                        });
                        await newContext.SaveChangesAsync();

                        await _uniqueIdentifiersService.MarkIdentifierAsProcessedInNewDatabase(uniqueIdentifier, newContext);

                        await transaction.CommitAsync();
                        _logger.LogDebug($"[DATABASE] [SUCCESS] AddToNewDatabase: Invoice (OldInvoiceId: {oldInvoice.InvoiceId}, NewInvoiceId: {newInvoiceId}) added successfully. SyncId: {syncId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[DATABASE] AddToNewDatabase: Error while adding invoice (OldInvoiceId: {oldInvoice?.InvoiceId ?? -1}). SyncId: {syncId}. Exception: {ex.Message}");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task AddToOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            _logger.LogDebug($"[DATABASE] [START] InvoiceService.AddToOldDatabase: SyncId: {syncId}. UniqueIdentifier: {uniqueIdentifier}");

            using (var newContext = _newContextFactory.CreateDbContext())
            using (var oldContext = _oldContextFactory.CreateDbContext())
            {
                var settings = new JsonSerializerSettings();
                settings.ConfigureConverters();

                var newInvoice = JsonConvert.DeserializeObject<Common.ModelsNewSchema.Invoice>(eventBody.ToString(), settings);

                if (newInvoice == null)
                {
                    _logger.LogWarning($"[DATABASE] AddToOldDatabase: Deserializing new invoice failed for eventBody: {eventBody}");
                    return;
                }

                // Check if an invoice mapping already exists in the new database
                var existingInvoiceMapping = await newContext.InvoiceMappings
                    .FirstOrDefaultAsync(im => im.NewInvoiceId == newInvoice.InvoiceId);

                if (existingInvoiceMapping != null)
                {
                    _logger.LogDebug($"[DATABASE] AddToOldDatabase: InvoiceMapping already exists for NewInvoiceId: {newInvoice.InvoiceId}. SyncId: {syncId}. Skipping add.");
                    return;
                }

                // Retrieve CustomerMapping based on NewCustomerId from the new invoice
                var customerMapping = await newContext.CustomerMappings
                    .FirstOrDefaultAsync(cm => cm.NewCustomerId == newInvoice.CustomerId);

                if (customerMapping == null)
                {
                    _logger.LogError($"[DATABASE] AddToOldDatabase: No customer mapping for NewCustomerId: {newInvoice.CustomerId}. SyncId: {syncId}");
                    throw new InvalidOperationException($"No customer mapping for NewCustomerId: {newInvoice.CustomerId}. SyncId: {syncId}");
                }

                // Use the OldCustomerId from mapping to get the customer from the old database
                var oldCustomer = await oldContext.Customers
                    .FirstOrDefaultAsync(c => c.CustomerId == customerMapping.OldCustomerId);

                if (oldCustomer == null)
                {
                    _logger.LogError($"[DATABASE] AddToOldDatabase: Customer not found in old database for OldCustomerId: {customerMapping.OldCustomerId} (NewCustomerId: {newInvoice.CustomerId}). SyncId: {syncId}");
                    throw new InvalidOperationException($"Customer not found in old database for OldCustomerId: {customerMapping.OldCustomerId} (NewCustomerId: {newInvoice.CustomerId}). SyncId: {syncId}");
                }

                // Create an invoice object for the old database
                var oldInvoice = new Common.ModelsOldSchema.Invoice
                {
                    CustomerId = oldCustomer.CustomerId,
                    InvoiceDate = newInvoice.InvoiceDate,
                    BillingAddress = oldCustomer.Address,
                    BillingCity = oldCustomer.City,
                    BillingState = oldCustomer.State,
                    BillingCountry = oldCustomer.Country,
                    BillingPostalCode = oldCustomer.PostalCode,
                    Total = newInvoice.Total
                };

                // Step 1: Add the invoice to the old database within a transaction
                using (var oldTransaction = await oldContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        oldContext.Invoices.Add(oldInvoice);
                        await oldContext.SaveChangesAsync();
                        await oldTransaction.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"[DATABASE] AddToOldDatabase: Error while adding invoice for OldCustomerId: {oldCustomer.CustomerId}. SyncId: {syncId}");
                        await oldTransaction.RollbackAsync();
                        throw;
                    }
                }

                // Step 2: Add the mapping to the new database
                try
                {
                    var invoiceMapping = new InvoiceMapping
                    {
                        NewInvoiceId = newInvoice.InvoiceId,
                        OldInvoiceId = oldInvoice.InvoiceId
                    };
                    newContext.InvoiceMappings.Add(invoiceMapping);
                    await newContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[DATABASE] AddToOldDatabase: Error while adding invoice mapping for NewInvoiceId: {newInvoice.InvoiceId}. SyncId: {syncId}");
                    // Compensation: remove the invoice added to the old database
                    await CompensateOldInvoiceAddition(oldInvoice, syncId);
                    throw;
                }

                // Step 3: Mark the unique identifier as processed in the old database
                try
                {
                    await _uniqueIdentifiersService.MarkIdentifierAsProcessedInOldDatabase(uniqueIdentifier, oldContext);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[DATABASE] AddToOldDatabase: Error while marking identifier as processed for OldInvoiceId: {oldInvoice.InvoiceId}. SyncId: {syncId}");
                    // Compensation: remove the mapping (and possibly the invoice) due to failure
                    await CompensateInvoiceMappingAddition(newInvoice.InvoiceId, oldInvoice.InvoiceId, syncId);
                    throw;
                }

                _logger.LogDebug($"[DATABASE] [SUCCESS] AddToOldDatabase: Invoice added to old database for OldCustomerId: {oldCustomer.CustomerId}. SyncId: {syncId}");
            }
        }

        public async Task UpdateInNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            _logger.LogDebug($"[DATABASE] [START] InvoiceService.UpdateInNewDatabase: SyncId: {syncId}. UniqueIdentifier: {uniqueIdentifier}");

            using (var newContext = _newContextFactory.CreateDbContext())
            {
                var settings = new JsonSerializerSettings();
                settings.ConfigureConverters();

                var oldInvoice = JsonConvert.DeserializeObject<Common.ModelsOldSchema.Invoice>(eventBody.ToString(), settings);

                if (oldInvoice == null)
                {
                    _logger.LogWarning($"[DATABASE] UpdateInNewDatabase: Deserializing old invoice has failed for eventBody: {eventBody}");
                    return;
                }

                using (var transaction = await newContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        // 1. Find InvoiceMapping by OldInvoiceId
                        var invoiceMapping = await newContext.InvoiceMappings
                            .FirstOrDefaultAsync(im => im.OldInvoiceId == oldInvoice.InvoiceId);

                        if (invoiceMapping == null)
                        {
                            _logger.LogError($"[DATABASE] UpdateInNewDatabase: No invoice mapping for OldInvoiceId: {oldInvoice.InvoiceId}. SyncId: {syncId}");
                            throw new InvalidOperationException($"No invoice mapping for OldInvoiceId: {oldInvoice.InvoiceId}. SyncId: {syncId}");
                        }

                        // 2. Get existing invoice from new database by NewInvoiceId from mapping
                        var existingInvoice = await newContext.Invoices
                            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceMapping.NewInvoiceId);

                        if (existingInvoice == null)
                        {
                            _logger.LogWarning($"[DATABASE] UpdateInNewDatabase: Invoice has not been found in new database for NewInvoiceId: {invoiceMapping.NewInvoiceId} (OldInvoiceId: {oldInvoice.InvoiceId}). SyncId: {syncId}");
                            return;
                            //throw new InvalidOperationException($"Invoice has not been found in new database for NewInvoiceId: {invoiceMapping.NewInvoiceId} (OldInvoiceId: {oldInvoice.InvoiceId}). SyncId: {syncId}");
                        }

                        // 3. Mapping BillingAddress
                        Guid billingAddressId = await GetOrAddAddressId(
                            oldInvoice.BillingAddress,
                            oldInvoice.BillingCity,
                            oldInvoice.BillingState,
                            oldInvoice.BillingCountry,
                            oldInvoice.BillingPostalCode,
                            newContext);

                        // 4. Customer Mapping
                        var customerMapping = await newContext.CustomerMappings
                            .FirstOrDefaultAsync(cm => cm.OldCustomerId == oldInvoice.CustomerId);

                        Guid newCustomerId;
                        if (customerMapping == null)
                        {
                            _logger.LogError($"[DATABASE] UpdateInNewDatabase: No customer mapping for OldCustomerId: {oldInvoice.CustomerId}. SyncId: {syncId}");
                            throw new InvalidOperationException($"No customer mapping for OldCustomerId: {oldInvoice.CustomerId}. SyncId: {syncId}");
                        }
                        newCustomerId = customerMapping.NewCustomerId;

                        // 5. Updating existing invoice in new database
                        existingInvoice.CustomerId = newCustomerId;
                        existingInvoice.InvoiceDate = oldInvoice.InvoiceDate.ToUniversalTime(); //DateTime.SpecifyKind(oldInvoice.InvoiceDate, DateTimeKind.Utc);
                        existingInvoice.BillingAddressId = billingAddressId;
                        existingInvoice.Total = oldInvoice.Total;
                        existingInvoice.TestValue3 = oldInvoice.TestValue3;

                        await newContext.SaveChangesAsync();

                        await _uniqueIdentifiersService.MarkIdentifierAsProcessedInNewDatabase(uniqueIdentifier, newContext);

                        await transaction.CommitAsync();
                        _logger.LogDebug($"[DATABASE] [SUCCESS] UpdateInNewDatabase: Invoice (OldInvoiceId: {oldInvoice.InvoiceId}, NewInvoiceId: {existingInvoice.InvoiceId}) updated successfully. SyncId: {syncId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[DATABASE] UpdateInNewDatabase: Error while updating invoice (OldInvoiceId: {oldInvoice?.InvoiceId ?? -1}). SyncId: {syncId}. Exception: {ex.Message}");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task UpdateInOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            _logger.LogDebug($"[DATABASE] [START] InvoiceService.UpdateInOldDatabase: SyncId: {syncId}. UniqueIdentifier: {uniqueIdentifier}");

            using (var newContext = _newContextFactory.CreateDbContext())
            using (var oldContext = _oldContextFactory.CreateDbContext())
            {
                var settings = new JsonSerializerSettings();
                settings.ConfigureConverters();

                var newInvoice = JsonConvert.DeserializeObject<Common.ModelsNewSchema.Invoice>(eventBody.ToString(), settings);

                if (newInvoice == null)
                {
                    _logger.LogWarning($"[DATABASE] UpdateInOldDatabase: Deserializing new invoice has not succeeded for eventBody: {eventBody}");
                    return;
                }

                using (var transaction = await oldContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        // 1. Find InvoiceMapping by NewInvoiceId
                        var invoiceMapping = await newContext.InvoiceMappings
                            .FirstOrDefaultAsync(im => im.NewInvoiceId == newInvoice.InvoiceId);

                        if (invoiceMapping == null)
                        {
                            _logger.LogError($"[DATABASE] UpdateInOldDatabase: No invoice mapping for NewInvoiceId: {newInvoice.InvoiceId}. SyncId: {syncId}");
                            throw new InvalidOperationException($"No invoice mapping for NewInvoiceId: {newInvoice.InvoiceId}. SyncId: {syncId}");
                        }

                        // 2. Get existing invoice from old database by OldInvoiceId from mapping
                        var existingInvoice = await oldContext.Invoices
                            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceMapping.OldInvoiceId);

                        if (existingInvoice == null)
                        {
                            _logger.LogWarning($"[DATABASE] UpdateInOldDatabase: No invoice found in old database for OldInvoiceId: {invoiceMapping.OldInvoiceId} (NewInvoiceId: {newInvoice.InvoiceId}). SyncId: {syncId}");
                            return;
                            //throw new InvalidOperationException($"No invoice found in old database for OldInvoiceId: {invoiceMapping.OldInvoiceId} (NewInvoiceId: {newInvoice.InvoiceId}). SyncId: {syncId}");
                        }

                        // 3. Get CustomerMapping based on NewCustomerId (from new invoice)
                        var customerMapping = await newContext.CustomerMappings
                            .FirstOrDefaultAsync(cm => cm.NewCustomerId == newInvoice.CustomerId);

                        if (customerMapping == null)
                        {
                            _logger.LogError($"[DATABASE] UpdateInOldDatabase: No customer mapping for NewCustomerId: {newInvoice.CustomerId}. SyncId: {syncId}");
                            throw new InvalidOperationException($"No customer mapping for NewCustomerId: {newInvoice.CustomerId}. SyncId: {syncId}");
                        }

                        // 4. Use OldCustomerId from mapping to get customer from old database
                        var oldCustomer = await oldContext.Customers
                            .FirstOrDefaultAsync(c => c.CustomerId == customerMapping.OldCustomerId);

                        if (oldCustomer == null)
                        {
                            _logger.LogError($"[DATABASE] UpdateInOldDatabase: No customer has been found for OldCustomerId: {customerMapping.OldCustomerId} (NewCustomerId: {newInvoice.CustomerId}). SyncId: {syncId}");
                            throw new InvalidOperationException($"No customer has been found in old database for OldCustomerId: {customerMapping.OldCustomerId} (NewCustomerId: {newInvoice.CustomerId}). SyncId: {syncId}");
                        }

                        // 5. Update existing invoice in old database
                        existingInvoice.CustomerId = oldCustomer.CustomerId;
                        existingInvoice.InvoiceDate = newInvoice.InvoiceDate;
                        existingInvoice.BillingAddress = oldCustomer.Address;
                        existingInvoice.BillingCity = oldCustomer.City;
                        existingInvoice.BillingState = oldCustomer.State;
                        existingInvoice.BillingCountry = oldCustomer.Country;
                        existingInvoice.BillingPostalCode = oldCustomer.PostalCode;
                        existingInvoice.Total = newInvoice.Total;

                        await oldContext.SaveChangesAsync();

                        await _uniqueIdentifiersService.MarkIdentifierAsProcessedInOldDatabase(uniqueIdentifier, oldContext);

                        await transaction.CommitAsync();
                        _logger.LogDebug($"[DATABASE] [SUCCESS] UpdateInOldDatabase: Invoice (NewInvoiceId: {newInvoice.InvoiceId}, OldInvoiceId: {existingInvoice.InvoiceId}) updated successfully. SyncId: {syncId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[DATABASE] UpdateInOldDatabase: Error while updating invoice (NewInvoiceId: {newInvoice?.InvoiceId.ToString() ?? Guid.Empty.ToString()}). SyncId: {syncId}. Exception: {ex.Message}");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task DeleteFromNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            _logger.LogDebug($"[DATABASE] [START] InvoiceService.DeleteFromNewDatabase: SyncId: {syncId}. UniqueIdentifier: {uniqueIdentifier}");

            using (var newContext = _newContextFactory.CreateDbContext())
            {
                var settings = new JsonSerializerSettings();
                settings.ConfigureConverters();

                var oldInvoice = JsonConvert.DeserializeObject<Common.ModelsOldSchema.Invoice>(eventBody.ToString(), settings); // Użycie Common.ModelsOldSchema
                if (oldInvoice == null)
                {
                    _logger.LogWarning($"[DATABASE] DeleteFromNewDatabase: Deserializing old invoice has failed for eventBody: {eventBody}");
                    return;
                }

                using (var transaction = await newContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        // 1. Find InvoiceMapping by OldInvoiceId
                        var invoiceMapping = await newContext.InvoiceMappings
                            .FirstOrDefaultAsync(im => im.OldInvoiceId == oldInvoice.InvoiceId);

                        if (invoiceMapping == null)
                        {
                            _logger.LogWarning($"[DATABASE] DeleteFromNewDatabase: No mapping for invoice for OldInvoiceId: {oldInvoice.InvoiceId}. SyncId: {syncId}");
                            return;
                        }

                        // 2. Get existing invoice from new database by NewInvoiceId from mapping
                        var existingInvoice = await newContext.Invoices
                            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceMapping.NewInvoiceId);

                        if (existingInvoice != null)
                        {
                            newContext.Invoices.Remove(existingInvoice);
                            await newContext.SaveChangesAsync();
                            await _uniqueIdentifiersService.MarkIdentifierAsProcessedInNewDatabase(uniqueIdentifier, newContext);
                            await transaction.CommitAsync();
                            _logger.LogDebug($"[DATABASE] [SUCCESS] DeleteFromNewDatabase: Invoice (OldInvoiceId: {oldInvoice.InvoiceId}, NewInvoiceId: {existingInvoice.InvoiceId}) removed successfully. SyncId: {syncId}");
                        }
                        else
                        {
                            _logger.LogWarning($"[DATABASE] DeleteFromNewDatabase: Invoice has not been found in new database for NewInvoiceId: {invoiceMapping.NewInvoiceId} (OldInvoiceId: {oldInvoice.InvoiceId}). SyncId: {syncId} - it may have been already removed.");
                            await transaction.RollbackAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[DATABASE] DeleteFromNewDatabase: Error while deleting invoice (OldInvoiceId: {oldInvoice?.InvoiceId ?? -1}). SyncId: {syncId}. Exception: {ex.Message}");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task DeleteFromOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            _logger.LogDebug($"[DATABASE] [START] InvoiceService.DeleteFromOldDatabase: SyncId: {syncId}. UniqueIdentifier: {uniqueIdentifier}");

            using (var newContext = _newContextFactory.CreateDbContext())
            using (var oldContext = _oldContextFactory.CreateDbContext())
            {
                var settings = new JsonSerializerSettings();
                settings.ConfigureConverters();

                var newInvoice = JsonConvert.DeserializeObject<Common.ModelsNewSchema.Invoice>(eventBody.ToString(), settings);

                if (newInvoice == null)
                {
                    _logger.LogWarning($"[DATABASE] DeleteFromOldDatabase: Deserializing new invoice has failed for eventBody: {eventBody}");
                    return;
                }

                using (var transaction = await oldContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        // 1. Find InvoiceMapping by NewInvoiceId
                        var invoiceMapping = await newContext.InvoiceMappings
                            .FirstOrDefaultAsync(im => im.NewInvoiceId == newInvoice.InvoiceId);

                        if (invoiceMapping == null)
                        {
                            _logger.LogWarning($"[DATABASE] DeleteFromOldDatabase: No invoice mapping for NewInvoiceId: {newInvoice.InvoiceId}. SyncId: {syncId}");
                            return;
                        }

                        // 2. Get existing invoice from old database by OldInvoiceId from the mapping
                        var existingInvoice = await oldContext.Invoices
                            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceMapping.OldInvoiceId);

                        if (existingInvoice == null)
                        {
                            _logger.LogWarning($"[DATABASE] DeleteFromOldDatabase: Invoice has not been found in old database for OldInvoiceId: {invoiceMapping.OldInvoiceId} (NewInvoiceId: {newInvoice.InvoiceId}). SyncId: {syncId} - it may have been already removed.");
                            await transaction.RollbackAsync();
                        }
                        else
                        {
                            oldContext.Invoices.Remove(existingInvoice);
                            await oldContext.SaveChangesAsync();
                            await _uniqueIdentifiersService.MarkIdentifierAsProcessedInOldDatabase(uniqueIdentifier, oldContext);
                            await transaction.CommitAsync();
                            _logger.LogDebug($"[DATABASE] [SUCCESS] DeleteFromOldDatabase: Invoice (NewInvoiceId: {newInvoice.InvoiceId}, OldInvoiceId: {existingInvoice.InvoiceId}) has been successfully removed from old database. SyncId: {syncId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[DATABASE] DeleteFromOldDatabase: Error while removing invoice from old database (NewInvoiceId: {newInvoice?.InvoiceId.ToString() ?? Guid.Empty.ToString()}). SyncId: {syncId}. Exception: {ex.Message}");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task<bool> MappingExists(int invoiceId)
        {
            using (var newContext = _newContextFactory.CreateDbContext())
            {
                var found = await newContext.InvoiceMappings.SingleOrDefaultAsync(i => i.OldInvoiceId == invoiceId);
                return found != null;
            }
        }

        public async Task<bool> MappingExists(Guid invoiceId)
        {
            using (var newContext = _newContextFactory.CreateDbContext())
            {
                var found = await newContext.InvoiceMappings.SingleOrDefaultAsync(i => i.NewInvoiceId == invoiceId);
                return found != null;
            }
        }

        private async Task<Guid> GetOrAddAddressId(string street, string city, string state, string country, string postalCode, Chinook2Context newContext)
        {
            var compositeKey = new AddressCompositeKey(street, city, state, country, postalCode);

            if (string.IsNullOrEmpty(compositeKey.Street) && string.IsNullOrEmpty(compositeKey.City)
                && string.IsNullOrEmpty(compositeKey.State) && string.IsNullOrEmpty(compositeKey.Country)
                && string.IsNullOrEmpty(compositeKey.PostalCode))
            {
                throw new InvalidOperationException("Everything is null in composite key in InvoiceService");
            }

            // 1. Check AddressMappings
            var addressMapping = await newContext.AddressMappings
                .FirstOrDefaultAsync(am =>
                    am.AddressCompositeKeyStreet == compositeKey.Street &&
                    am.AddressCompositeKeyCity == compositeKey.City &&
                    am.AddressCompositeKeyState == compositeKey.State &&
                    am.AddressCompositeKeyCountry == compositeKey.Country &&
                    am.AddressCompositeKeyPostalCode == compositeKey.PostalCode);

            if (addressMapping != null)
            {
                return addressMapping.NewAddressId;
            }

            // 2. Check if address already existst in Addresses table (deduplication)
            var existingAddress = await newContext.Addresses
                .FirstOrDefaultAsync(a =>
                    a.Street == compositeKey.Street &&
                    a.City == compositeKey.City &&
                    a.State == compositeKey.State &&
                    a.Country == compositeKey.Country &&
                    a.PostalCode == compositeKey.PostalCode);

            if (existingAddress != null)
            {
                // Address exists - use existing AddressId and add mapping
                newContext.AddressMappings.Add(new AddressMapping
                {
                    AddressCompositeKeyStreet = compositeKey.Street,
                    AddressCompositeKeyCity = compositeKey.City,
                    AddressCompositeKeyState = compositeKey.State,
                    AddressCompositeKeyCountry = compositeKey.Country,
                    AddressCompositeKeyPostalCode = compositeKey.PostalCode,
                    NewAddressId = existingAddress.AddressId,
                    MappingTimestamp = DateTime.UtcNow
                });
                await newContext.SaveChangesAsync();
                return existingAddress.AddressId;
            }

            // 3. Address does not exist - add new address and create mapping
            var newAddressId = Guid.NewGuid();
            var newAddress = new Common.ModelsNewSchema.Address
            {
                AddressId = newAddressId,
                Street = compositeKey.Street,
                City = compositeKey.City,
                State = compositeKey.State,
                Country = compositeKey.Country,
                PostalCode = compositeKey.PostalCode
            };
            newContext.Addresses.Add(newAddress);
            await newContext.SaveChangesAsync();

            // 4. Add mapping
            newContext.AddressMappings.Add(new AddressMapping
            {
                AddressCompositeKeyStreet = compositeKey.Street,
                AddressCompositeKeyCity = compositeKey.City,
                AddressCompositeKeyState = compositeKey.State,
                AddressCompositeKeyCountry = compositeKey.Country,
                AddressCompositeKeyPostalCode = compositeKey.PostalCode,
                NewAddressId = newAddressId,
                MappingTimestamp = DateTime.UtcNow
            });
            await newContext.SaveChangesAsync();

            return newAddressId;
        }

        private async Task CompensateOldInvoiceAddition(Common.ModelsOldSchema.Invoice oldInvoice, string syncId)
        {
            // Implement compensation logic, e.g., remove the invoice from the old database
            throw new NotImplementedException();
        }

        private async Task CompensateInvoiceMappingAddition(Guid newInvoiceId, int oldInvoiceId, string syncId)
        {
            // Implement compensation logic, e.g., remove the mapping from the new database and/or delete the invoice from the old database
            throw new NotImplementedException();
        }
    }
}