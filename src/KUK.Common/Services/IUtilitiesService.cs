using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KUK.Common.Services
{
    public interface IUtilitiesService
    {
        string GetEnvironmentDestinationString();
        ApplicationDestinationMode GetOnlineOrDockerMode();
        string GetConnectionString(IConfiguration configuration, string databaseKey, bool useRoot = false);
        IServiceProvider GetServiceProvider();
    }
}
