using KUK.ChinookSync.Commands;
using KUK.ChinookSync.Contexts;
using KUK.ChinookSync.Services;
using KUK.ChinookSync.Services.Domain;
using KUK.ChinookSync.Services.Domain.Interfaces;
using KUK.ChinookSync.Services.Management;
using KUK.ChinookSync.Services.Management.Interfaces;
using KUK.Common;
using KUK.Common.Contexts;
using KUK.Common.MigrationLogic;
using KUK.Common.MigrationLogic.Interfaces;
using KUK.Common.Services;
using KUK.KafkaProcessor.Commands;
using KUK.KafkaProcessor.EventProcessing;
using KUK.KafkaProcessor.Services;
using KUK.KafkaProcessor.Services.Interfaces;
using KUK.KafkaProcessor.Utilities;
using KUK.ManagementServices.Services;
using KUK.ManagementServices.Services.Interfaces;
using Marten;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Weasel.Core;

namespace KUK.ChinookIntegrationTests
{
    public class TestFixture : IAsyncLifetime
    {
        public IDockerService DockerService { get; private set; }
        public IDatabaseManagementService DatabaseManagementService { get; private set; }
        public ISchemaInitializerService SchemaInitializerService { get; private set; }
        public IConnectorsRegistrationService ConnectorsRegistrationService { get; private set; }
        public IProcessorService ProcessorService { get; private set; }
        public IProcessingService ChinookProcessingService { get; private set; }
        public IDatabaseOperationsService DatabaseOperationsService { get; private set; }
        public IQuickActionsService QuickActionsService { get; private set; }
        public IDatabaseDumpService DatabaseDumpService { get; private set; }
        public IUtilitiesService UtilitiesService { get; private set; }
        public IDatabaseDirectConnectionService DatabaseDirectConnectionService { get; private set; }
        public IExternalConnectionService ExternalConnectionService { get; private set; }
        public ILogger<IntegrationTests> Logger { get; private set; }
        public IKafkaService KafkaService { get; private set; }
        public IRunnerService RunnerService { get; private set; }
        public IEventCommandFactory EventCommandFactory { get; private set; }
        public ICustomerService CustomerService { get; private set; }
        public IInvoiceService InvoiceService { get; private set; }
        public IInvoiceLineService InvoiceLineService { get; private set; }
        public IAddressService AddressService { get; private set; }
        public IDomainDependencyService DomainDependencyService { get; private set; }
        public ICustomTriggersCreationService<OldDbContext, NewDbContext> CustomTriggersCreationService { get; private set; }
        public ICustomSchemaInitializerService CustomSchemaInitializerService { get; private set; }
        public IConnectorsService ConnectorsService { get; private set; }
        public bool IsDockerMode { get; private set; }
        public string LogsPath { get; private set; }

        public async Task InitializeAsync()
        {
            // Add logging registration
            var services = new ServiceCollection();

            var directory = @"C:\IntegrationTestsLogs";
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var filePath = @$"{directory}\log_{DateTime.Now:yyyyMMdd-HHmmss-fffff}.txt";
            LogsPath = filePath;

            services.AddLogging(builder =>
            {
                builder.AddProvider(new FileLoggerProvider(filePath, LogLevel.Trace));
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
                builder.AddFilter("Microsoft.Extensions", LogLevel.Warning);
                builder.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
                builder.AddFilter("Microsoft.AspNetCore.Mvc.Infrastructure", LogLevel.Warning);
                builder.AddFilter("Microsoft.AspNetCore.Routing.EndpointMiddleware", LogLevel.Warning);
                builder.AddFilter("System.Net.Http", LogLevel.Warning);
                builder.AddFilter("KUK", LogLevel.Debug);
            });

            var serviceProvider = services.BuildServiceProvider();
            Logger = serviceProvider.GetService<ILogger<IntegrationTests>>();

            // Check if this is for Docker or Online run
            var environmentDestinationVariableName = "DebeziumWorker_EnvironmentDestination";
            string environmentDestination = Environment.GetEnvironmentVariable(environmentDestinationVariableName);
            if (string.IsNullOrWhiteSpace(environmentDestination))
                throw new ArgumentException($"Variable {environmentDestination} is not set in environment variables.");

            if (environmentDestination.ToUpper() == "DOCKER")
            {
                IsDockerMode = true;
            }

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

            services.AddDatabaseContexts(appSettingsConfig, ServiceLifetime.Transient);
            services.AddHttpClient();

            services.AddSingleton<IDockerService, DockerService>();
            services.AddSingleton<IDatabaseManagementService>(provider =>
                new DatabaseManagementService(
                    appSettingsConfig,
                    provider.GetRequiredService<ILogger<DatabaseManagementService>>(),
                    provider.GetRequiredService<IConfiguration>()));
            services.AddSingleton<ISchemaInitializerService>(provider =>
                new SchemaInitializerService(
                    provider.GetRequiredService<Chinook1DataChangesContext>(),
                    provider.GetRequiredService<Chinook1RootContext>(),
                    provider.GetRequiredService<Chinook2Context>(),
                    provider.GetRequiredService<ILogger<SchemaInitializerService>>(),
                    appSettingsConfig,
                    provider.GetRequiredService<IDockerService>(),
                    provider.GetRequiredService<IDatabaseManagementService>(),
                    provider.GetRequiredService<IUtilitiesService>(),
                    provider.GetRequiredService<IDatabaseDirectConnectionService>(),
                    provider.GetRequiredService<IConfiguration>(),
                    provider.GetRequiredService<ICustomTriggersCreationService<OldDbContext, NewDbContext>>(),
                    provider.GetRequiredService<ICustomSchemaInitializerService>()));
            services.AddSingleton<IQuickActionsService, QuickActionsService>();
            services.AddSingleton<IHostApplicationLifetime, DummyHostApplicationLifetime>();
            services.AddSingleton<GlobalState, GlobalState>();
            services.AddSingleton<IEventsSortingService, EventsSortingService>();
            services.AddMemoryCache(options =>
            {
                options.ExpirationScanFrequency = TimeSpan.FromMinutes(5);
            });
            services.AddSingleton<IRetryHelper>(provider =>
                new RetryHelper(
                    provider.GetRequiredService<GlobalState>(),
                    provider.GetRequiredService<ILogger<RetryHelper>>()));
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
            services.AddSingleton<IDataMigrationRunner<Chinook1DataChangesContext, Chinook2Context>, DataMigrationRunner<Chinook1DataChangesContext, Chinook2Context>>();
            services.AddSingleton<ITriggersCreationService<Chinook1DataChangesContext, Chinook2Context>, TriggersCreationService<Chinook1DataChangesContext, Chinook2Context>>();
            services.AddScoped<IRunnerService>(provider =>
                new RunnerService(
                    provider.GetRequiredService<IDatabaseEventProcessorService>(),
                    provider.GetRequiredService<IConnectorsService>(),
                    provider.GetRequiredService<GlobalState>()));
            services.AddSingleton<IUniqueIdentifiersService, UniqueIdentifiersService>();
            services.AddSingleton<IDocumentStore>(sp =>
            {
                return DocumentStore.For(options =>
                {
                    options.Connection(appSettingsConfig.NewDatabaseConnectionString);
                    options.AutoCreateSchemaObjects = AutoCreate.All;
                });
            });

            services.AddDbContextFactory<Chinook1DataChangesContext>(options =>
                options.UseMySQL(appSettingsConfig.OldDatabaseConnectionString));
            services.AddDbContextFactory<Chinook2Context>(options =>
                options.UseNpgsql(appSettingsConfig.NewDatabaseConnectionString));

            services.AddScoped<IDatabaseEventProcessorService>(provider =>
                new DatabaseEventProcessorService(
                    provider.GetRequiredService<IConfiguration>(),
                    provider.GetRequiredService<ILogger<DatabaseEventProcessorService>>(),
                    provider.GetRequiredService<IEventCommandFactory>(),
                    provider.GetRequiredService<IDocumentStore>(),
                    provider.GetRequiredService<IKafkaService>(),
                    provider.GetRequiredService<IHostApplicationLifetime>(),
                    provider.GetRequiredService<IUniqueIdentifiersService>(),
                    provider.GetRequiredService<IRetryHelper>(),
                    provider.GetRequiredService<IUtilitiesService>(),
                    provider.GetRequiredService<IEventsSortingService>(),
                    provider.GetRequiredService<GlobalState>()));
            services.AddSingleton<IConnectorsService>(provider =>
                new ConnectorsService(
                    provider.GetRequiredService<IConfiguration>(),
                    provider.GetRequiredService<IConnectorsRegistrationService>(),
                    provider.GetRequiredService<ILogger<ConnectorsService>>(),
                    provider.GetRequiredService<GlobalState>(),
                    provider.GetRequiredService<IUtilitiesService>(),
                    provider.GetRequiredService<IExternalConnectionService>(),
                    provider.GetRequiredService<IKafkaService>()));

            services.AddSingleton<IEventCommandFactory, EventCommandFactory>();
            services.AddSingleton<ICustomerService>(provider =>
                new CustomerService(
                    provider.GetRequiredService<IDbContextFactory<Chinook1DataChangesContext>>(),
                    provider.GetRequiredService<IDbContextFactory<Chinook2Context>>(),
                    provider.GetRequiredService<ILogger<CustomerService>>(),
                    provider.GetRequiredService<IUniqueIdentifiersService>()));
            services.AddSingleton<IInvoiceService>(provider =>
                new InvoiceService(
                    provider.GetRequiredService<IDbContextFactory<Chinook1DataChangesContext>>(),
                    provider.GetRequiredService<IDbContextFactory<Chinook2Context>>(),
                    provider.GetRequiredService<ILogger<InvoiceService>>(),
                    provider.GetRequiredService<IUniqueIdentifiersService>()));
            services.AddSingleton<IInvoiceLineService>(provider =>
                new InvoiceLineService(
                    provider.GetRequiredService<IDbContextFactory<Chinook1DataChangesContext>>(),
                    provider.GetRequiredService<IDbContextFactory<Chinook2Context>>(),
                    provider.GetRequiredService<ILogger<InvoiceLineService>>(),
                    provider.GetRequiredService<IUniqueIdentifiersService>()));
            services.AddSingleton<IAddressService>(provider =>
                new AddressService(
                    provider.GetRequiredService<IDbContextFactory<Chinook1DataChangesContext>>(),
                    provider.GetRequiredService<IDbContextFactory<Chinook2Context>>(),
                    provider.GetRequiredService<ILogger<AddressService>>(),
                    provider.GetRequiredService<IUniqueIdentifiersService>()));
            services.AddScoped<IDatabaseMigrator<Chinook1DataChangesContext, Chinook2Context>>(provider =>
                new DatabaseMigrator<Chinook1DataChangesContext, Chinook2Context>(
                    provider.GetRequiredService<Chinook2Context>(),
                    provider.GetRequiredService<IDataMigrationRunner<Chinook1DataChangesContext, Chinook2Context>>(),
                    provider.GetRequiredService<ILogger<DatabaseMigrator<Chinook1DataChangesContext, Chinook2Context>>>(),
                    provider.GetRequiredService<ITriggersCreationService<Chinook1DataChangesContext, Chinook2Context>>(),
                    provider.GetRequiredService<IConfiguration>(),
                    provider.GetRequiredService<IUtilitiesService>()));
            services.AddScoped<IInitializationService>(provider =>
                new InitializationService(
                    provider.GetRequiredService<ILogger<InitializationService>>(),
                    provider.GetRequiredService<IDatabaseMigrator<Chinook1DataChangesContext, Chinook2Context>>(),
                    provider.GetRequiredService<IConfiguration>(),
                    provider.GetRequiredService<IUtilitiesService>()));
            services.AddScoped<IProcessingService>(provider =>
                new ProcessingService(
                    provider.GetRequiredService<ILogger<ProcessingService>>(),
                    provider.GetRequiredService<IInitializationService>(),
                    provider.GetRequiredService<IRunnerService>()));
            services.AddTransient<IDatabaseOperationsService, DatabaseOperationsService>();
            services.AddSingleton<IDatabaseDumpService, DatabaseDumpService>();
            services.AddSingleton<IContainersHealthCheckService, ContainersHealthCheckService>();
            services.AddSingleton<IUtilitiesService>(provider => new UtilitiesService(provider));
            services.AddSingleton<IDatabaseDirectConnectionService, DatabaseDirectConnectionService>();
            services.AddSingleton<IExternalConnectionService, ExternalConnectionService>();
            services.AddSingleton<IKafkaService, KafkaService>();

            services.AddScoped<IEventCommandFactory, EventCommandFactory>();
            services.AddScoped<ICustomerService, CustomerService>();
            services.AddScoped<IInvoiceService, InvoiceService>();
            services.AddScoped<IInvoiceLineService, InvoiceLineService>();
            services.AddScoped<IAddressService, AddressService>();

            services.AddScoped<IDomainDependencyService, DomainDependencyService>();
            services.AddScoped<IConnectorsService, ConnectorsService>();
            services.AddScoped<ITriggersCreationService<OldDbContext, NewDbContext>, TriggersCreationService<OldDbContext, NewDbContext>>();
            services.AddScoped<
                ICustomTriggersCreationService<OldDbContext, NewDbContext>,
                CustomTriggersCreationService<OldDbContext, NewDbContext>>();
            services.AddScoped<ICustomSchemaInitializerService, CustomSchemaInitializerService>();

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
            ChinookProcessingService = serviceProvider.GetService<IProcessingService>();
            DatabaseOperationsService = serviceProvider.GetService<IDatabaseOperationsService>();
            QuickActionsService = serviceProvider.GetService<IQuickActionsService>();
            UtilitiesService = serviceProvider.GetService<IUtilitiesService>();
            DatabaseDirectConnectionService = serviceProvider.GetService<IDatabaseDirectConnectionService>();
            ExternalConnectionService = serviceProvider.GetService<IExternalConnectionService>();
            KafkaService = serviceProvider.GetService<IKafkaService>();
            RunnerService = serviceProvider.GetService<IRunnerService>();
            EventCommandFactory = serviceProvider.GetService<IEventCommandFactory>();
            CustomerService = serviceProvider.GetService<ICustomerService>();
            InvoiceService = serviceProvider.GetService<IInvoiceService>();
            InvoiceLineService = serviceProvider.GetService<IInvoiceLineService>();
            AddressService = serviceProvider.GetService<IAddressService>();
            DomainDependencyService = serviceProvider.GetService<IDomainDependencyService>();
            ConnectorsService = serviceProvider.GetService<IConnectorsService>();
            CustomTriggersCreationService = serviceProvider.GetService<ICustomTriggersCreationService<OldDbContext, NewDbContext>>();
            CustomSchemaInitializerService = serviceProvider.GetService<ICustomSchemaInitializerService>();
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}