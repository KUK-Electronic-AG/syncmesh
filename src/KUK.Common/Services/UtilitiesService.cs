namespace KUK.Common.Services
{
    public class UtilitiesService : IUtilitiesService
    {
        public string GetEnvironmentDestinationString()
        {
            var environmentVariableName = "DebeziumWorker_EnvironmentDestination";
            string value = Environment.GetEnvironmentVariable(environmentVariableName);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Environment variable {environmentVariableName} is not set. Suggested values are Docker or Online.");
            }
            return value;
        }

        public ApplicationDestinationMode GetOnlineOrDockerMode()
        {
            // Check if this is for Docker or Online run
            var environmentDestinationVariableName = "DebeziumWorker_EnvironmentDestination";
            string environmentDestination = Environment.GetEnvironmentVariable(environmentDestinationVariableName);
            if (string.IsNullOrWhiteSpace(environmentDestination))
                throw new ArgumentException($"Variable {environmentDestination} is not set in environment variables.");
            switch (environmentDestination.ToLower())
            {
                case "docker":
                    return ApplicationDestinationMode.Docker;
                case "online":
                    return ApplicationDestinationMode.Online;
                default:
                    throw new InvalidOperationException(
                        $"Unknown environment variable value for {environmentDestination}. " +
                        $"Found {environmentDestination}. Expected: Docker or Online");
            }
        }
    }
}
