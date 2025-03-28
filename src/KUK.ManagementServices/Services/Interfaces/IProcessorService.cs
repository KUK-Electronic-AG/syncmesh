namespace KUK.ManagementServices.Services.Interfaces
{
    public interface IProcessorService
    {
        Task<bool> DeleteProcessorLogs();
        Task<bool> AreProcessorLogsFreeFromErrors();
        Task<bool> StartProcessorAsync();
        Task<bool> KillProcessorAsync();
        Task<bool> WaitForProcessorToFinishInitializatonAsync();
    }

}
