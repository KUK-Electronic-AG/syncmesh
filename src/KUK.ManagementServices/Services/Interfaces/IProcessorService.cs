namespace KUK.ManagementServices.Services.Interfaces
{
    public interface IProcessorService
    {
        Task<bool> DeleteProcessorLogs();
        Task<bool> AreProcessorLogsFreeFromErrors(string logsPath);
        Task<bool> StartProcessorAsync();
        Task<bool> KillProcessorAsync();
    }
}
