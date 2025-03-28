﻿using KUK.Common.Contexts;
using KUK.Common.ModelsNewSchema;
using KUK.KafkaProcessor.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace KUK.KafkaProcessor.Services
{
    public class AddressService : IAddressService
    {
        private readonly IDbContextFactory<Chinook1DataChangesContext> _oldContextFactory;
        private readonly IDbContextFactory<Chinook2Context> _newContextFactory;
        private readonly ILogger<CustomerService> _logger;
        private readonly IUniqueIdentifiersService _uniqueIdentifiersService;

        public AddressService(
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

        public async Task AddToNewDatabase(JObject payload, string syncId, string uniqueIdentifier)
        {
            _logger.LogDebug($"[DATABASE] [START] AddressService.AddToNewDatabase: SyncId: {syncId}. UniqueIdentifier: {uniqueIdentifier}");

            using (var newContext = _newContextFactory.CreateDbContext())
            {
                // Extract and standardize address fields from payload.
                string street = payload["Address"]?.ToString()?.Trim();
                string city = payload["City"]?.ToString()?.Trim();
                string state = payload["State"]?.ToString()?.Trim();
                string country = payload["Country"]?.ToString()?.Trim();
                string postalCode = payload["PostalCode"]?.ToString()?.Trim();

                // Search for existing address record in the new DB.
                var existingAddress = await newContext.AddressMappings.FirstOrDefaultAsync(a =>
                    a.AddressCompositeKeyStreet == street &&
                    a.AddressCompositeKeyCity == city &&
                    a.AddressCompositeKeyState == state &&
                    a.AddressCompositeKeyCountry == country &&
                    a.AddressCompositeKeyPostalCode == postalCode);

                if (existingAddress != null)
                {
                    _logger.LogDebug($"[DATABASE] AddToNewDatabase: Address already exists. SyncId: {syncId}");
                    return;
                }

                using (var transaction = await newContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        // Create new address record.
                        var newAddress = new KUK.Common.ModelsNewSchema.Address
                        {
                            // Assuming AddressId is generated by the DB.
                            Street = street,
                            City = city,
                            State = state,
                            Country = country,
                            PostalCode = postalCode
                        };

                        newContext.Addresses.Add(newAddress);
                        await newContext.SaveChangesAsync();

                        await _uniqueIdentifiersService.MarkIdentifierAsProcessedInNewDatabase(uniqueIdentifier, newContext);

                        await transaction.CommitAsync();
                        _logger.LogDebug($"[DATABASE] [SUCCESS] AddToNewDatabase: New address added. SyncId: {syncId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[DATABASE] AddToNewDatabase: Error while adding new address. SyncId: {syncId}. Exception: {ex.Message}");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task AddToOldDatabase(JObject payload, string syncId, string uniqueIdentifier)
        {
            _logger.LogDebug($"[DATABASE] [START] AddressService.AddToOldDatabase: SyncId: {syncId}. UniqueIdentifier: {uniqueIdentifier}");

            using (var newContext = _newContextFactory.CreateDbContext())
            using (var oldContext = _oldContextFactory.CreateDbContext())
            {
                // Get address data from payload
                string addressId = payload["AddressId"]?.ToString()?.Trim();
                string address = payload["Street"]?.ToString()?.Trim();
                string city = payload["City"]?.ToString()?.Trim();
                string state = payload["State"]?.ToString()?.Trim();
                string country = payload["Country"]?.ToString()?.Trim();
                string postalCode = payload["PostalCode"]?.ToString()?.Trim();

                // Search for existing address record in the new DB.
                var existingAddress = await newContext.AddressMappings.FirstOrDefaultAsync(a =>
                    a.NewAddressId == Guid.Parse(addressId));

                if (existingAddress != null)
                {
                    _logger.LogDebug($"[DATABASE] AddToNewDatabase: Address already exists. SyncId: {syncId}");
                    return;
                }

                using (var transaction = await oldContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        // Update all the customers records which address matches the one from payload
                        var customersToUpdate = await oldContext.Customers
                            .Where(c => c.Address == address &&
                                        c.City == city &&
                                        c.State == state &&
                                        c.Country == country &&
                                        c.PostalCode == postalCode)
                            .ToListAsync();

                        foreach (var customer in customersToUpdate)
                        {
                            customer.Address = address;
                            customer.City = city;
                            customer.State = state;
                            customer.Country = country;
                            customer.PostalCode = postalCode;
                        }

                        // Update all the invoices where denormalized address follows the payload
                        var invoicesToUpdate = await oldContext.Invoices
                            .Where(i => i.BillingAddress == address &&
                                        i.BillingCity == city &&
                                        i.BillingState == state &&
                                        i.BillingCountry == country &&
                                        i.BillingPostalCode == postalCode)
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
                        _logger.LogDebug($"[DATABASE] [SUCCESS] AddToOldDatabase: Updated {customersToUpdate.Count} customer(s) and {invoicesToUpdate.Count} invoice(s) with address. SyncId: {syncId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[DATABASE] AddToOldDatabase: Error while updating address records. SyncId: {syncId}. Exception: {ex.Message}");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task DeleteFromNewDatabase(JObject payload, string syncId, string uniqueIdentifier)
        {
            _logger.LogDebug($"[DATABASE] [START] AddressService.DeleteFromNewDatabase: SyncId: {syncId}. UniqueIdentifier: {uniqueIdentifier}");

            using (var newContext = _newContextFactory.CreateDbContext())
            {
                // In new database, if we delete the address, we search for a record in Addresses table
                string street = payload["Address"]?.ToString()?.Trim();
                string city = payload["City"]?.ToString()?.Trim();
                string state = payload["State"]?.ToString()?.Trim();
                string country = payload["Country"]?.ToString()?.Trim();
                string postalCode = payload["PostalCode"]?.ToString()?.Trim();

                using (var transaction = await newContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        var addressRecord = await newContext.Addresses.FirstOrDefaultAsync(a =>
                            a.Street == street &&
                            a.City == city &&
                            a.State == state &&
                            a.Country == country &&
                            a.PostalCode == postalCode);

                        if (addressRecord == null)
                        {
                            _logger.LogDebug($"[DATABASE] DeleteFromNewDatabase: Address not found. SyncId: {syncId}");
                            return;
                        }

                        newContext.Addresses.Remove(addressRecord);
                        await newContext.SaveChangesAsync();

                        await _uniqueIdentifiersService.MarkIdentifierAsProcessedInNewDatabase(uniqueIdentifier, newContext);

                        await transaction.CommitAsync();
                        _logger.LogDebug($"[DATABASE] [SUCCESS] DeleteFromNewDatabase: Address deleted. SyncId: {syncId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[DATABASE] DeleteFromNewDatabase: Error while deleting address. SyncId: {syncId}. Exception: {ex.Message}");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task DeleteFromOldDatabase(JObject payload, string syncId, string uniqueIdentifier)
        {
            _logger.LogDebug($"[DATABASE] [START] AddressService.DeleteFromOldDatabase: SyncId: {syncId}. UniqueIdentifier: {uniqueIdentifier}");

            using (var oldContext = _oldContextFactory.CreateDbContext())
            {
                // In old database, removing address is clearing all the address fields in customers and invoices records
                string address = payload["Street"]?.ToString()?.Trim();
                string city = payload["City"]?.ToString()?.Trim();
                string state = payload["State"]?.ToString()?.Trim();
                string country = payload["Country"]?.ToString()?.Trim();
                string postalCode = payload["PostalCode"]?.ToString()?.Trim();

                using (var transaction = await oldContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        // Find customers whose address follows the given data
                        var customersToUpdate = await oldContext.Customers
                            .Where(c => c.Address == address &&
                                        c.City == city &&
                                        c.State == state &&
                                        c.Country == country &&
                                        c.PostalCode == postalCode)
                            .ToListAsync();

                        foreach (var customer in customersToUpdate)
                        {
                            customer.Address = null;
                            customer.City = null;
                            customer.State = null;
                            customer.Country = null;
                            customer.PostalCode = null;
                        }

                        // Find invoices where denormalized address follows the given ones
                        var invoicesToUpdate = await oldContext.Invoices
                            .Where(i => i.BillingAddress == address &&
                                        i.BillingCity == city &&
                                        i.BillingState == state &&
                                        i.BillingCountry == country &&
                                        i.BillingPostalCode == postalCode)
                            .ToListAsync();

                        foreach (var invoice in invoicesToUpdate)
                        {
                            invoice.BillingAddress = null;
                            invoice.BillingCity = null;
                            invoice.BillingState = null;
                            invoice.BillingCountry = null;
                            invoice.BillingPostalCode = null;
                        }

                        await oldContext.SaveChangesAsync();

                        await _uniqueIdentifiersService.MarkIdentifierAsProcessedInOldDatabase(uniqueIdentifier, oldContext);

                        await transaction.CommitAsync();
                        _logger.LogDebug($"[DATABASE] [SUCCESS] DeleteFromOldDatabase: Cleared address fields for {customersToUpdate.Count} customer(s) and {invoicesToUpdate.Count} invoice(s). SyncId: {syncId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[DATABASE] DeleteFromOldDatabase: Error while clearing address fields. SyncId: {syncId}. Exception: {ex.Message}");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task UpdateInNewDatabase(JObject payload, string syncId, string uniqueIdentifier)
        {
            _logger.LogDebug($"[DATABASE] [START] AddressService.UpdateInNewDatabase: SyncId: {syncId}. UniqueIdentifier: {uniqueIdentifier}");

            using (var newContext = _newContextFactory.CreateDbContext())
            {
                // Get new address values from payload
                string newStreet = payload["Address"]?.ToString()?.Trim();
                string newCity = payload["City"]?.ToString()?.Trim();
                string newState = payload["State"]?.ToString()?.Trim();
                string newCountry = payload["Country"]?.ToString()?.Trim();
                string newPostalCode = payload["PostalCode"]?.ToString()?.Trim();

                if (string.IsNullOrWhiteSpace(newStreet) || string.IsNullOrWhiteSpace(newCity) ||
                    string.IsNullOrWhiteSpace(newCountry))
                {
                    _logger.LogWarning($"[DATABASE] UpdateInNewDatabase: Insufficient address data. SyncId: {syncId}");
                    return;
                }

                using (var transaction = await newContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        // Find address records that belong to the same location using ToUpper()
                        var addressesToUpdate = await newContext.Addresses
                            .Where(a => a.Street.Trim().ToUpper() == newStreet.ToUpper() &&
                                        a.City.Trim().ToUpper() == newCity.ToUpper() &&
                                        ((a.State == null && string.IsNullOrWhiteSpace(newState)) ||
                                         (a.State != null && a.State.Trim().ToUpper() == newState.ToUpper())) &&
                                        a.Country.Trim().ToUpper() == newCountry.ToUpper() &&
                                        ((a.PostalCode == null && string.IsNullOrWhiteSpace(newPostalCode)) ||
                                         (a.PostalCode != null && a.PostalCode.Trim().ToUpper() == newPostalCode.ToUpper()))
                            )
                            .ToListAsync();

                        if (addressesToUpdate.Count == 0)
                        {
                            _logger.LogWarning($"[DATABASE] UpdateInNewDatabase: No matching address record found for Street: {newStreet}. SyncId: {syncId}");
                            await transaction.CommitAsync();
                            return;
                        }

                        // Additionally: check if at least one of the records requires update
                        bool needsUpdate = addressesToUpdate.Any(addr =>
                               addr.City.Trim().ToUpper() != newCity.ToUpper() ||
                               ((addr.State == null && !string.IsNullOrWhiteSpace(newState)) ||
                                (addr.State != null && addr.State.Trim().ToUpper() != newState.ToUpper())) ||
                               addr.Country.Trim().ToUpper() != newCountry.ToUpper() ||
                               ((addr.PostalCode == null && !string.IsNullOrWhiteSpace(newPostalCode)) ||
                                (addr.PostalCode != null && addr.PostalCode.Trim().ToUpper() != newPostalCode.ToUpper()))
                        );

                        if (!needsUpdate)
                        {
                            _logger.LogDebug($"[DATABASE] UpdateInNewDatabase: Address record(s) already up-to-date for Street: {newStreet}. SyncId: {syncId}");
                            await transaction.CommitAsync();
                            return;
                        }

                        // We update found records - override all the address fields with new values
                        foreach (var addr in addressesToUpdate)
                        {
                            addr.Street = newStreet;
                            addr.City = newCity;
                            addr.State = newState;
                            addr.Country = newCountry;
                            addr.PostalCode = newPostalCode;
                        }

                        await newContext.SaveChangesAsync();

                        await _uniqueIdentifiersService.MarkIdentifierAsProcessedInNewDatabase(uniqueIdentifier, newContext);

                        await transaction.CommitAsync();
                        _logger.LogDebug($"[DATABASE] [SUCCESS] UpdateInNewDatabase: Updated {addressesToUpdate.Count} address record(s) for Street: {newStreet}. SyncId: {syncId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[DATABASE] UpdateInNewDatabase: Error while updating address records. SyncId: {syncId}. Exception: {ex.Message}");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task UpdateInOldDatabase(JObject payload, string syncId, string uniqueIdentifier)
        {
            _logger.LogDebug($"[DATABASE] [START] AddressService.UpdateInOldDatabase: SyncId: {syncId}. UniqueIdentifier: {uniqueIdentifier}");

            using (var newContext = _newContextFactory.CreateDbContext())
            using (var oldContext = _oldContextFactory.CreateDbContext())
            {
                // We get new address values from payload (we assume that new data come from Street as a source)
                string newAddressId = payload["AddressId"]?.ToString()?.Trim();
                string newAddress = payload["Street"]?.ToString()?.Trim();
                string newCity = payload["City"]?.ToString()?.Trim();
                string newState = payload["State"]?.ToString()?.Trim();
                string newCountry = payload["Country"]?.ToString()?.Trim();
                string newPostalCode = payload["PostalCode"]?.ToString()?.Trim();

                if (string.IsNullOrWhiteSpace(newAddress) || string.IsNullOrWhiteSpace(newCity) ||
                    string.IsNullOrWhiteSpace(newCountry))
                {
                    _logger.LogWarning($"[DATABASE] UpdateInOldDatabase: Insufficient address data. SyncId: {syncId}");
                    return;
                }

                var existingMapping = await newContext.AddressMappings.SingleAsync(am => am.NewAddressId == Guid.Parse(newAddressId));

                using (var transaction = await oldContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        // We update customers records for which address data no longer follow new values
                        var customersToUpdate = await oldContext.Customers
                            .Where(c => c.Address == existingMapping.AddressCompositeKeyStreet
                                        && c.City == existingMapping.AddressCompositeKeyCity
                                        && c.State == existingMapping.AddressCompositeKeyState
                                        && c.Country == existingMapping.AddressCompositeKeyCountry
                                        && c.PostalCode == existingMapping.AddressCompositeKeyPostalCode)
                            .ToListAsync();

                        if (customersToUpdate.Any())
                        {
                            foreach (var cust in customersToUpdate)
                            {
                                cust.Address = newAddress;
                                cust.City = newCity;
                                cust.State = newState;
                                cust.Country = newCountry;
                                cust.PostalCode = newPostalCode;
                            }
                        }
                        else
                        {
                            _logger.LogDebug($"[DATABASE] UpdateInOldDatabase: No customer records require update. SyncId: {syncId}");
                        }

                        // We also update invoices which denormalised address data do not follow new values
                        var invoicesToUpdate = await oldContext.Invoices
                            .Where(i => i.BillingAddress == existingMapping.AddressCompositeKeyStreet
                                        && i.BillingCity == existingMapping.AddressCompositeKeyCity
                                        && i.BillingState == existingMapping.AddressCompositeKeyState
                                        && i.BillingCountry == existingMapping.AddressCompositeKeyCountry
                                        && i.BillingPostalCode == existingMapping.AddressCompositeKeyPostalCode)
                            .ToListAsync();

                        if (invoicesToUpdate.Any())
                        {
                            foreach (var inv in invoicesToUpdate)
                            {
                                inv.BillingAddress = newAddress;
                                inv.BillingCity = newCity;
                                inv.BillingState = newState;
                                inv.BillingCountry = newCountry;
                                inv.BillingPostalCode = newPostalCode;
                            }
                        }
                        else
                        {
                            _logger.LogDebug($"[DATABASE] UpdateInOldDatabase: No invoice records require update. SyncId: {syncId}");
                        }

                        await oldContext.SaveChangesAsync();

                        await _uniqueIdentifiersService.MarkIdentifierAsProcessedInOldDatabase(uniqueIdentifier, oldContext);

                        await transaction.CommitAsync();
                        _logger.LogDebug($"[DATABASE] [SUCCESS] UpdateInOldDatabase: Updated address for {customersToUpdate.Count} customer(s) and {invoicesToUpdate.Count} invoice(s). SyncId: {syncId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[DATABASE] UpdateInOldDatabase: Error while updating address records. SyncId: {syncId}. Exception: {ex.Message}");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task<bool> MappingExists(int addressId)
        {
            // In the old database, there is no separate address table, so there is no direct address identifier
            // In this case, we can assume that the addressId is either the customer ID or the invoice ID in the old database
            // and check whether there is a mapping for the address of that customer/invoice

            using (var oldContext = _oldContextFactory.CreateDbContext())
            using (var newContext = _newContextFactory.CreateDbContext())
            {
                // We check if this is client ID
                var customer = await oldContext.Customers.FindAsync(addressId);
                if (customer != null)
                {
                    // We check if mapping exists for compositeKey address for the customer
                    var addressMapping = await newContext.AddressMappings.FirstOrDefaultAsync(am => 
                        am.AddressCompositeKeyStreet == customer.Address &&
                        am.AddressCompositeKeyCity == customer.City &&
                        am.AddressCompositeKeyState == customer.State &&
                        am.AddressCompositeKeyCountry == customer.Country &&
                        am.AddressCompositeKeyPostalCode == customer.PostalCode);
                        
                    return addressMapping != null;
                }
                
                // We check if this is ID of he invoice
                var invoice = await oldContext.Invoices.FindAsync(addressId);
                if (invoice != null)
                {
                    // We check if there exists mapping for compositeKey address for this invoice
                    var addressMapping = await newContext.AddressMappings.FirstOrDefaultAsync(am => 
                        am.AddressCompositeKeyStreet == invoice.BillingAddress &&
                        am.AddressCompositeKeyCity == invoice.BillingCity &&
                        am.AddressCompositeKeyState == invoice.BillingState &&
                        am.AddressCompositeKeyCountry == invoice.BillingCountry &&
                        am.AddressCompositeKeyPostalCode == invoice.BillingPostalCode);
                        
                    return addressMapping != null;
                }
                
                // We have not found any matching customer or invoice
                return false;
            }
        }

        public async Task<bool> MappingExists(Guid addressId)
        {
            using (var newContext = _newContextFactory.CreateDbContext())
            {
                var found = await newContext.AddressMappings.SingleOrDefaultAsync(i => i.NewAddressId == addressId);
                return found != null;
            }
        }
    }
}
