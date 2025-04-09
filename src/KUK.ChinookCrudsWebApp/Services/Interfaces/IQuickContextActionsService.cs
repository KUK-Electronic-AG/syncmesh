using KUK.Common.Utilities;

namespace KUK.ChinookCrudsWebApp.Services.Interfaces
{
    public interface IQuickContextActionsService
    {
        Task<ServiceActionStatus> QuickCreateOldCustomer();
        Task<ServiceActionStatus> QuickCreateOldInvoice();
        Task<ServiceActionStatus> QuickCreateNewCustomer();
        Task<ServiceActionStatus> QuickCreateNewInvoice();
        Task<ServiceActionStatus> QuickCreateNewAddress();
    }
}
