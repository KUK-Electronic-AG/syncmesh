using KUK.ChinookSync.TestUtilities;
using KUK.Common.Utilities;

namespace KUK.ChinookSync.Services.Management.Interfaces
{
    public interface IDatabaseOperationsService
    {
        Task<OldCustomerOperationResult> CreateCustomerInOldDatabaseAsync();
        Task<OldInvoiceOperationResult> CreateInvoiceInOldDatabaseAsync();
        Task<NewCustomerOperationResult> CreateCustomerInNewDatabaseAsync();
        Task<NewInvoiceOperationResult> CreateInvoiceInNewDatabaseAsync();
        Task<string> CustomerExistsInNewDatabaseAsync(OldCustomerOperationResult customerResult);
        Task<string> InvoiceExistsInNewDatabaseAsync(OldInvoiceOperationResult invoiceResult);
        Task<string> CustomerExistsInOldDatabaseAsync(NewCustomerOperationResult customerResult);
        Task<string> InvoiceExistsInOldDatabaseAsync(NewInvoiceOperationResult invoiceResult);
        Task<ServiceActionStatus> AddColumnToCustomerTableInOldDatabase();
        Task<ServiceActionStatus> AddNewCustomerUsingSubquery();
        Task<ServiceActionStatus> AddNewInvoiceUsingNestedQuery();
        Task<string> CustomerAddedWithSubqueryExistsInNewDatabase();
        Task<string> InvoiceAddedWithNestedQueryExistsInNewDatabase();
        Task<bool> UpdateCustomerCityInOldDatabaseAsync(OldCustomerOperationResult createdCustomerInOldDatabase);
        Task<bool> UpdateCustomerEmailAndAddressInNewDatabaseAsync(OldCustomerOperationResult createdCustomerInOldDatabase);
        Task<string> CustomerCityUpdatedInOldDatabaseAsync(OldCustomerOperationResult createdCustomerInOldDatabase);
        Task<string> CustomerCityUpdatedInNewDatabaseAsync(OldCustomerOperationResult createdCustomerInOldDatabase);
        Task<string> CustomerCityEmailAndCountryUpdatedInOldDatabaseAsync(OldCustomerOperationResult createdCustomerInOldDatabase);
        Task<string> CustomerCityEmailAndCountryUpdatedInNewDatabaseAsync(OldCustomerOperationResult createdCustomerInOldDatabase);
        Task<bool> DeleteCustomerInOldDatabaseAsync(OldCustomerOperationResult createdCustomerInOldDatabase);
        Task<string> FirstCustomerDeletedInOldDatabaseAsync(OldCustomerOperationResult createdCustomerInOldDatabase);
        Task<string> FirstCustomerDeletedInNewDatabaseAsync(OldCustomerOperationResult createdCustomerInOldDatabase);
        Task<bool> DeleteCustomerInNewDatabaseAsync(NewCustomerOperationResult createdCustomerInNewDatabase);
        Task<string> SecondCustomerDeletedInOldDatabaseAsync(NewCustomerOperationResult createdCustomerInNewDatabase);
        Task<string> SecondCustomerDeletedInNewDatabaseAsync(NewCustomerOperationResult createdCustomerInNewDatabase);
        Task<bool> UpdateInvoiceLineInOldDatabaseAsync(OldInvoiceOperationResult createdInvoiceInOldDatabase);
        Task<bool> UpdateInvoiceLineInNewDatabaseAsync(NewInvoiceOperationResult createdInvoiceInNewDatabase);
        Task<string> FirstInvoiceLineUpdatedInOldDatabaseAsync(OldInvoiceOperationResult createdInvoiceInOldDatabase);
        Task<string> FirstInvoiceLineUpdatedInNewDatabaseAsync(OldInvoiceOperationResult createdInvoiceInOldDatabase);
        Task<string> SecondInvoiceLineUpdatedInOldDatabaseAsync(NewInvoiceOperationResult createdInvoiceInNewDatabase);
        Task<string> SecondInvoiceLineUpdatedInNewDatabaseAsync(NewInvoiceOperationResult createdInvoiceInNewDatabase);
    }
}
