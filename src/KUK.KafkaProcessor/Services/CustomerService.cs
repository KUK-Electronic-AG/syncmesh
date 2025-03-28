using KUK.Common.Contexts;
using KUK.Common.ModelsNewSchema.Mapping;
using KUK.KafkaProcessor.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace KUK.KafkaProcessor.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly IDbContextFactory<Chinook1DataChangesContext> _oldContextFactory;
        private readonly IDbContextFactory<Chinook2Context> _newContextFactory;
        private readonly ILogger<CustomerService> _logger;
        private readonly IUniqueIdentifiersService _uniqueIdentifiersService;

        public CustomerService(
            IDbContextFactory<Chinook1DataChangesContext> oldContextFactory,
            IDbContextFactory<Chinook2Context> newContextFactory,
            ILogger<CustomerService> logger,
            IUniqueIdentifiersService uniqueIdentifiersService)
        {
            _oldContextFactory = oldContextFactory;
            _newContextFactory = newContextFactory;
            _logger = logger;
            _uniqueIdentifiersService = uniqueIdentifiersService;
        }

        public async Task AddToNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            _logger.LogDebug($"[DATABASE] [START] CustomerService.AddToNewDatabase: SyncId: {syncId}. UniqueIdentifier: {uniqueIdentifier}");

            using (var newContext = _newContextFactory.CreateDbContext())
            {
                using (var transaction = await newContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        _logger.LogDebug($"[DATABASE] AddToNewDatabase: Processing event with syncId: {syncId}");

                        var oldCustomerId = eventBody["CustomerId"]?.ToObject<int>();
                        var firstName = eventBody["FirstName"]?.ToString();
                        var lastName = eventBody["LastName"]?.ToString();
                        var address = eventBody["Address"]?.ToString();
                        var city = eventBody["City"]?.ToString();
                        var state = eventBody["State"]?.ToString();
                        var country = eventBody["Country"]?.ToString();
                        var postalCode = eventBody["PostalCode"]?.ToString();
                        var phone = eventBody["Phone"]?.ToString();
                        var email = eventBody["Email"]?.ToString();

                        // 1. Check if CustomerMapping already exists for OldCustomerId
                        var existingCustomerMapping = await newContext.CustomerMappings
                            .FirstOrDefaultAsync(cm => cm.OldCustomerId == oldCustomerId);

                        if (existingCustomerMapping != null)
                        {
                            _logger.LogDebug($"[DATABASE] AddToNewDatabase: CustomerMapping already exists for OldCustomerId: {oldCustomerId}. SyncId: {syncId}. Skipping add.");
                            return;
                        }

                        // 2. Find or create the address in the NEW database
                        var addressEntity = await newContext.Addresses.FirstOrDefaultAsync(a =>
                            a.Street == address &&
                            a.City == city &&
                            a.State == state &&
                            a.Country == country &&
                            a.PostalCode == postalCode);

                        if (addressEntity == null)
                        {
                            addressEntity = new KUK.Common.ModelsNewSchema.Address
                            {
                                Street = address,
                                City = city,
                                State = state,
                                Country = country,
                                PostalCode = postalCode
                            };
                            newContext.Addresses.Add(addressEntity);
                            await newContext.SaveChangesAsync();

                            var addressMappingEntity = new AddressMapping
                            {
                                AddressCompositeKeyStreet = address,
                                AddressCompositeKeyCity = city,
                                AddressCompositeKeyState = state,
                                AddressCompositeKeyCountry = country,
                                AddressCompositeKeyPostalCode = postalCode,
                                NewAddressId = addressEntity.AddressId,
                                MappingTimestamp = DateTime.UtcNow
                            };
                            newContext.AddressMappings.Add(addressMappingEntity);
                            await newContext.SaveChangesAsync();
                        }

                        // 3. Create the new customer in the NEW database with GUID ID
                        var newCustomerId = Guid.NewGuid();
                        var customer = new KUK.Common.ModelsNewSchema.Customer
                        {
                            CustomerId = newCustomerId,
                            FirstName = firstName,
                            LastName = lastName,
                            AddressId = addressEntity.AddressId,
                            Phone = phone,
                            Email = email
                        };

                        newContext.Customers.Add(customer);
                        await newContext.SaveChangesAsync();

                        if (!oldCustomerId.HasValue || newCustomerId == null || newCustomerId == Guid.Empty)
                        {
                            _logger.LogWarning($"[DATABASE] Empty old or new customer id in customer service - CustomerService/AddToNewDatabase");
                        }

                        // 4. Create CustomerMapping
                        newContext.CustomerMappings.Add(new CustomerMapping
                        {
                            OldCustomerId = oldCustomerId.Value,
                            NewCustomerId = newCustomerId,
                            MappingTimestamp = DateTime.UtcNow
                        });
                        await newContext.SaveChangesAsync();

                        await _uniqueIdentifiersService.MarkIdentifierAsProcessedInNewDatabase(uniqueIdentifier, newContext);

                        await transaction.CommitAsync();
                        _logger.LogDebug($"[DATABASE] [SUCCESS] AddToNewDatabase: Customer added with syncId: {syncId}, OldCustomerId: {oldCustomerId}, NewCustomerId: {newCustomerId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"[DATABASE] AddToNewDatabase: Error processing event with syncId: {syncId}");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }


        public async Task AddToOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            _logger.LogDebug($"[DATABASE] [START] CustomerService.AddToOldDatabase: SyncId: {syncId}. UniqueIdentifier: {uniqueIdentifier}");

            using (var newContext = _newContextFactory.CreateDbContext())
            using (var oldContext = _oldContextFactory.CreateDbContext())
            {
                using (var transaction = await oldContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        _logger.LogDebug($"[DATABASE] AddToOldDatabase: Processing event with syncId: {syncId}");

                        var newCustomerId = eventBody["CustomerId"].ToObject<Guid>();
                        var firstName = eventBody["FirstName"]?.ToString();
                        var lastName = eventBody["LastName"]?.ToString();
                        var newAddressId = eventBody["AddressId"]?.ToString();
                        var phone = eventBody["Phone"]?.ToString();
                        var email = eventBody["Email"]?.ToString();

                        // 1. Get CustomerMapping in order to receive OldCustomerId
                        var customerMapping = await newContext.CustomerMappings
                            .FirstOrDefaultAsync(cm => cm.NewCustomerId == newCustomerId);

                        if (customerMapping != null)
                        {
                            _logger.LogDebug($"[DATABASE] AddToOldDatabase: CustomerMapping already exists for NewCustomerId: {newCustomerId}. SyncId: {syncId}");
                            return;
                        }

                        // 2. Get address from new database
                        var address = newContext.Addresses.SingleOrDefault(a => a.AddressId == Guid.Parse(newAddressId));

                        // 3. Create old Customer object. Address is set directly in Customer
                        var customer = new Common.ModelsOldSchema.Customer
                        {
                            FirstName = firstName,
                            LastName = lastName,
                            Phone = phone,
                            Email = email,
                            City = address.City,
                            State = address.State,
                            Country = address.Country,
                            PostalCode = address.PostalCode,
                            Address = address.Street
                        };

                        oldContext.Customers.Add(customer);
                        await oldContext.SaveChangesAsync();

                        // 4. Create CustomerMapping
                        newContext.CustomerMappings.Add(new CustomerMapping
                        {
                            OldCustomerId = customer.CustomerId,
                            NewCustomerId = newCustomerId,
                            MappingTimestamp = DateTime.UtcNow
                        });
                        await newContext.SaveChangesAsync();

                        await _uniqueIdentifiersService.MarkIdentifierAsProcessedInOldDatabase(uniqueIdentifier, oldContext);

                        await transaction.CommitAsync();
                        _logger.LogDebug($"[DATABASE] [SUCCESS] AddToOldDatabase: Customer added to old DB with syncId: {syncId}, NewCustomerId: {newCustomerId}, OldCustomerId: {customer.CustomerId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"[DATABASE] AddToOldDatabase: Error processing event with syncId: {syncId}");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task UpdateInNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            _logger.LogDebug($"[DATABASE] [START] CustomerService.UpdateInNewDatabase: SyncId: {syncId}. UniqueIdentifier: {uniqueIdentifier}");

            using (var newContext = _newContextFactory.CreateDbContext())
            {
                using (var transaction = await newContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        _logger.LogDebug("[DATABASE] UpdateInNewDatabase: Processing event with syncId: {SyncId}", syncId);

                        // Extract properties from eventBody
                        var oldCustomerId = eventBody["CustomerId"]?.ToObject<int>();
                        var firstName = eventBody["FirstName"]?.ToString();
                        var lastName = eventBody["LastName"]?.ToString();
                        var address = eventBody["Address"]?.ToString();
                        var city = eventBody["City"]?.ToString();
                        var state = eventBody["State"]?.ToString();
                        var country = eventBody["Country"]?.ToString();
                        var postalCode = eventBody["PostalCode"]?.ToString();
                        var phone = eventBody["Phone"]?.ToString();
                        var email = eventBody["Email"]?.ToString();

                        // 1. Retrieve CustomerMapping to obtain NewCustomerId based on OldCustomerId
                        var customerMapping = await newContext.CustomerMappings
                            .FirstOrDefaultAsync(cm => cm.OldCustomerId == oldCustomerId);

                        if (customerMapping == null)
                        {
                            _logger.LogError("[DATABASE] UpdateInNewDatabase: CustomerMapping not found for OldCustomerId: {OldCustomerId}. SyncId: {SyncId}", oldCustomerId, syncId);
                            throw new InvalidOperationException($"CustomerMapping not found for OldCustomerId: {oldCustomerId}. SyncId: {syncId}");
                        }
                        Guid newCustomerId = customerMapping.NewCustomerId;

                        // 2. Retrieve existing customer from new database using NewCustomerId
                        var customer = await newContext.Customers.FindAsync(newCustomerId);
                        if (customer != null)
                        {
                            // Update customer properties
                            customer.FirstName = firstName;
                            customer.LastName = lastName;
                            customer.Phone = phone;
                            customer.Email = email;

                            KUK.Common.ModelsNewSchema.Address addressEntity = null;

                            // If customer already has an assigned address, try to update it
                            if (customer.AddressId != null)
                            {
                                addressEntity = await newContext.Addresses.FindAsync(customer.AddressId);
                                if (addressEntity != null)
                                {
                                    // Update existing address
                                    addressEntity.Street = address;
                                    addressEntity.City = city;
                                    addressEntity.State = state;
                                    addressEntity.Country = country;
                                    addressEntity.PostalCode = postalCode;
                                    _logger.LogDebug("[DATABASE] UpdateInNewDatabase: Updated existing address.");
                                }

                                // Update existing address mapping
                                var addressMappingEntity = await newContext.AddressMappings
                                    .SingleOrDefaultAsync(am => am.NewAddressId == addressEntity.AddressId);
                                if (addressMappingEntity != null)
                                {
                                    // Check if another mapping already exists with the same composite key
                                    var duplicateMapping = await newContext.AddressMappings
                                        .FirstOrDefaultAsync(am => am.NewAddressId != addressEntity.AddressId &&
                                                                   am.AddressCompositeKeyStreet == address &&
                                                                   am.AddressCompositeKeyCity == city &&
                                                                   am.AddressCompositeKeyState == state &&
                                                                   am.AddressCompositeKeyCountry == country &&
                                                                   am.AddressCompositeKeyPostalCode == postalCode);

                                    if (duplicateMapping != null)
                                    {
                                        // REMARK: This is edge case that happened only once. It may be some race condition.
                                        _logger.LogWarning("[DATABASE] UpdateInNewDatabase: Duplicate address mapping exists for a different address. " +
                                            "Updating customer's AddressId to duplicate mapping's NewAddressId: {DuplicateNewAddressId}.", duplicateMapping.NewAddressId);
                                        // Set customer's AddressId to the one from the duplicate mapping
                                        customer.AddressId = duplicateMapping.NewAddressId;
                                    }
                                    else
                                    {
                                        addressMappingEntity.AddressCompositeKeyStreet = address;
                                        addressMappingEntity.AddressCompositeKeyCity = city;
                                        addressMappingEntity.AddressCompositeKeyState = state;
                                        addressMappingEntity.AddressCompositeKeyCountry = country;
                                        addressMappingEntity.AddressCompositeKeyPostalCode = postalCode;
                                        _logger.LogDebug("[DATABASE] UpdateInNewDatabase: Updated existing address mapping.");
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning("[DATABASE] UpdateInNewDatabase: Address mapping not found for AddressId: {AddressId}.", addressEntity.AddressId);
                                }
                            }

                            // If customer had no assigned address or address not found, try to find address based on data
                            if (addressEntity == null)
                            {
                                addressEntity = await newContext.Addresses.FirstOrDefaultAsync(a =>
                                    a.Street == address &&
                                    a.City == city &&
                                    a.State == state &&
                                    a.Country == country &&
                                    a.PostalCode == postalCode);

                                if (addressEntity != null)
                                {
                                    _logger.LogDebug("[DATABASE] UpdateInNewDatabase: Found matching address record.");
                                }
                            }

                            // If still no address found, create a new one
                            if (addressEntity == null)
                            {
                                addressEntity = new KUK.Common.ModelsNewSchema.Address
                                {
                                    Street = address,
                                    City = city,
                                    State = state,
                                    Country = country,
                                    PostalCode = postalCode
                                };
                                newContext.Addresses.Add(addressEntity);
                                await newContext.SaveChangesAsync();
                                _logger.LogDebug("[DATABASE] UpdateInNewDatabase: Created new address record.");
                            }

                            // Set customer's AddressId to the found or created address
                            customer.AddressId = addressEntity.AddressId;

                            _logger.LogDebug("[DATABASE] UpdateInNewDatabase: About to update customer record for NewCustomerId: {NewCustomerId}", newCustomerId);
                            await newContext.SaveChangesAsync();
                            await _uniqueIdentifiersService.MarkIdentifierAsProcessedInNewDatabase(uniqueIdentifier, newContext);
                            await transaction.CommitAsync();

                            _logger.LogDebug("[DATABASE] [SUCCESS] UpdateInNewDatabase: Customer updated successfully with syncId: {SyncId}, OldCustomerId: {OldCustomerId}, NewCustomerId: {NewCustomerId}",
                                syncId, oldCustomerId, newCustomerId);
                        }
                        else
                        {
                            _logger.LogWarning("[DATABASE] UpdateInNewDatabase: Customer with NewCustomerId: {NewCustomerId} not found (based on OldCustomerId: {OldCustomerId} mapping). SyncId: {SyncId}.", newCustomerId, oldCustomerId, syncId);
                            await transaction.RollbackAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[DATABASE] UpdateInNewDatabase: Error processing event with syncId: {SyncId}", syncId);
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task UpdateInOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            _logger.LogDebug($"[DATABASE] [START] CustomerService.UpdateInOldDatabase: SyncId: {syncId}. UniqueIdentifier: {uniqueIdentifier}");

            using (var newContext = _newContextFactory.CreateDbContext())
            using (var oldContext = _oldContextFactory.CreateDbContext())
            {
                var newCustomerId = eventBody["CustomerId"]?.ToObject<Guid>()
                ?? throw new Exception("CustomerId is missing in eventBody.");
                var firstName = eventBody["FirstName"]?.ToString();
                var lastName = eventBody["LastName"]?.ToString();
                var addressId = eventBody["AddressId"]?.ToString();
                var phone = eventBody["Phone"]?.ToString();
                var email = eventBody["Email"]?.ToString();

                // Get address
                // AsNoTracking is needed to skip cache, we can use it because we don't modify the 'addressToUpdate', only use its data
                var addressToUpdate = newContext.Addresses.AsNoTracking().SingleOrDefault(a => a.AddressId == Guid.Parse(addressId));
                if (addressToUpdate == null)
                {
                    throw new Exception($"Address with id {addressId} not found.");
                }
                var address = addressToUpdate.Street;
                var city = addressToUpdate.City;
                var state = addressToUpdate.State;
                var country = addressToUpdate.Country;
                var postalCode = addressToUpdate.PostalCode;

                using (var transaction = await oldContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        _logger.LogDebug($"[DATABASE] UpdateInOldDatabase: Processing event with syncId: {syncId}");

                        // 1. Get CustomerMapping to obtain OldCustomerId
                        var customerMapping = await newContext.CustomerMappings
                            .FirstOrDefaultAsync(cm => cm.NewCustomerId == newCustomerId);
                        if (customerMapping == null)
                        {
                            _logger.LogError($"[DATABASE] UpdateInOldDatabase: CustomerMapping not found for NewCustomerId: {newCustomerId}. SyncId: {syncId}");
                            throw new InvalidOperationException($"CustomerMapping not found for NewCustomerId: {newCustomerId}. SyncId: {syncId}");
                        }
                        int oldCustomerId = customerMapping.OldCustomerId;

                        // 2. Update existing customer in old database using OldCustomerId
                        var customer = await oldContext.Customers.FindAsync(oldCustomerId);
                        if (customer != null)
                        {
                            customer.FirstName = firstName;
                            customer.LastName = lastName;
                            customer.Phone = phone;
                            customer.Email = email;
                            customer.Address = address;
                            customer.City = city;
                            customer.State = state;
                            customer.Country = country;
                            customer.PostalCode = postalCode;

                            await oldContext.SaveChangesAsync();
                            _logger.LogDebug($"[DATABASE] UpdateInOldDatabase: Customer updated in old DB with syncId: {syncId}, NewCustomerId: {newCustomerId}, OldCustomerId: {oldCustomerId}");
                        }
                        else
                        {
                            _logger.LogWarning($"[DATABASE] UpdateInOldDatabase: Customer with OldCustomerId: {oldCustomerId} not found. SyncId: {syncId}");
                        }

                        // 3. Update all invoices for that customer in old database
                        // REMARK: This is business decision if we want to update these
                        var invoicesToUpdate = await oldContext.Invoices
                            .Where(i => i.CustomerId == oldCustomerId)
                            .ToListAsync();
                        foreach (var invoice in invoicesToUpdate)
                        {
                            invoice.BillingAddress = address;
                            invoice.BillingCity = city;
                            invoice.BillingState = state;
                            invoice.BillingCountry = country;
                            invoice.BillingPostalCode = postalCode;
                        }
                        await oldContext.SaveChangesAsync();

                        await _uniqueIdentifiersService.MarkIdentifierAsProcessedInOldDatabase(uniqueIdentifier, oldContext);

                        await transaction.CommitAsync();
                        _logger.LogDebug($"[DATABASE] [SUCCESS] UpdateInOldDatabase: Updated {invoicesToUpdate.Count} invoice(s) for CustomerId: {oldCustomerId}. SyncId: {syncId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"[DATABASE] UpdateInOldDatabase: Error processing event with syncId: {syncId}");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }

                // 4. Update address mapping in new database, if applicable
                try
                {
                    var newCustomer = await newContext.Customers.FindAsync(newCustomerId);
                    if (newCustomer != null && newCustomer.AddressId != null)
                    {
                        var addressMapping = await newContext.AddressMappings
                            .SingleOrDefaultAsync(am => am.NewAddressId == newCustomer.AddressId);
                        if (addressMapping != null)
                        {
                            addressMapping.AddressCompositeKeyStreet = address;
                            addressMapping.AddressCompositeKeyCity = city;
                            addressMapping.AddressCompositeKeyState = state;
                            addressMapping.AddressCompositeKeyCountry = country;
                            addressMapping.AddressCompositeKeyPostalCode = postalCode;
                            await newContext.SaveChangesAsync();
                            _logger.LogDebug($"[DATABASE] [SUCCESS] UpdateInOldDatabase: Updated address mapping for NewAddressId: {newCustomer.AddressId}. SyncId: {syncId}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[DATABASE] UpdateInOldDatabase: Error updating new database address mapping. SyncId: {syncId}");
                    // REMARK: In case of error updating new database, do compensation operation in the old database to undo previous changes
                    await CompensateUpdateInOldDatabaseChanges(newCustomerId, syncId, uniqueIdentifier);
                    throw;
                }
            }
        }

        public async Task DeleteFromNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            _logger.LogDebug($"[DATABASE] [START] CustomerService.DeleteFromNewDatabase: SyncId: {syncId}. UniqueIdentifier: {uniqueIdentifier}");

            using (var newContext = _newContextFactory.CreateDbContext())
            {
                using (var transaction = await newContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        _logger.LogDebug($"[DATABASE] DeleteFromNewDatabase: Processing event with syncId: {syncId}");

                        var oldCustomerId = eventBody["CustomerId"]?.ToObject<int>();

                        // 1. Get CustomerMapping in order to receive NewCustomerId
                        var customerMapping = await newContext.CustomerMappings
                            .FirstOrDefaultAsync(cm => cm.OldCustomerId == oldCustomerId);

                        if (customerMapping == null)
                        {
                            _logger.LogWarning(
                                $"[DATABASE] DeleteFromNewDatabase: CustomerMapping not found for OldCustomerId: {oldCustomerId}. " +
                                $"SyncId: {syncId}. Customer deletion skipped (in new DB, but mapping missing).");
                            return;
                        }
                        Guid newCustomerId = customerMapping.NewCustomerId;

                        // 2. Get existing customer from new database using NewCustomerId
                        var customer = await newContext.Customers.FindAsync(newCustomerId);

                        if (customer != null)
                        {
                            newContext.Customers.Remove(customer);
                            await newContext.SaveChangesAsync();
                            await _uniqueIdentifiersService.MarkIdentifierAsProcessedInNewDatabase(uniqueIdentifier, newContext);
                            await transaction.CommitAsync();
                            _logger.LogDebug($"[DATABASE] [SUCCESS] DeleteFromNewDatabase: Customer deleted with syncId: {syncId}, OldCustomerId: {oldCustomerId}, NewCustomerId: {newCustomerId}");
                        }
                        else
                        {
                            _logger.LogWarning($"[DATABASE] DeleteFromNewDatabase: Customer with NewCustomerId: {newCustomerId} not found (based on OldCustomerId: {oldCustomerId} mapping). " +
                                $"SyncId: {syncId} - possible already deleted.");
                            await transaction.RollbackAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"[DATABASE] DeleteFromNewDatabase: Error processing event with syncId: {syncId}");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task DeleteFromOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            _logger.LogDebug($"[DATABASE] [START] CustomerService.DeleteFromOldDatabase: SyncId: {syncId}. UniqueIdentifier: {uniqueIdentifier}");

            using (var newContext = _newContextFactory.CreateDbContext())
            using (var oldContext = _oldContextFactory.CreateDbContext())
            {
                using (var transaction = await oldContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        _logger.LogDebug($"[DATABASE] DeleteFromOldDatabase: Processing event with syncId: {syncId}");

                        var newCustomerId = eventBody["CustomerId"]?.ToObject<Guid>();

                        // 1. Get CustomerMapping in order to receive OldCustomerId
                        var customerMapping = await newContext.CustomerMappings
                            .FirstOrDefaultAsync(cm => cm.NewCustomerId == newCustomerId);

                        if (customerMapping == null)
                        {
                            _logger.LogWarning($"[DATABASE] DeleteFromOldDatabase: CustomerMapping not found for NewCustomerId: {newCustomerId}. SyncId: {syncId}. Customer deletion skipped (in old DB, but mapping missing).");
                            return;
                        }
                        int oldCustomerId = customerMapping.OldCustomerId;

                        // 2. Get existing customer from old database using OldCustomerId
                        var customer = await oldContext.Customers.FindAsync(oldCustomerId);

                        if (customer != null)
                        {
                            oldContext.Customers.Remove(customer);
                            await oldContext.SaveChangesAsync();
                            await _uniqueIdentifiersService.MarkIdentifierAsProcessedInOldDatabase(uniqueIdentifier, oldContext);
                            await transaction.CommitAsync();
                            _logger.LogDebug($"[DATABASE] [SUCCESS] DeleteFromOldDatabase: Customer deleted from old database with syncId: {syncId}, NewCustomerId: {newCustomerId}, OldCustomerId: {oldCustomerId}");
                        }
                        else
                        {
                            _logger.LogWarning($"[DATABASE] DeleteFromOldDatabase: Customer with OldCustomerId: {oldCustomerId} not found (based on NewCustomerId: {newCustomerId} mapping). SyncId: {syncId} - possible already deleted.");
                            await transaction.RollbackAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"[DATABASE] DeleteFromOldDatabase: Error processing event with syncId: {syncId}");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task<bool> MappingExists(int customerId)
        {
            using (var newContext = _newContextFactory.CreateDbContext())
            {
                var found = await newContext.CustomerMappings.SingleOrDefaultAsync(i => i.OldCustomerId == customerId);
                return found != null;
            }
        }

        public async Task<bool> MappingExists(Guid customerId)
        {
            using (var newContext = _newContextFactory.CreateDbContext())
            {
                var found = await newContext.CustomerMappings.SingleOrDefaultAsync(i => i.NewCustomerId == customerId);
                return found != null;
            }
        }

        private async Task CompensateUpdateInOldDatabaseChanges(Guid newCustomerId, string syncId, string uniqueIdentifier)
        {
            // Saga pattern (compensation = undoing first transaction in other database)
            throw new NotImplementedException("Compensation logic is not implemented");
        }
    }
}