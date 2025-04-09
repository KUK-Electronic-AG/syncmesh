using System.Diagnostics;
using KUK.ChinookSync.Commands;
using KUK.ChinookSync.Contexts;
using KUK.ChinookSync.Services;
using KUK.ChinookSync.Services.Domain;
using KUK.ChinookSync.Services.Domain.Interfaces;
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

namespace KUK.ChinookSync
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddScoped<IInitializationService, InitializationService>();
            builder.Services.AddScoped<IRunnerService, RunnerService>();
            builder.Services.AddScoped<
                IDataMigrationRunner<Chinook1DataChangesContext, Chinook2Context>,
                DataMigrationRunner<Chinook1DataChangesContext, Chinook2Context>>();
            builder.Services.AddScoped<IProcessingService, ProcessingService>();
            builder.Services.AddScoped<IConnectorsService, ConnectorsService>();

            builder.Services.AddScoped<IEventCommandFactory, EventCommandFactory>();
            builder.Services.AddScoped<ICustomerService, CustomerService>();
            builder.Services.AddScoped<IInvoiceService, InvoiceService>();
            builder.Services.AddScoped<IInvoiceLineService, InvoiceLineService>();
            builder.Services.AddScoped<IAddressService, AddressService>();

            builder.Services.AddScoped<IDomainDependencyService, DomainDependencyService>();
            builder.Services.AddScoped<
                ICustomTriggersCreationService<Chinook1DataChangesContext, Chinook2Context>,
                CustomTriggersCreationService<Chinook1DataChangesContext, Chinook2Context>>();
            builder.Services.AddScoped<ICustomSchemaInitializerService, CustomSchemaInitializerService>();

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
                .WriteTo.File(@"Logs\Processor.log", shared: true/*, rollingInterval: RollingInterval.Day*/));

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            IConfiguration configuration = BuildKukConfiguration();
            AddKukServices(builder.Services, configuration);

            WebApplication app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            Task.Run(async () =>
            {
                using (var scope = app.Services.CreateScope())
                {
                    var processingService = scope.ServiceProvider.GetRequiredService<IProcessingService>();
                    await processingService.CreateConnectorTopics();
                    await processingService.RegisterOldConnector();
                    await processingService.Process();
                }
            });

            using (var scope = app.Services.CreateScope())
            {
                var processingService = scope.ServiceProvider.GetRequiredService<IProcessingService>();
                await processingService.WaitForProcessorToFinishInitializationAsync();
                await processingService.RegisterNewConnector();
            }

            app.Run();
        }

        public static IConfiguration BuildKukConfiguration()
        {
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);

            string environmentDestination = GetEnvironmentDestinationString();

            string integrationTestsSubname = string.Empty;
            if (IsRunningFromIntegrationTests())
            {
                integrationTestsSubname = ".IntegrationTests";
            }

            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile($"appsettings{integrationTestsSubname}.{environmentDestination}.json", optional: false, reloadOnChange: true)
                .Build();
        }

        public static IServiceCollection AddKukServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IConfiguration>(configuration);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore.Mvc.Infrastructure", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore.Routing.EndpointMiddleware", LogEventLevel.Warning)
                .MinimumLevel.Override("KUK", LogEventLevel.Debug)
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File("Logs/Processor.log", shared: true)
                .CreateLogger();

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddSerilog(dispose: true);
            });

            string environmentDestination = GetEnvironmentDestinationString();
            string envVariableOldMySqlDebeziumPassword = $"DebeziumWorker_{environmentDestination}_OldDatabaseDebeziumPassword";
            string envVariableNewMySqlDebeziumPassword = $"DebeziumWorker_{environmentDestination}_NewDatabaseDebeziumPassword";
            string envVariablePostgresMartenUserName = $"DebeziumWorker_{environmentDestination}_PostgresMartenRootUserName";
            string envVariablePostgresMartenPassword = $"DebeziumWorker_{environmentDestination}_PostgresMartenRootPassword";
            string envVariablePostgresMartenDatabaseName = $"DebeziumWorker_{environmentDestination}_PostgresMartenDatabaseName";

            var oldDatabasePassword = Environment.GetEnvironmentVariable(envVariableOldMySqlDebeziumPassword);
            var newDatabasePassword = Environment.GetEnvironmentVariable(envVariableNewMySqlDebeziumPassword);

            CheckIfEmpty(oldDatabasePassword, envVariableOldMySqlDebeziumPassword);
            CheckIfEmpty(newDatabasePassword, envVariableNewMySqlDebeziumPassword);

            var oldDatabaseConnectionString = configuration["Databases:OldDatabase:ConnectionString"].Replace("{password}", oldDatabasePassword);
            var newDatabaseConnectionString = configuration["Databases:NewDatabase:ConnectionString"].Replace("{password}", newDatabasePassword);

            services.AddDbContext<Chinook1DataChangesContext>(options =>
                options.UseMySQL(oldDatabaseConnectionString));

            services.AddDbContext<Chinook2Context>(options =>
                options.UseNpgsql(newDatabaseConnectionString,
                    x => x.MigrationsAssembly("KUK.ChinookSync")));

            services.Add(new ServiceDescriptor(typeof(IOldDataChangesContext),
                provider => provider.GetService<Chinook1DataChangesContext>(), ServiceLifetime.Scoped));
                
            services.AddDbContext<NewDbContext>(options =>
                options.UseNpgsql(newDatabaseConnectionString,
                    x => x.MigrationsAssembly("KUK.ChinookSync")));

            services.AddDbContextFactory<Chinook1DataChangesContext>(options =>
                options.UseMySQL(oldDatabaseConnectionString), ServiceLifetime.Transient);

            services.AddDbContextFactory<Chinook2Context>(options =>
                options.UseNpgsql(newDatabaseConnectionString,
                    x => x.MigrationsAssembly("KUK.ChinookSync")), 
                ServiceLifetime.Transient);

            services.AddSingleton<GlobalState>();

            services.AddScoped<ITriggersCreationService<Chinook1DataChangesContext, Chinook2Context>, TriggersCreationService<Chinook1DataChangesContext, Chinook2Context>>();
            services.AddScoped<IUniqueIdentifiersService, UniqueIdentifiersService>();
            services.AddScoped<IDatabaseEventProcessorService, DatabaseEventProcessorService>();

            services.AddScoped<DatabaseMigrator<Chinook1DataChangesContext, Chinook2Context>>();
            services.AddScoped<DataMigrationRunner<Chinook1DataChangesContext, Chinook2Context>>();

            services.AddScoped<IRunnerService, RunnerService>();



            services.AddSingleton<IUtilitiesService, UtilitiesService>();
            services.AddSingleton<IRetryHelper, RetryHelper>();
            services.AddSingleton<IExternalConnectionService, ExternalConnectionService>();
            services.AddSingleton<IDatabaseDirectConnectionService, DatabaseDirectConnectionService>();
            services.AddScoped<IEventsSortingService, EventsSortingService>();

            services.AddMemoryCache();

            services.AddSingleton<IKafkaService, KafkaService>();
            services.AddScoped<IDatabaseMigrator<Chinook1DataChangesContext, Chinook2Context>, DatabaseMigrator<Chinook1DataChangesContext, Chinook2Context>>();
            services.AddScoped<IDataMigrationRunner<Chinook1DataChangesContext, Chinook2Context>, DataMigrationRunner<Chinook1DataChangesContext, Chinook2Context>>();
            services.AddHttpClient();

            string firstConnectorConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "debezium-connector-config-1.json");
            string secondConnectorConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "debezium-connector-config-2.json");
            services.AddSingleton<IConnectorsRegistrationService, ConnectorsRegistrationService>(provider =>
                new ConnectorsRegistrationService(
                    provider.GetRequiredService<HttpClient>(),
                    provider.GetRequiredService<ILogger<ConnectorsRegistrationService>>(),
                    firstConnectorConfigPath,
                    secondConnectorConfigPath,
                    provider.GetRequiredService<IConfiguration>(),
                    provider.GetRequiredService<IUtilitiesService>(),
                    provider.GetRequiredService<IExternalConnectionService>()
                )
            );

            var postgresMartenUserName = Environment.GetEnvironmentVariable(envVariablePostgresMartenUserName);
            CheckIfEmpty(postgresMartenUserName, envVariablePostgresMartenUserName);
            var postgresMartenPassword = Environment.GetEnvironmentVariable(envVariablePostgresMartenPassword);
            CheckIfEmpty(postgresMartenPassword, envVariablePostgresMartenPassword);
            var postgresMartenDatabaseName = Environment.GetEnvironmentVariable(envVariablePostgresMartenDatabaseName);
            CheckIfEmpty(postgresMartenDatabaseName, envVariablePostgresMartenDatabaseName);
            var postgresMartenConnectionString = configuration["Postgres:ConnectionString"]
                .Replace("{postgresMartenDatabaseName}", postgresMartenDatabaseName)
                .Replace("{postgresUsername}", postgresMartenUserName)
                .Replace("{postgresPassword}", postgresMartenPassword);
            services.AddSingleton<IDocumentStore>(sp =>
                DocumentStore.For(options =>
                {
                    options.Connection(postgresMartenConnectionString);
                    options.AutoCreateSchemaObjects = AutoCreate.All;
                })
            );

            return services;
        }

        private static void CheckIfEmpty(string? value, string variableName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Environment variable {variableName} is not set");
            }
        }

        private static string GetEnvironmentDestinationString()
        {
            var environmentVariableName = "DebeziumWorker_EnvironmentDestination";
            string? value = Environment.GetEnvironmentVariable(environmentVariableName);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Environment variable {environmentVariableName} is not set. Suggested values are Docker or Online.");
            }
            return value;
        }

        private static bool IsRunningFromIntegrationTests()
        {
            var stackTrace = new StackTrace();
            var frames = stackTrace.GetFrames();
            if (frames == null)
                return false;
            foreach (var frame in frames)
            {
                var method = frame.GetMethod();
                if (method?.DeclaringType?.Assembly.FullName.Contains("IntegrationTests") == true ||
                    method?.DeclaringType?.FullName.Contains("IntegrationTests") == true)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
