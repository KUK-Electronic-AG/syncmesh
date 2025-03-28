using KUK.Common.Contexts;
using KUK.Common.ModelsNewSchema.Mapping;
using KUK.Common.Utilities;
using KUK.KafkaProcessor.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KUK.KafkaProcessor.Services
{
    public class InvoiceLineService : IInvoiceLineService
    {
        private readonly IDbContextFactory<Chinook1DataChangesContext> _oldContextFactory;
        private readonly IDbContextFactory<Chinook2Context> _newContextFactory;
        private readonly ILogger<InvoiceLineService> _logger;
        private readonly IUniqueIdentifiersService _uniqueIdentifiersService;

        public InvoiceLineService(
            IDbContextFactory<Chinook1DataChangesContext> oldContextFactory,
            IDbContextFactory<Chinook2Context> newContextFactory,
            ILogger<InvoiceLineService> logger,
            IUniqueIdentifiersService uniqueIdentifiersService)
        {
            _oldContextFactory = oldContextFactory;
            _newContextFactory = newContextFactory;
            _logger = logger;
            _uniqueIdentifiersService = uniqueIdentifiersService;
        }

        public async Task AddToNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            _logger.LogDebug($"[DATABASE] [START] InvoiceLineService.AddToNewDatabase: SyncId: {syncId}. UniqueIdentifier: {uniqueIdentifier}");

            using (var newContext = _newContextFactory.CreateDbContext())
            {
                var settings = new JsonSerializerSettings();
                settings.ConfigureConverters();

                var oldInvoiceLine = JsonConvert.DeserializeObject<Common.ModelsOldSchema.InvoiceLine>(eventBody.ToString(), settings);

                if (oldInvoiceLine == null)
                {
                    _logger.LogError($"[DATABASE] AddToNewDatabase: Failure deserializing old invoice line for eventBody: {eventBody}. SyncId: {syncId}");
                    return;
                }

                // Check if mapping exists for invoice line
                var existingInvoiceLineMapping = await newContext.InvoiceLineMappings
                    .FirstOrDefaultAsync(ilm => ilm.OldInvoiceLineId == oldInvoiceLine.InvoiceLineId);

                if (existingInvoiceLineMapping != null)
                {
                    _logger.LogDebug($"[DATABASE] AddToNewDatabase: InvoiceMapping already exists for OldInvoiceId: {oldInvoiceLine.InvoiceId}. SyncId: {syncId}. Skipping add.");
                    return;
                }

                using (var transaction = await newContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        // 1. Get InvoiceMapping in order to receive NewInvoiceId
                        var invoiceMapping = await newContext.InvoiceMappings
                            .FirstOrDefaultAsync(im => im.OldInvoiceId == oldInvoiceLine.InvoiceId);

                        if (invoiceMapping == null)
                        {
                            _logger.LogError($"[DATABASE] AddToNewDatabase: InvoiceMapping has not been found for OldInvoiceId: {oldInvoiceLine.InvoiceId}. SyncId: {syncId}");
                            throw new InvalidOperationException($"InvoiceMapping has not been found for OldInvoiceId: {oldInvoiceLine.InvoiceId}. SyncId: {syncId}");
                        }
                        Guid newInvoiceId = invoiceMapping.NewInvoiceId;

                        // 2. Create new InvoiceLine with GUID ID, InvoiceId is of type Guid
                        var newInvoiceLineId = Guid.NewGuid();
                        var newInvoiceLine = new Common.ModelsNewSchema.InvoiceLine
                        {
                            InvoiceLineId = newInvoiceLineId,
                            InvoiceId = newInvoiceId,
                            TrackId = oldInvoiceLine.TrackId,
                            UnitPrice = oldInvoiceLine.UnitPrice,
                            Quantity = oldInvoiceLine.Quantity
                        };

                        newContext.InvoiceLines.Add(newInvoiceLine);
                        await newContext.SaveChangesAsync();

                        // 3. Add InvoiceLineMapping
                        newContext.InvoiceLineMappings.Add(new InvoiceLineMapping
                        {
                            OldInvoiceLineId = oldInvoiceLine.InvoiceLineId,
                            NewInvoiceLineId = newInvoiceLineId,
                            MappingTimestamp = DateTime.UtcNow
                        });
                        await newContext.SaveChangesAsync();

                        await _uniqueIdentifiersService.MarkIdentifierAsProcessedInNewDatabase(uniqueIdentifier, newContext);

                        await transaction.CommitAsync();
                        _logger.LogDebug($"[DATABASE] [SUCCESS] AddToNewDatabase: Invoice line (OldInvoiceLineId: {oldInvoiceLine.InvoiceLineId}, NewInvoiceLineId: {newInvoiceLineId}) added successfully. SyncId: {syncId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[DATABASE] AddToNewDatabase: Error while adding invoice line (OldInvoiceLineId: {oldInvoiceLine?.InvoiceLineId ?? -1}). SyncId: {syncId}. Exception: {ex.Message}");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task AddToOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            _logger.LogDebug($"[DATABASE] [START] InvoiceLineService.AddToOldDatabase: SyncId: {syncId}. UniqueIdentifier: {uniqueIdentifier}");

            using (var newContext = _newContextFactory.CreateDbContext())
            using (var oldContext = _oldContextFactory.CreateDbContext())
            {
                var settings = new JsonSerializerSettings();
                settings.ConfigureConverters();

                var newInvoiceLine = JsonConvert.DeserializeObject<Common.ModelsNewSchema.InvoiceLine>(eventBody.ToString(), settings);

                if (newInvoiceLine == null)
                {
                    _logger.LogWarning($"[DATABASE] AddToOldDatabase: Deserializing new invoice line has failed for eventBody: {eventBody}. SyncId: {syncId}");
                    return;
                }

                // Check if mapping exists for invoice line
                var existingInvoiceLineMapping = await newContext.InvoiceLineMappings
                    .FirstOrDefaultAsync(ilm => ilm.NewInvoiceLineId == newInvoiceLine.InvoiceLineId);

                if (existingInvoiceLineMapping != null)
                {
                    _logger.LogDebug($"[DATABASE] AddToNewDatabase: InvoiceMapping already exists for OldInvoiceId: {newInvoiceLine.InvoiceId}. SyncId: {syncId}. Skipping add.");
                    return;
                }

                // 1. Get InvoiceMapping in order to get OldInvoiceId based on NewInvoiceId from newInvoiceLine
                var invoiceMapping = await newContext.InvoiceMappings
                    .FirstOrDefaultAsync(im => im.NewInvoiceId == newInvoiceLine.InvoiceId);

                if (invoiceMapping == null)
                {
                    _logger.LogError($"[DATABASE] AddToOldDatabase: InvoiceMapping not found for NewInvoiceId (from new invoice line, NewInvoiceLineId: InvoiceLineId={newInvoiceLine.InvoiceLineId}, InvoiceId={newInvoiceLine.InvoiceId}). SyncId: {syncId}");
                    throw new InvalidOperationException($"InvoiceMapping not found for NewInvoiceId (from new invoice line, NewInvoiceLineId: InvoiceLineId={newInvoiceLine.InvoiceLineId}, InvoiceId={newInvoiceLine.InvoiceId}). SyncId: {syncId}");
                }

                using (var transaction = await oldContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        // 2. Create old InvoiceLine using OldInvoiceId from InvoiceMapping
                        var oldInvoiceLine = new Common.ModelsOldSchema.InvoiceLine
                        {
                            InvoiceId = invoiceMapping.OldInvoiceId,
                            TrackId = newInvoiceLine.TrackId,
                            UnitPrice = newInvoiceLine.UnitPrice,
                            Quantity = newInvoiceLine.Quantity,
                            // InvoiceLineId - Old database has Identity on InvoiceLineId so we don't set it here while adding
                        };

                        oldContext.InvoiceLines.Add(oldInvoiceLine);
                        await oldContext.SaveChangesAsync();

                        // 3. Add InvoiceLineMapping
                        newContext.InvoiceLineMappings.Add(new InvoiceLineMapping
                        {
                            OldInvoiceLineId = oldInvoiceLine.InvoiceLineId,
                            NewInvoiceLineId = newInvoiceLine.InvoiceLineId,
                            MappingTimestamp = DateTime.UtcNow
                        });
                        await newContext.SaveChangesAsync();

                        await _uniqueIdentifiersService.MarkIdentifierAsProcessedInOldDatabase(uniqueIdentifier, oldContext);

                        await transaction.CommitAsync();
                        _logger.LogDebug($"[DATABASE] [SUCCESS] AddToOldDatabase: Invoice line (OldInvoiceLineId: {oldInvoiceLine.InvoiceLineId}, NewInvoiceLineId: {newInvoiceLine.InvoiceLineId}) added successfully. SyncId: {syncId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[DATABASE] AddToOldDatabase: Error while adding invoice line. SyncId: {syncId}. Exception: {ex.Message}");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task UpdateInNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            _logger.LogDebug($"[DATABASE] [START] InvoiceLineService.UpdateInNewDatabase: SyncId: {syncId}. UniqueIdentifier: {uniqueIdentifier}");

            using (var newContext = _newContextFactory.CreateDbContext())
            {
                var settings = new JsonSerializerSettings();
                settings.ConfigureConverters();

                var oldInvoiceLine = JsonConvert.DeserializeObject<Common.ModelsOldSchema.InvoiceLine>(eventBody.ToString(), settings);

                if (oldInvoiceLine == null)
                {
                    _logger.LogWarning($"[DATABASE] UpdateInNewDatabase: Deserializing old invoice line has not been successful for eventBody: {eventBody}. SyncId: {syncId}");
                    return;
                }

                using (var transaction = await newContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        // 1. Get InvoiceLineMapping in order to get NewInvoiceLineId
                        var invoiceLineMapping = await newContext.InvoiceLineMappings
                            .FirstOrDefaultAsync(ilm => ilm.OldInvoiceLineId == oldInvoiceLine.InvoiceLineId);

                        if (invoiceLineMapping == null)
                        {
                            _logger.LogError($"[DATABASE] UpdateInNewDatabase: InvoiceLineMapping has not been found for OldInvoiceLineId: {oldInvoiceLine.InvoiceLineId}. SyncId: {syncId}");
                            throw new InvalidOperationException($"InvoiceLineMapping has not been found for OldInvoiceLineId: {oldInvoiceLine.InvoiceLineId}. SyncId: {syncId}");
                        }

                        // 2. Get existing InvoiceLine from new database using NewInvoiceLineId
                        var existingInvoiceLine = await newContext.InvoiceLines
                            .FirstOrDefaultAsync(il => il.InvoiceLineId == invoiceLineMapping.NewInvoiceLineId);

                        if (existingInvoiceLine == null)
                        {
                            _logger.LogError($"[DATABASE] UpdateInNewDatabase: InvoiceLine has not been found in new database for NewInvoiceLineId: {invoiceLineMapping.NewInvoiceLineId} (OldInvoiceLineId: {oldInvoiceLine.InvoiceLineId}). SyncId: {syncId}");
                            throw new InvalidOperationException($"InvoiceLine has not been found in new database for NewInvoiceLineId: {invoiceLineMapping.NewInvoiceLineId} (OldInvoiceLineId: {oldInvoiceLine.InvoiceLineId}). SyncId: {syncId}");
                        }

                        // 3. Get InvoiceMapping in order to receive NewInvoiceId for existing newInvoiceLine
                        var invoiceMapping = await newContext.InvoiceMappings
                            .FirstOrDefaultAsync(im => im.NewInvoiceId == existingInvoiceLine.InvoiceId);

                        if (invoiceMapping == null)
                        {
                            _logger.LogError($"[DATABASE] UpdateInNewDatabase: InvoiceMapping has not been found for NewInvoiceId (from NewInvoiceLineId: {oldInvoiceLine.InvoiceLineId}). SyncId: {syncId}");
                            throw new InvalidOperationException($"InvoiceMapping has not been found for NewInvoiceId (from NewInvoiceLineId: {oldInvoiceLine.InvoiceLineId}). SyncId: {syncId}");
                        }

                        // 4. Update existing InvoiceLine in new database
                        existingInvoiceLine.InvoiceId = invoiceMapping.NewInvoiceId;
                        existingInvoiceLine.TrackId = oldInvoiceLine.TrackId;
                        existingInvoiceLine.UnitPrice = oldInvoiceLine.UnitPrice;
                        existingInvoiceLine.Quantity = oldInvoiceLine.Quantity;

                        await newContext.SaveChangesAsync();

                        await _uniqueIdentifiersService.MarkIdentifierAsProcessedInNewDatabase(uniqueIdentifier, newContext);

                        await transaction.CommitAsync();
                        _logger.LogDebug($"[DATABASE] [SUCCESS] UpdateInNewDatabase: Invoice line (OldInvoiceLineId: {oldInvoiceLine.InvoiceLineId}, NewInvoiceLineId: {existingInvoiceLine.InvoiceLineId}) updated successfully. SyncId: {syncId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[DATABASE] UpdateInNewDatabase: Error while updating invoice line (OldInvoiceLineId: {oldInvoiceLine?.InvoiceLineId ?? -1}). SyncId: {syncId}. Exception: {ex.Message}");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task UpdateInOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            _logger.LogDebug($"[DATABASE] [START] InvoiceLineService.UpdateInOldDatabase: SyncId: {syncId}. UniqueIdentifier: {uniqueIdentifier}");

            using (var newContext = _newContextFactory.CreateDbContext())
            using (var oldContext = _oldContextFactory.CreateDbContext())
            {
                var settings = new JsonSerializerSettings();
                settings.ConfigureConverters();

                var newInvoiceLine = JsonConvert.DeserializeObject<Common.ModelsNewSchema.InvoiceLine>(eventBody.ToString(), settings);

                if (newInvoiceLine == null)
                {
                    _logger.LogWarning($"[DATABASE] UpdateInOldDatabase: Deserializing new invoice line has not succeeded for eventBody: {eventBody}. SyncId: {syncId}");
                    return;
                }

                using (var transaction = await oldContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        // 1. Get InvoiceLineMapping in order to get OldInvoiceLineId
                        var invoiceLineMapping = await newContext.InvoiceLineMappings
                            .FirstOrDefaultAsync(ilm => ilm.NewInvoiceLineId == newInvoiceLine.InvoiceLineId);

                        if (invoiceLineMapping == null)
                        {
                            _logger.LogError($"[DATABASE] UpdateInOldDatabase: InvoiceLineMapping has not been found for NewInvoiceLineId: {newInvoiceLine.InvoiceLineId}. SyncId: {syncId}");
                            throw new InvalidOperationException($"InvoiceLineMapping has not been found for NewInvoiceLineId: {newInvoiceLine.InvoiceLineId}. SyncId: {syncId}");
                        }

                        // 2. Get existing InvoiceLine from old database using OldInvoiceLineId
                        var existingInvoiceLine = await oldContext.InvoiceLines
                            .FirstOrDefaultAsync(il => il.InvoiceLineId == invoiceLineMapping.OldInvoiceLineId);

                        if (existingInvoiceLine == null)
                        {
                            _logger.LogError($"[DATABASE] UpdateInOldDatabase: InvoiceLine has not been found in old database for OldInvoiceLineId: {invoiceLineMapping.OldInvoiceLineId} (NewInvoiceLineId: {newInvoiceLine.InvoiceLineId}). SyncId: {syncId}");
                            throw new InvalidOperationException($"InvoiceLine has not been found in old database for OldInvoiceLineId: {invoiceLineMapping.OldInvoiceLineId} (NewInvoiceLineId: {newInvoiceLine.InvoiceLineId}). SyncId: {syncId}");
                        }

                        // 3. Get InvoiceMapping in order to get OldInvoiceId from newInvoiceLine
                        var invoiceMapping = await newContext.InvoiceMappings
                            .FirstOrDefaultAsync(im => im.NewInvoiceId == newInvoiceLine.InvoiceId);

                        if (invoiceMapping == null)
                        {
                            _logger.LogError($"[DATABASE] UpdateInOldDatabase: InvoiceMapping has not been found for NewInvoiceId (from NewInvoiceLineId: {newInvoiceLine.InvoiceLineId}). SyncId: {syncId}");
                            throw new InvalidOperationException($"InvoiceMapping has not been found for NewInvoiceId (from NewInvoiceLineId: {newInvoiceLine.InvoiceLineId}). SyncId: {syncId}");
                        }

                        // 4. Update existing InvoiceLine in the old database
                        existingInvoiceLine.InvoiceId = invoiceMapping.OldInvoiceId;
                        existingInvoiceLine.TrackId = newInvoiceLine.TrackId;
                        existingInvoiceLine.UnitPrice = newInvoiceLine.UnitPrice;
                        existingInvoiceLine.Quantity = newInvoiceLine.Quantity;

                        await oldContext.SaveChangesAsync();

                        await _uniqueIdentifiersService.MarkIdentifierAsProcessedInOldDatabase(uniqueIdentifier, oldContext);

                        await transaction.CommitAsync();
                        _logger.LogDebug($"[DATABASE] [SUCCESS] UpdateInOldDatabase: Invoice line (NewInvoiceLineId: {newInvoiceLine.InvoiceLineId}, OldInvoiceLineId: {existingInvoiceLine.InvoiceLineId}) updated successfully. SyncId: {syncId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[DATABASE] UpdateInOldDatabase: Error while updating invoice line (NewInvoiceLineId: {newInvoiceLine?.InvoiceLineId.ToString() ?? Guid.Empty.ToString()}). SyncId: {syncId}. Exception: {ex.Message}");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task DeleteFromNewDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            _logger.LogDebug($"[DATABASE] [START] InvoiceLineService.DeleteFromNewDatabase: SyncId: {syncId}. UniqueIdentifier: {uniqueIdentifier}");

            using (var newContext = _newContextFactory.CreateDbContext())
            {
                var settings = new JsonSerializerSettings();
                settings.ConfigureConverters();

                var oldInvoiceLine = JsonConvert.DeserializeObject<Common.ModelsOldSchema.InvoiceLine>(eventBody.ToString(), settings);

                if (oldInvoiceLine == null)
                {
                    _logger.LogWarning($"[DATABASE] DeleteFromNewDatabase: Deserializing old invoice line has not succeeded for eventBody: {eventBody}. SyncId: {syncId}");
                    return;
                }

                using (var transaction = await newContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        // 1. Get InvoiceLineMapping in order to get NewInvoiceLineId
                        var invoiceLineMapping = await newContext.InvoiceLineMappings
                            .FirstOrDefaultAsync(ilm => ilm.OldInvoiceLineId == oldInvoiceLine.InvoiceLineId);

                        if (invoiceLineMapping == null)
                        {
                            _logger.LogWarning($"[DATABASE] DeleteFromNewDatabase: InvoiceLineMapping has not been found for OldInvoiceLineId: {oldInvoiceLine.InvoiceLineId}. SyncId: {syncId}");
                            return;
                        }

                        // 2. Get existing InvoiceLine from new database using NewInvoiceLineId
                        var existingInvoiceLine = await newContext.InvoiceLines
                            .FirstOrDefaultAsync(il => il.InvoiceLineId == invoiceLineMapping.NewInvoiceLineId);

                        if (existingInvoiceLine != null)
                        {
                            newContext.InvoiceLines.Remove(existingInvoiceLine);
                            await newContext.SaveChangesAsync();
                            await _uniqueIdentifiersService.MarkIdentifierAsProcessedInNewDatabase(uniqueIdentifier, newContext);
                            await transaction.CommitAsync();
                            _logger.LogDebug($"[DATABASE] [SUCCESS] DeleteFromNewDatabase: Invoice line (OldInvoiceLineId: {oldInvoiceLine.InvoiceLineId}, NewInvoiceLineId: {existingInvoiceLine.InvoiceLineId}) removed successfully. SyncId: {syncId}");
                        }
                        else
                        {
                            _logger.LogWarning($"[DATABASE] DeleteFromNewDatabase: Invoice line has not been found in new database for NewInvoiceLineId: {invoiceLineMapping.NewInvoiceLineId} (OldInvoiceLineId: {oldInvoiceLine.InvoiceLineId}). SyncId: {syncId} - maybe it has already been deleted.");
                            await transaction.RollbackAsync();
                        }

                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[DATABASE] DeleteFromNewDatabase: Error while removing invoice line (OldInvoiceLineId: {oldInvoiceLine?.InvoiceLineId ?? -1}). SyncId: {syncId}. Exception: {ex.Message}");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task DeleteFromOldDatabase(JObject eventBody, string syncId, string uniqueIdentifier)
        {
            _logger.LogDebug($"[DATABASE] [START] InvoiceLineService.DeleteFromOldDatabase: SyncId: {syncId}. UniqueIdentifier: {uniqueIdentifier}");

            using (var newContext = _newContextFactory.CreateDbContext())
            using (var oldContext = _oldContextFactory.CreateDbContext())
            {
                var settings = new JsonSerializerSettings();
                settings.ConfigureConverters();

                var newInvoiceLine = JsonConvert.DeserializeObject<Common.ModelsNewSchema.InvoiceLine>(eventBody.ToString(), settings);

                if (newInvoiceLine == null)
                {
                    _logger.LogWarning($"[DATABASE] DeleteFromOldDatabase: Deserializing new invoice line has not succeeded for eventBody: {eventBody}. SyncId: {syncId}");
                    return;
                }

                using (var transaction = await oldContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        // 1. Get InvoiceLineMapping in order to get OldInvoiceLineId
                        var invoiceLineMapping = await newContext.InvoiceLineMappings
                            .FirstOrDefaultAsync(ilm => ilm.NewInvoiceLineId == newInvoiceLine.InvoiceLineId);

                        if (invoiceLineMapping == null)
                        {
                            _logger.LogWarning($"[DATABASE] DeleteFromOldDatabase: InvoiceLineMapping has not been found for NewInvoiceLineId: {newInvoiceLine.InvoiceLineId}. SyncId: {syncId}");
                            return;
                        }

                        // 2. Get existing InvoiceLine from old database using OldInvoiceLineId
                        var existingInvoiceLine = await oldContext.InvoiceLines
                            .FirstOrDefaultAsync(il => il.InvoiceLineId == invoiceLineMapping.OldInvoiceLineId);

                        if (existingInvoiceLine != null)
                        {
                            oldContext.InvoiceLines.Remove(existingInvoiceLine);
                            await oldContext.SaveChangesAsync();
                            await _uniqueIdentifiersService.MarkIdentifierAsProcessedInOldDatabase(uniqueIdentifier, oldContext);
                            await transaction.CommitAsync();
                            _logger.LogDebug($"[DATABASE] [SUCCESS] DeleteFromOldDatabase: Invoice line (NewInvoiceLineId: {newInvoiceLine.InvoiceLineId}, OldInvoiceLineId: {existingInvoiceLine.InvoiceLineId}) successfully removed from old database. SyncId: {syncId}");
                        }
                        else
                        {
                            _logger.LogWarning($"[DATABASE] DeleteFromOldDatabase: Invoice line has not been found in old database for OldInvoiceLineId: {invoiceLineMapping.OldInvoiceLineId} (NewInvoiceLineId: {newInvoiceLine.InvoiceLineId}). SyncId: {syncId} - maybe it has already been removed.");
                            await transaction.RollbackAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[DATABASE] DeleteFromOldDatabase: Error while removing invoice line from old database (NewInvoiceLineId: {newInvoiceLine?.InvoiceLineId.ToString() ?? Guid.Empty.ToString()}). SyncId: {syncId}. Exception: {ex.Message}");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }
    }
}