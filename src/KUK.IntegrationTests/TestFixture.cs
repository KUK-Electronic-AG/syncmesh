using KUK.Common;
using KUK.Common.Services;
using KUK.ManagementServices.Services;
using KUK.ManagementServices.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KUK.IntegrationTests
{
    public class TestFixture : IAsyncLifetime
    {
        public IDockerService DockerService { get; private set; }
        public IDatabaseManagementService DatabaseManagementService { get; private set; }
        public ISchemaInitializerService SchemaInitializerService { get; private set; }
        public IConnectorsRegistrationService ConnectorsRegistrationService { get; private set; }
        public IProcessorService ProcessorService { get; private set; }
        public IDatabaseOperationsService DatabaseOperationsService { get; private set; }
        public IQuickActionsService QuickActionsService { get; private set; }
        public IDatabaseDumpService DatabaseDumpService { get; private set; }
        public IUtilitiesService UtilitiesService { get; private set; }
        public IDatabaseDirectConnectionService DatabaseDirectConnectionService { get; private set; }
        public IExternalConnectionService ExternalConnectionService { get; private set; }
        public ILogger<IntegrationTests> Logger { get; private set; }
        public IKafkaService KafkaService { get; private set; }

        public async Task InitializeAsync()
        {
            // Add logging registration
            var services = new ServiceCollection();

            var directory = @"C:\IntegrationTestsLogs";
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            services.AddLogging(builder =>
            {
                builder.AddProvider(new FileLoggerProvider(@$"{directory}\{DateTime.Now.ToString("yyyyMMdd-HHmmss-fffff")}.txt", LogLevel.Trace));
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddFilter((category, logLevel) =>
                    category == "KUK.IntegrationTests.IntegrationTests" && logLevel >= LogLevel.Trace);
            });

            var serviceProvider = services.BuildServiceProvider();
            Logger = serviceProvider.GetService<ILogger<IntegrationTests>>();

            // Check if this is for Docker or Online run
            var environmentDestinationVariableName = "DebeziumWorker_EnvironmentDestination";
            string environmentDestination = Environment.GetEnvironmentVariable(environmentDestinationVariableName);
            if (string.IsNullOrWhiteSpace(environmentDestination))
                throw new ArgumentException($"Variable {environmentDestination} is not set in environment variables.");

            // Make sure that appsettings.json is read from the correct path
            var currentDirectory = Directory.GetCurrentDirectory();
            var appSettingsPath = Path.Combine(currentDirectory, $"appsettings.IntegrationTests.{environmentDestination}.json");

            // Create configuration builder object and use appsettings.json from current location
            var configuration = new ConfigurationBuilder()
                .SetBasePath(currentDirectory)
                .AddJsonFile($"appsettings.IntegrationTests.{environmentDestination}.json", optional: false, reloadOnChange: true)
                .Build();

            // Check if configuration was correctly loaded by looking at the difference
            // between appsettings files from various locations
            var serverName = configuration["Databases:OldDatabase:ServerName"];
            if (string.IsNullOrEmpty(serverName))
            {
                throw new InvalidOperationException(
                    "Appsettings.IntegrationTests.json wasn't correctly loaded." +
                    "It might be issue with order of files being loaded or not copying them to correct output directory.");
            }

            services.AddSingleton<IConfiguration>(configuration);

            // Use the AddAppSettingsConfig extension method to transform and register AppSettingsConfig
            services.AddAppSettingsConfig(configuration);
            serviceProvider = services.BuildServiceProvider();

            // Retrieve the transformed AppSettingsConfig
            var appSettingsConfig = serviceProvider.GetService<AppSettingsConfig>();

            services.AddDatabaseContexts(appSettingsConfig);
            services.AddHttpClient();

            services.AddSingleton<IDockerService, DockerService>();
            services.AddSingleton<IDatabaseManagementService>(provider =>
                new DatabaseManagementService(appSettingsConfig, provider.GetRequiredService<ILogger<DatabaseManagementService>>()));
            services.AddSingleton<ISchemaInitializerService, SchemaInitializerService>();
            services.AddSingleton<IQuickActionsService, QuickActionsService>();
            services.AddSingleton<IConnectorsRegistrationService>(provider =>
                new ConnectorsRegistrationService(
                    provider.GetRequiredService<HttpClient>(),
                    provider.GetRequiredService<ILogger<ConnectorsRegistrationService>>(),
                    appSettingsConfig,
                    provider.GetRequiredService<IConfiguration>(),
                    provider.GetRequiredService<IUtilitiesService>(),
                    provider.GetRequiredService<IExternalConnectionService>()
                    ));
            services.AddSingleton<IProcessorService>(provider =>
                new ProcessorService(
                    provider.GetRequiredService<ILogger<ProcessorService>>(),
                    appSettingsConfig,
                    provider.GetRequiredService<HttpClient>()));
            services.AddSingleton<IDatabaseOperationsService, DatabaseOperationsService>();
            services.AddSingleton<IDatabaseDumpService, DatabaseDumpService>();
            services.AddSingleton<IContainersHealthCheckService, ContainersHealthCheckService>();
            services.AddSingleton<IUtilitiesService, UtilitiesService>();
            services.AddSingleton<IDatabaseDirectConnectionService, DatabaseDirectConnectionService>();
            services.AddSingleton<IExternalConnectionService, ExternalConnectionService>();
            services.AddSingleton<IKafkaService, KafkaService>();

            // Add logging registration
            services.AddLogging(configure =>
            {
                configure.AddConsole();
                configure.AddDebug(); // Output window in Visual Studio
            });
            Logger = serviceProvider.GetService<ILogger<IntegrationTests>>();

            serviceProvider = services.BuildServiceProvider();

            DockerService = serviceProvider.GetService<IDockerService>();
            DatabaseManagementService = serviceProvider.GetService<IDatabaseManagementService>();
            DatabaseDumpService = serviceProvider.GetService<IDatabaseDumpService>();
            SchemaInitializerService = serviceProvider.GetService<ISchemaInitializerService>();
            ConnectorsRegistrationService = serviceProvider.GetService<IConnectorsRegistrationService>();
            ProcessorService = serviceProvider.GetService<IProcessorService>();
            DatabaseOperationsService = serviceProvider.GetService<IDatabaseOperationsService>();
            QuickActionsService = serviceProvider.GetService<IQuickActionsService>();
            UtilitiesService = serviceProvider.GetService<IUtilitiesService>();
            DatabaseDirectConnectionService = serviceProvider.GetService<IDatabaseDirectConnectionService>();
            ExternalConnectionService = serviceProvider.GetService<IExternalConnectionService>();
            KafkaService = serviceProvider.GetService<IKafkaService>();
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}