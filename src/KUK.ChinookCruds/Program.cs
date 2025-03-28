using AutoMapper;
using KUK.ChinookCruds;
using KUK.ChinookCruds.Debezium;
using KUK.Common;
using KUK.Common.Contexts;
using KUK.Common.Services;
using KUK.ManagementServices.Services;
using KUK.ManagementServices.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

var firstEvnPartEnvironmentVariableName = "DebeziumWorker_EnvironmentDestination";
string firstEnvPart = Environment.GetEnvironmentVariable(firstEvnPartEnvironmentVariableName);
if (string.IsNullOrWhiteSpace(firstEnvPart)) throw new ArgumentException($"Variable {firstEnvPart} is not set in environment variables.");
string secondEnvPart = Environment.GetEnvironmentVariable($"DebeziumWorker_{firstEnvPart}_Environment");
if (string.IsNullOrWhiteSpace(secondEnvPart)) throw new ArgumentException($"Variable {secondEnvPart} is not set in environment variables.");
string dynamicEnvironment = $"{firstEnvPart}.{secondEnvPart}";
builder.Environment.EnvironmentName = dynamicEnvironment;

builder.Host.ConfigureAppConfiguration((hostingContext, config) =>
{
    config.AddJsonFile($"appsettings.{dynamicEnvironment}.json", optional: true, reloadOnChange: true);
});

// Register AppSettingsConfig
builder.Services.AddAppSettingsConfig(builder.Configuration);

// Add services to the container
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<ISchemaInitializerService, SchemaInitializerService>();
builder.Services.AddSingleton<IContainersHealthCheckService, ContainersHealthCheckService>();
builder.Services.AddScoped<IQuickActionsService, QuickActionsService>();
builder.Services.AddScoped<IDatabaseOperationsService, DatabaseOperationsService>();
builder.Services.AddSingleton<IDatabaseDumpService, DatabaseDumpService>();
builder.Services.AddSingleton<DebeziumConfigService>();
builder.Services.AddSingleton<IUtilitiesService, UtilitiesService>();
builder.Services.AddSingleton<IExternalConnectionService, ExternalConnectionService>();
builder.Services.AddSingleton<IDatabaseDirectConnectionService, DatabaseDirectConnectionService>();
builder.Services.AddSingleton<IKafkaService, KafkaService>();
builder.Services.AddHttpClient(); // required to get status of connectors

// Register database contexts using AppSettingsConfig
var appSettingsConfig = builder.Services.BuildServiceProvider().GetService<AppSettingsConfig>();
builder.Services.AddDatabaseContexts(appSettingsConfig);

// Register ConnectorsRegistrationService and ProcessorService with AppSettingsConfig
builder.Services.AddSingleton<IConnectorsRegistrationService>(provider =>
    new ConnectorsRegistrationService(
        provider.GetRequiredService<HttpClient>(),
        provider.GetRequiredService<ILogger<ConnectorsRegistrationService>>(),
        appSettingsConfig,
        provider.GetRequiredService<IConfiguration>(),
        provider.GetRequiredService<IUtilitiesService>(),
        provider.GetRequiredService<IExternalConnectionService>()));

builder.Services.AddSingleton<IProcessorService>(provider =>
    new ProcessorService(
        provider.GetRequiredService<ILogger<ProcessorService>>(),
        appSettingsConfig,
        provider.GetRequiredService<HttpClient>()));

// Register DatabaseManagementService
builder.Services.AddSingleton<IDatabaseManagementService>(provider =>
    new DatabaseManagementService(appSettingsConfig, provider.GetRequiredService<ILogger<DatabaseManagementService>>()));
builder.Services.AddSingleton<IDockerService>(provider =>
    new DockerService(appSettingsConfig, provider.GetRequiredService<ILogger<DockerService>>(), provider.GetRequiredService<IUtilitiesService>()));
builder.Services.AddScoped<ISchemaInitializerService>(provider =>
    new SchemaInitializerService(
        provider.GetRequiredService<IChinook1DataChangesContext>(),
        provider.GetRequiredService<IChinook1RootContext>(),
        provider.GetRequiredService<IChinook2Context>(),
        provider.GetRequiredService<ILogger<SchemaInitializerService>>(),
        appSettingsConfig,
        provider.GetRequiredService<IDockerService>(),
        provider.GetRequiredService<IDatabaseManagementService>(),
        provider.GetRequiredService<IDatabaseDumpService>(),
        provider.GetRequiredService<IUtilitiesService>(),
        provider.GetRequiredService<IConfiguration>(),
        provider.GetRequiredService<IDatabaseDirectConnectionService>()
        ));

// Register AutoMapper
var mapperConfig = new MapperConfiguration(mc =>
{
    mc.AddProfile(new MappingProfile());
});
IMapper mapper = mapperConfig.CreateMapper();
builder.Services.AddSingleton(mapper);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
