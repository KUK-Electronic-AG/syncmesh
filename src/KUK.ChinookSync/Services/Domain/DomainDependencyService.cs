using KUK.ChinookSync.Services.Domain.Interfaces;
using KUK.KafkaProcessor.Services.Interfaces;

namespace KUK.ChinookSync.Services.Domain
{
    public class DomainDependencyService : IDomainDependencyService
    {
        private readonly ILogger<DomainDependencyService> _logger;
        private readonly IInvoiceService _invoiceService;
        private readonly ICustomerService _customerService;
        private readonly IAddressService _addressService;

        public DomainDependencyService(
            ILogger<DomainDependencyService> logger,
            IInvoiceService invoiceService,
            ICustomerService customerService,
            IAddressService addressService)
        {
            _logger = logger;
            _invoiceService = invoiceService;
            _customerService = customerService;
            _addressService = addressService;
        }

        public async Task<bool> CheckDependencyExistsAsync(string dependencyType, string aggregateId, string source)
        {
            _logger.LogDebug($"CheckDependencyExistsAsync (dependencyType={dependencyType}, aggregateId={aggregateId}, source={source})");
            string normalizedDependencyType = NormalizeDependencyType(dependencyType);

            // We always return true for special case of address
            if (aggregateId == "CREATE_NEW_ADDRESS")
            {
                return true;
            }

            switch (normalizedDependencyType)
            {
                case "INVOICE":
                    switch (source.ToUpper())
                    {
                        case "OLD_TO_NEW":
                            _logger.LogDebug($"Checking in old - CheckDependencyExistsAsync (normalizedDependencyType={normalizedDependencyType}, aggregateId={aggregateId})");
                            var foundInOld = await _invoiceService.MappingExists(int.Parse(aggregateId));
                            _logger.LogDebug($"Found in old: {foundInOld} - CheckDependencyExistsAsync (normalizedDependencyType={normalizedDependencyType}, aggregateId={aggregateId})");
                            return foundInOld;
                        case "NEW_TO_OLD":
                            _logger.LogDebug($"Checking in new - CheckDependencyExistsAsync (normalizedDependencyType={normalizedDependencyType}, aggregateId={aggregateId})");
                            var foundInNew = await _invoiceService.MappingExists(Guid.Parse(aggregateId));
                            _logger.LogDebug($"Found in new: {foundInNew} - CheckDependencyExistsAsync (normalizedDependencyType={normalizedDependencyType}, aggregateId={aggregateId})");
                            return foundInNew;
                        default:
                            _logger.LogWarning($"CheckDependencyExistsAsync - unknown source {source}");
                            throw new InvalidOperationException($"Unknown source {source}");
                    }
                case "CUSTOMER":
                    switch (source.ToUpper())
                    {
                        case "OLD_TO_NEW":
                            _logger.LogDebug($"Checking in old - CheckDependencyExistsAsync (normalizedDependencyType={normalizedDependencyType}, aggregateId={aggregateId})");
                            var foundInOld = await _customerService.MappingExists(int.Parse(aggregateId));
                            _logger.LogDebug($"Found in old: {foundInOld} - CheckDependencyExistsAsync (normalizedDependencyType={normalizedDependencyType}, aggregateId={aggregateId})");
                            return foundInOld;
                        case "NEW_TO_OLD":
                            _logger.LogDebug($"Checking in new - CheckDependencyExistsAsync (normalizedDependencyType={normalizedDependencyType}, aggregateId={aggregateId})");
                            var foundInNew = await _customerService.MappingExists(Guid.Parse(aggregateId));
                            _logger.LogDebug($"Found in new: {foundInNew} - CheckDependencyExistsAsync (normalizedDependencyType={normalizedDependencyType}, aggregateId={aggregateId})");
                            return foundInNew;
                        default:
                            _logger.LogWarning($"CheckDependencyExistsAsync - unknown source {source}");
                            throw new InvalidOperationException($"Unknown source {source}");
                    }
                case "ADDRESS":
                    switch (source.ToUpper())
                    {
                        case "OLD_TO_NEW":
                            _logger.LogDebug($"Checking in old - CheckDependencyExistsAsync (normalizedDependencyType={normalizedDependencyType}, aggregateId={aggregateId})");
                            var foundInOld = await _addressService.MappingExists(int.Parse(aggregateId));
                            _logger.LogDebug($"Found in old: {foundInOld} - CheckDependencyExistsAsync (normalizedDependencyType={normalizedDependencyType}, aggregateId={aggregateId})");
                            return foundInOld;
                        case "NEW_TO_OLD":
                            _logger.LogDebug($"Checking in new - CheckDependencyExistsAsync (normalizedDependencyType={normalizedDependencyType}, aggregateId={aggregateId})");
                            var foundInNew = await _addressService.MappingExists(Guid.Parse(aggregateId));
                            _logger.LogDebug($"Found in new: {foundInNew} - CheckDependencyExistsAsync (normalizedDependencyType={normalizedDependencyType}, aggregateId={aggregateId})");
                            return foundInNew;
                        default:
                            _logger.LogWarning($"CheckDependencyExistsAsync - unknown source {source}");
                            throw new InvalidOperationException($"Unknown source {source}");
                    }
                // REMARK: Here you add new cases for other types like "A", "B", "C" etc.
                default:
                    _logger.LogError($"CheckDependencyExistsAsync - default {normalizedDependencyType}");
                    return false;
            }
        }

        public bool ShouldSkipEnsuringDependency(
            string aggregateId, string dependencyType, string expectedDependencyAggregateId, string operation, bool isDeleteOperation)
        {
            // If ID of dependency not found
            if (string.IsNullOrEmpty(expectedDependencyAggregateId))
            {
                // For DELETE operations we simply skip checking the dependency
                if (isDeleteOperation)
                {
                    _logger.LogDebug("EnsureDependencyForEventAsync: Missing dependency ID for {DependencyType} in DELETE event with aggregateId={AggregateId}. Skipping dependency check.",
                        dependencyType, aggregateId);
                    return true;
                }
                // For UPDATE operation we check if ID of dependency is CREATE_NEW_ADDRESS
                else if (operation.Equals("UPDATED", StringComparison.OrdinalIgnoreCase) && expectedDependencyAggregateId == "CREATE_NEW_ADDRESS")
                {
                    _logger.LogDebug("EnsureDependencyForEventAsync: Ignoring missing AddressId for UPDATE event with aggregateId={AggregateId}.", aggregateId);
                    return true; // We ignore lack of ID in case of update
                }
                // For other operations (CREATED) we log warning but we still process
                else
                {
                    _logger.LogWarning("EnsureDependencyForEventAsync: Missing dependency ID for {DependencyType} in {Operation} event with aggregateId={AggregateId}. ExpectedDependencyAggregateId={ExpectedDependencyAggregateId}. This may indicate an issue with the data.",
                        dependencyType, operation, aggregateId, expectedDependencyAggregateId);
                    return true;
                }
            }

            return false;
        }

        public string GetAggregateIdToSkip(string idFieldName, dynamic outerJson)
        {
            // Check if this event is of type CREATED for customer
            if (outerJson.aggregate_type?.ToString().Equals("CUSTOMER", StringComparison.OrdinalIgnoreCase) == true &&
                outerJson.event_type?.ToString().Equals("CREATED", StringComparison.OrdinalIgnoreCase) == true &&
                idFieldName.Equals("AddressId", StringComparison.OrdinalIgnoreCase))
            {
                // For CREATED event for customer, we return special ID
                // that will be later processed by CustomerService
                return "CREATE_NEW_ADDRESS";
            }

            return null;
        }

        private string NormalizeDependencyType(string dependencyType)
        {
            if (string.IsNullOrEmpty(dependencyType))
            {
                return string.Empty;
            }

            return dependencyType.Trim().ToUpperInvariant();
        }
    }
}
