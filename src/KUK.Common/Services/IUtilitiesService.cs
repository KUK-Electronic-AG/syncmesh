namespace KUK.Common.Services
{
    public interface IUtilitiesService
    {
        string GetEnvironmentDestinationString();
        ApplicationDestinationMode GetOnlineOrDockerMode();
    }
}
