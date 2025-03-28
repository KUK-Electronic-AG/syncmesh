using KUK.Common.Contexts;
using KUK.Common.MigrationLogic;
using KUK.Common.MigrationLogic.Interfaces;
using KUK.Common.Services;
using KUK.KafkaProcessor.Commands;
using KUK.KafkaProcessor.EventProcessing;
using KUK.KafkaProcessor.Services;
using KUK.KafkaProcessor.Services.Interfaces;
using KUK.KafkaProcessor.Utilities;
using Marten;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Weasel.Core;

namespace KUK.KafkaProcessorApp
{
    public class Program
    {
        private static IConfiguration _configuration;

        public static async Task Main(string[] args)
        {
            Directory.SetCurrentDirectory(AppContext.BaseDirectory); // required so that we can get assets directory from bin\Debug\net8.0 directory

            // It is used to load appsettings.{environment}.json
            string environmentDestination = GetEnvironmentDestinationString();
            string environmentEnvVariable = $"DebeziumWorker_{environmentDestination}_Environment";
            var environment = Environment.GetEnvironmentVariable(environmentEnvVariable)
                ?? throw new InvalidOperationException(
                    $"You need to provide value to {environmentEnvVariable} environment variable as Development, Staging or Production " +
                    "so that correct appsettings are loaded.");
            if (environment != "Development" && environment != "Staging" && environment != "Production")
            {
                throw new InvalidOperationException(
                    $"Invalid value of {environmentEnvVariable} = {environment}." +
                    $"Expected to have one of the following: Development, Staging or Production." +
                    $"This is important for selecting correct appsettings to load.");
            }

            // Read appsettings.json
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile($"appsettings.{environmentDestination}.json", optional: false, reloadOnChange: true);

            _configuration = configBuilder.Build();

            var testValue = _configuration["Databases:OldDatabase:ConnectionString"];
            if (string.IsNullOrWhiteSpace(testValue)) throw new InvalidOperationException("Application hasn't read configuration correctly");

            var builder = WebApplication.CreateBuilder(args);

            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.ListenAnyIP(5123); // HTTP port
                serverOptions.ListenAnyIP(7040, listenOptions =>
                {
                    listenOptions.UseHttps(); // HTTPS port
                });
            });

            builder.Host.UseSerilog((context, services, configuration) => configuration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore.Mvc.Infrastructure", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore.Routing.EndpointMiddleware", LogEventLevel.Warning)
                .MinimumLevel.Override("KUK", LogEventLevel.Debug)
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File("Logs/Processor.log", shared: true/*, rollingInterval: RollingInterval.Day*/));

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddSingleton<IConfiguration>(_configuration);

            ConfigureServices(builder.Services);

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            // Run the event processor in the background
            var eventProcessorTask = Task.Run(async () =>
            {
                try
                {
                    using (var scope = app.Services.CreateScope())
                    {
                        var eventProcessor = scope.ServiceProvider.GetRequiredService<DatabaseEventProcessor>();
                        await eventProcessor.RunEventProcessingAsync();
                    }
                }
                catch (Exception ex)
                {
                    // TODO: Improve logging
                    Console.WriteLine($"An error occurred: {ex.Message}");
                }
            });

            await app.RunAsync();

            // Ensure the event processor task completes
            await eventProcessorTask;
        }

        public static void ConfigureServices(IServiceCollection services)
        {
            // Getting environment variable names
            string environmentDestination = GetEnvironmentDestinationString();
            string envVariableOldMySqlDebeziumPassword = $"DebeziumWorker_{environmentDestination}_OldDatabaseDebeziumPassword";
            string envVariableNewMySqlDebeziumPassword = $"DebeziumWorker_{environmentDestination}_NewDatabaseDebeziumPassword";
            string envVariablePostgresMartenUserName = $"DebeziumWorker_{environmentDestination}_PostgresMartenRootUserName";
            string envVariablePostgresMartenPassword = $"DebeziumWorker_{environmentDestination}_PostgresMartenRootPassword";
            string envVariablePostgresMartenDatabaseName = $"DebeziumWorker_{environmentDestination}_PostgresMartenDatabaseName";

            // Getting passwords from environment variables
            var oldDatabasePassword = Environment.GetEnvironmentVariable(envVariableOldMySqlDebeziumPassword);
            var newDatabasePassword = Environment.GetEnvironmentVariable(envVariableNewMySqlDebeziumPassword);

            CheckIfEmpty(oldDatabasePassword, envVariableOldMySqlDebeziumPassword);
            CheckIfEmpty(newDatabasePassword, envVariableNewMySqlDebeziumPassword);

            // Configuring connection strings with passwords
            var oldDatabaseConnectionString = _configuration["Databases:OldDatabase:ConnectionString"].Replace("{password}", oldDatabasePassword);
            var newDatabaseConnectionString = _configuration["Databases:NewDatabase:ConnectionString"].Replace("{password}", newDatabasePassword);

            services.AddDbContext<Chinook1DataChangesContext>(options =>
                options
                    .UseMySQL(oldDatabaseConnectionString)
                    /*.EnableDetailedErrors()*/);

            services.AddDbContext<Chinook2Context>(options =>
                options
                    .UseNpgsql(newDatabaseConnectionString)
                    /*.EnableDetailedErrors()*/);

            services.AddDbContextFactory<Chinook1DataChangesContext>(options =>
                options.UseMySQL(oldDatabaseConnectionString),
                ServiceLifetime.Transient);

            services.AddDbContextFactory<Chinook2Context>(options =>
                options.UseNpgsql(newDatabaseConnectionString),
                ServiceLifetime.Transient);

            services.AddSingleton<GlobalState>();

            services.AddScoped<ITriggersCreationService, TriggersCreationService>();
            services.AddScoped<IUniqueIdentifiersService, UniqueIdentifiersService>();
            services.AddScoped<DatabaseEventProcessor>();

            services.AddScoped<DatabaseMigrator>();
            services.AddScoped<DataMigrationRunner>(); // REMARK: It is registered twice, here directly and later with interface

            services.AddScoped<IEventCommandFactory, EventCommandFactory>();
            services.AddScoped<ICustomerService, CustomerService>();
            services.AddScoped<IInvoiceService, InvoiceService>();
            services.AddScoped<IInvoiceLineService, InvoiceLineService>();
            services.AddScoped<IAddressService, AddressService>();

            services.AddSingleton<IUtilitiesService, UtilitiesService>();
            services.AddSingleton<IRetryHelper, RetryHelper>();
            services.AddSingleton<IExternalConnectionService, ExternalConnectionService>();
            services.AddSingleton<IDatabaseDirectConnectionService, DatabaseDirectConnectionService>();
            services.AddScoped<IEventsSortingService, EventsSortingService>();

            services.AddMemoryCache();

            services.AddSingleton<IKafkaService, KafkaService>();
            services.AddScoped<IInitializationService, InitializationService>();
            services.AddScoped<IDatabaseMigrator, DatabaseMigrator>();
            services.AddScoped<IDataMigrationRunner, DataMigrationRunner>();
            services.AddHttpClient();
            string firstConnectorConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "debezium-connector-config-1.json");
            string secondConnectorConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "debezium-connector-config-2.json");
            services.AddSingleton<IConnectorsRegistrationService, ConnectorsRegistrationService>(
                provider => new ConnectorsRegistrationService(
                    provider.GetRequiredService<HttpClient>(),
                    provider.GetRequiredService<ILogger<ConnectorsRegistrationService>>(),
                    firstConnectorConfigPath,
                    secondConnectorConfigPath,
                    provider.GetRequiredService<IConfiguration>(),
                    provider.GetRequiredService<IUtilitiesService>(),
                    provider.GetRequiredService<IExternalConnectionService>()
                    )
                );

            // Marten
            var postgresMartenUserName = Environment.GetEnvironmentVariable(envVariablePostgresMartenUserName);
            CheckIfEmpty(postgresMartenUserName, envVariablePostgresMartenUserName);
            var postgresMartenPassword = Environment.GetEnvironmentVariable(envVariablePostgresMartenPassword);
            CheckIfEmpty(postgresMartenPassword, envVariablePostgresMartenPassword);
            var postgresMartenDatabaseName = Environment.GetEnvironmentVariable(envVariablePostgresMartenDatabaseName);
            CheckIfEmpty(postgresMartenDatabaseName, envVariablePostgresMartenDatabaseName);
            var postgresMartenConnectionString = _configuration["Postgres:ConnectionString"]
                .Replace("{postgresMartenDatabaseName}", postgresMartenDatabaseName)
                .Replace("{postgresUsername}", postgresMartenUserName)
                .Replace("{postgresPassword}", postgresMartenPassword);
            services.AddSingleton<IDocumentStore>(sp =>
            {
                return DocumentStore.For(options =>
                {
                    options.Connection(postgresMartenConnectionString);
                    options.AutoCreateSchemaObjects = AutoCreate.All;
                });
            });
        }

        private static void CheckIfEmpty(string? environmentVariableValue, string environmentVariableName)
        {
            if (string.IsNullOrWhiteSpace(environmentVariableValue))
            {
                throw new InvalidOperationException($"Environment variable {environmentVariableName} is not set");
            }
        }

        private static string GetEnvironmentDestinationString()
        {
            // REMARK: This is code duplication from UtilitiesService.GetEnvironmentDestinationString
            var environmentVariableName = "DebeziumWorker_EnvironmentDestination";
            string value = Environment.GetEnvironmentVariable(environmentVariableName);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Environment variable {environmentVariableName} is not set. Suggested values are Docker or Online.");
            }
            return value;
        }
    }
}
