namespace KUK.Common
{
    public class AppSettingsConfig
    {
        public string RootOldDatabaseConnectionString { get; set; }
        public string OldDatabaseConnectionString { get; set; }
        public string NewDatabaseConnectionString { get; set; }
        public string OldDatabaseName { get; set; }
        public int OldDatabasePort { get; set; }
        public string NewDatabaseName { get; set; }
        public int NewDatabasePort { get; set; }
        public string DockerComposeDirectory { get; set; }
        public string ProcessorLogsPath { get; set; }
        public string ProcessorExePath { get; set; }
        public string FirstConnectorJsonPath { get; set; }
        public string SecondConnectorJsonPath { get; set; }
        public string ProcessorAppSettingsPath { get; set; }
        public string KafkaBootstrapServers { get; set; }
        public string ProcessorAppSettingsFileName
        {
            get
            {
                // REMARK: This is code duplication from UtilitiesService.GetEnvironmentDestinationString
                var environmentVariableName = "DebeziumWorker_EnvironmentDestination";
                string value = Environment.GetEnvironmentVariable(environmentVariableName);
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new InvalidOperationException($"Environment variable {environmentVariableName} is not set. Suggested values are Docker or Online.");
                }

                return $"appsettings.{value}.json";
            }
        }
        public string ProcessorExeFileName { get; } = "KUK.KafkaProcessor.exe";
        public string ProcessorLogsFileName { get; } = "Processor.log";

        public string ProcessorExeFullPath => Path.Combine(ProcessorExePath, ProcessorExeFileName);
        public string ProcessorLogsFullPath => Path.Combine(ProcessorLogsPath, ProcessorLogsFileName);
        public string ProcessorAppSettingsFullPath => Path.Combine(ProcessorAppSettingsPath, ProcessorAppSettingsFileName);
        public string RootAccountName { get; set; }
        public string PostgresAccountName { get; set; }
    }
}
