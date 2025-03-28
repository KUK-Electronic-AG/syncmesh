using KUK.ManagementServices.Utilities;

namespace KUK.ManagementServices.Services.Interfaces
{
    public interface IQuickActionsService
    {
        Task<ServiceActionStatus> QuickDelete();
        Task<ServiceActionStatus> QuickStartup();
        Task<ServiceActionStatus> QuickCreateOldCustomer();
        Task<ServiceActionStatus> QuickCreateOldInvoice();
        Task<ServiceActionStatus> QuickCreateNewCustomer();
        Task<ServiceActionStatus> QuickCreateNewInvoice();
        Task<ServiceActionStatus> QuickCreateNewAddress();
    }
}
