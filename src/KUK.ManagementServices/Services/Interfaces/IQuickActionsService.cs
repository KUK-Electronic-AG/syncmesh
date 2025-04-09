using KUK.Common.Utilities;
using KUK.ManagementServices.Utilities;

namespace KUK.ManagementServices.Services.Interfaces
{
    public interface IQuickActionsService
    {
        Task<ServiceActionStatus> QuickDelete();
        Task<ServiceActionStatus> QuickStartup();

    }
}
