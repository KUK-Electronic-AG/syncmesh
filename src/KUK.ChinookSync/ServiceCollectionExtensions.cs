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
using Weasel.Core;

namespace KUK.ChinookSync
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Required to be able to start processor from ChinookCrudsWebApp by pressing button
        /// </summary>
        public static IServiceCollection AddChinookSyncServices(this IServiceCollection services, 
            string oldDatabaseConnectionString, 
            string newDatabaseConnectionString,
            IConfiguration configuration)
        {
            services.AddMemoryCache();
            
            if (!double.TryParse(configuration["InternalKafkaProcessorParameters:MemoryCacheExpirationInSeconds"], out double memoryCacheExpiration) 
                || memoryCacheExpiration <= 0)
            {
                configuration["InternalKafkaProcessorParameters:MemoryCacheExpirationInSeconds"] = "60";
            }
            
            // Registration fo database contexts
            services.AddDbContext<Chinook1DataChangesContext>(options =>
                options.UseMySQL(oldDatabaseConnectionString));

            services.AddDbContext<Chinook2Context>(options =>
                options.UseNpgsql(newDatabaseConnectionString,
                    x => x.MigrationsAssembly("KUK.ChinookSync")));

            services.Add(new ServiceDescriptor(typeof(IOldDataChangesContext),
                provider => provider.GetService<Chinook1DataChangesContext>(), ServiceLifetime.Scoped));

            // Registration of context factories
            services.AddDbContextFactory<Chinook1DataChangesContext>(options =>
                options.UseMySQL(oldDatabaseConnectionString), ServiceLifetime.Transient);

            services.AddDbContextFactory<Chinook2Context>(options =>
                options.UseNpgsql(newDatabaseConnectionString,
                    x => x.MigrationsAssembly("KUK.ChinookSync")), 
                ServiceLifetime.Transient);

            // Registration of Marten Document Store
            string environmentDestination = GetEnvironmentDestinationString();
            string envVariablePostgresMartenUserName = $"DebeziumWorker_{environmentDestination}_PostgresMartenRootUserName";
            string envVariablePostgresMartenPassword = $"DebeziumWorker_{environmentDestination}_PostgresMartenRootPassword";
            string envVariablePostgresMartenDatabaseName = $"DebeziumWorker_{environmentDestination}_PostgresMartenDatabaseName";

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

            // Main services
            services.AddScoped<IInitializationService, InitializationService>();
            services.AddScoped<IRunnerService, RunnerService>();
            services.AddScoped<IProcessingService, ProcessingService>();
            services.AddScoped<IConnectorsService, ConnectorsService>();
            
            // Factories and domain services
            services.AddScoped<IEventCommandFactory, EventCommandFactory>();
            services.AddScoped<ICustomerService, CustomerService>();
            services.AddScoped<IInvoiceService, InvoiceService>();
            services.AddScoped<IInvoiceLineService, InvoiceLineService>();
            services.AddScoped<IAddressService, AddressService>();
            services.AddScoped<IDomainDependencyService, DomainDependencyService>();
            
            // Data migration
            services.AddScoped<IDataMigrationRunner<Chinook1DataChangesContext, Chinook2Context>, 
                DataMigrationRunner<Chinook1DataChangesContext, Chinook2Context>>();
            services.AddScoped<ICustomTriggersCreationService<Chinook1DataChangesContext, Chinook2Context>, 
                CustomTriggersCreationService<Chinook1DataChangesContext, Chinook2Context>>();
            services.AddScoped<ICustomSchemaInitializerService, CustomSchemaInitializerService>();
            services.AddScoped<ITriggersCreationService<Chinook1DataChangesContext, Chinook2Context>, 
                TriggersCreationService<Chinook1DataChangesContext, Chinook2Context>>();
            services.AddScoped<IDatabaseMigrator<Chinook1DataChangesContext, Chinook2Context>, 
                DatabaseMigrator<Chinook1DataChangesContext, Chinook2Context>>();
            services.AddScoped<DatabaseMigrator<Chinook1DataChangesContext, Chinook2Context>>();
            
            // Auxiliary services
            services.AddScoped<IUniqueIdentifiersService, UniqueIdentifiersService>();
            services.AddScoped<IDatabaseEventProcessorService, DatabaseEventProcessorService>();
            services.AddSingleton<IUtilitiesService>(provider => new UtilitiesService(provider));
            services.AddSingleton<IRetryHelper, RetryHelper>();
            services.AddSingleton<IExternalConnectionService, ExternalConnectionService>();
            services.AddSingleton<IDatabaseDirectConnectionService, DatabaseDirectConnectionService>();
            services.AddScoped<IEventsSortingService, EventsSortingService>();
            services.AddSingleton<GlobalState>();
            
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
    }
} 