using KUK.ChinookCruds.Debezium;
using KUK.ChinookCruds.Models;
using KUK.ChinookCruds.Utilities;
using KUK.ChinookCruds.ViewModels;
using KUK.Common;
using KUK.Common.Services;
using KUK.ManagementServices.Services.Interfaces;
using KUK.ManagementServices.Utilities;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IDockerService _dockerService;
    private readonly AppSettingsConfig _appSettingsConfig;
    private readonly ISchemaInitializerService _schemaInitializerService;
    private readonly DebeziumConfigService _debeziumConfigService;
    private readonly HttpClient _httpClient;
    private readonly IDatabaseManagementService _databaseManagementService;
    private readonly IProcessorService _processorService;
    private readonly IQuickActionsService _quickActionsService;
    private readonly IDatabaseOperationsService _databaseOperationsService;
    private readonly IUtilitiesService _utilitiesService;
    private readonly IExternalConnectionService _externalConnectionService;

    private static Process _processor;
    private string _processorExeFullPath;
    private string _processorExePath;
    private string _processorLogsFullPath;
    private string _processorAppSettingsFullPath;
    private string _firstConnectorFullPath;
    private string _secondConnectorFullPath;

    public HomeController(
        ILogger<HomeController> logger,
        IDockerService dockerService,
        AppSettingsConfig appSettingsConfig,
        ISchemaInitializerService schemaInitializerService,
        DebeziumConfigService configService,
        HttpClient httpClient,
        IDatabaseManagementService databaseManagementService,
        IProcessorService processorService,
        IQuickActionsService quickActionsService,
        IDatabaseOperationsService databaseOperationsService,
        IUtilitiesService utilitiesService,
        IExternalConnectionService externalConnectionService)
    {
        _logger = logger;
        _dockerService = dockerService;
        _appSettingsConfig = appSettingsConfig;
        _schemaInitializerService = schemaInitializerService;
        _debeziumConfigService = configService;
        _httpClient = httpClient;
        _databaseManagementService = databaseManagementService;
        _processorService = processorService;
        _quickActionsService = quickActionsService;
        _utilitiesService = utilitiesService;
        _externalConnectionService = externalConnectionService;

        _processorExeFullPath = _appSettingsConfig.ProcessorExeFullPath;
        _processorExePath = _appSettingsConfig.ProcessorExePath;
        _processorLogsFullPath = _appSettingsConfig.ProcessorLogsFullPath;
        _processorAppSettingsFullPath = _appSettingsConfig.ProcessorAppSettingsFullPath;
        _firstConnectorFullPath = _appSettingsConfig.FirstConnectorJsonPath;
        _secondConnectorFullPath = _appSettingsConfig.SecondConnectorJsonPath;
        _databaseOperationsService = databaseOperationsService;
    }

    [Route("Home/test")]
    public IActionResult Test()
    {
        return Content("HomeController is available");
    }

    public IActionResult Index()
    {
        var config1 = _debeziumConfigService.GetConfig1();
        var config2 = _debeziumConfigService.GetConfig2();
        var displayParameters = _debeziumConfigService.GetDisplayParameters();
        var model = new DebeziumConfigsViewModel
        {
            Config1 = config1,
            Config2 = config2,
            DisplayParameters = displayParameters,
            IsDockerMode = _utilitiesService.GetOnlineOrDockerMode() == ApplicationDestinationMode.Docker
        };
        return View(model);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [Route("Home/QuickDelete")]
    [HttpPost]
    public async Task<IActionResult> QuickDelete()
    {
        try
        {
            ServiceActionStatus status = await _quickActionsService.QuickDelete();
            if (status.Success)
            {
                return Ok(new { message = status.Message, success = true });
            }
            else
            {
                return StatusCode(500, new { message = status.Message, success = false });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"An error occurred while doing quick delete: {ex.Message}", success = false });
        }
    }

    [Route("Home/QuickStartup")]
    [HttpPost]
    public async Task<IActionResult> QuickStartup()
    {
        try
        {
            ServiceActionStatus status = await _quickActionsService.QuickStartup();
            if (status.Success)
            {
                return Ok(new { message = status.Message, success = true });
            }
            else
            {
                return StatusCode(500, new { message = status.Message, success = false });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"An error occurred while doing quick startup: {ex.Message}", success = false });
        }
    }

    [Route("Home/CreateOldCustomer")]
    [HttpPost]
    public async Task<IActionResult> CreateOldCustomer()
    {
        try
        {
            ServiceActionStatus status = await _quickActionsService.QuickCreateOldCustomer();
            if (status.Success)
            {
                return Ok(new { message = status.Message, success = true });
            }
            else
            {
                return StatusCode(500, new { message = status.Message, success = false });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"An error occurred while creating old customer: {ex.Message}", success = false });
        }
    }

    [Route("Home/CreateOldInvoice")]
    [HttpPost]
    public async Task<IActionResult> CreateOldInvoice()
    {
        try
        {
            ServiceActionStatus status = await _quickActionsService.QuickCreateOldInvoice();
            if (status.Success)
            {
                return Ok(new { message = status.Message, success = true });
            }
            else
            {
                return StatusCode(500, new { message = status.Message, success = false });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"An error occurred while creating old invoice: {ex.Message}", success = false });
        }
    }

    [Route("Home/AddColumnToCustomerTable")]
    [HttpPost]
    public async Task<IActionResult> AddColumnToCustomerTable()
    {
        try
        {
            ServiceActionStatus status = await _databaseOperationsService.AddColumnToCustomerTableInOldDatabase();
            if (status.Success)
            {
                return Ok(new { message = status.Message, success = true });
            }
            else
            {
                return StatusCode(500, new { message = status.Message, success = false });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"An error occurred while creating old invoice: {ex.Message}", success = false });
        }
    }

    [Route("Home/AddNewCustomerUsingSubquery")]
    [HttpPost]
    public async Task<IActionResult> AddNewCustomerUsingSubquery()
    {
        try
        {
            ServiceActionStatus status = await _databaseOperationsService.AddNewCustomerUsingSubquery();
            if (status.Success)
            {
                return Ok(new { message = status.Message, success = true });
            }
            else
            {
                return StatusCode(500, new { message = status.Message, success = false });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"An error occurred while creating new customer in old database using subquery: {ex.Message}", success = false });
        }
    }

    [Route("Home/AddNewInvoiceUsingNestedQuery")]
    [HttpPost]
    public async Task<IActionResult> AddNewInvoiceUsingNestedQuery()
    {
        try
        {
            ServiceActionStatus status = await _databaseOperationsService.AddNewInvoiceUsingNestedQuery();
            if (status.Success)
            {
                return Ok(new { message = status.Message, success = true });
            }
            else
            {
                return StatusCode(500, new { message = status.Message, success = false });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"An error occurred while creating new invoice in old database using nested query: {ex.Message}", success = false });
        }
    }

    [Route("Home/CreateNewCustomer")]
    [HttpPost]
    public async Task<IActionResult> CreateNewCustomer()
    {
        try
        {
            ServiceActionStatus status = await _quickActionsService.QuickCreateNewCustomer();
            if (status.Success)
            {
                return Ok(new { message = status.Message, success = true });
            }
            else
            {
                return StatusCode(500, new { message = status.Message, success = false });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"An error occurred while creating new customer: {ex.Message}", success = false });
        }
    }

    [Route("Home/CreateNewInvoice")]
    [HttpPost]
    public async Task<IActionResult> CreateNewInvoice()
    {
        try
        {
            ServiceActionStatus status = await _quickActionsService.QuickCreateNewInvoice();
            if (status.Success)
            {
                return Ok(new { message = status.Message, success = true });
            }
            else
            {
                return StatusCode(500, new { message = status.Message, success = false });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"An error occurred while creating new invoice: {ex.Message}", success = false });
        }
    }

    [Route("Home/CreateNewAddress")]
    [HttpPost]
    public async Task<IActionResult> CreateNewAddress()
    {
        try
        {
            ServiceActionStatus status = await _quickActionsService.QuickCreateNewAddress();
            if (status.Success)
            {
                return Ok(new { message = status.Message, success = true });
            }
            else
            {
                return StatusCode(500, new { message = status.Message, success = false });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"An error occurred while creating new address: {ex.Message}", success = false });
        }
    }


    [Route("Home/start/{containerName}")]
    [HttpPost]
    public async Task<IActionResult> StartContainer(string containerName)
    {
        var message = await _dockerService.StartContainerAsync(containerName);
        return Json(new { message });
    }

    [Route("Home/stop/{containerName}")]
    [HttpPost]
    public async Task<IActionResult> StopContainer(string containerName)
    {
        var message = await _dockerService.StopContainerAsync(containerName);
        return Json(new { message });
    }

    [Route("Home/remove/{containerName}")]
    [HttpPost]
    public async Task<IActionResult> RemoveContainer(string containerName)
    {
        var message = await _dockerService.RemoveContainerAsync(containerName);
        return Json(new { message });
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetContainerStatus()
    {
        var statuses = await _dockerService.GetContainerStatusesAsync(new[] { "kafka", "schema-registry", "connect", "control-center", "mysql80", "destinationpostgres", "martenpostgres", "debezium" });
        return Json(statuses);
    }

    [HttpGet("dbstatus")]
    public async Task<IActionResult> GetDatabaseStatus()
    {
        var dbConfig = new DatabasesBasicConfiguration()
        {
            RootOldDbConnectionString = _appSettingsConfig.RootOldDatabaseConnectionString,
            OldDbConnectionString = _appSettingsConfig.OldDatabaseConnectionString,
            NewDbConnectionString = _appSettingsConfig.NewDatabaseConnectionString,
            OldDbName = _appSettingsConfig.OldDatabaseName,
            NewDbName = _appSettingsConfig.NewDatabaseName,
            OldDbPort = _appSettingsConfig.OldDatabasePort,
            NewDbPort = _appSettingsConfig.NewDatabasePort,
        };

        DbStatuses databasesStatus = await _databaseManagementService.CheckDatabasesStatus(dbConfig);

        // REMARK: We may eliminate the need to use this auxiliary mapping class and use DbStatuses databasesStatus directly
        var dbStatuses = new
        {
            OldDbStatus = databasesStatus.OldDbStatus == DbStatusEnum.Connected ? "Connected" : "Disconnected",
            NewDbStatus = databasesStatus.NewDbStatus == DbStatusEnum.Connected ? "Connected" : "Disconnected",
            OldSchemaExists = databasesStatus.OldSchemaExists == CheckStatusEnum.Exists ? "True" : "False",
            NewSchemaExists = databasesStatus.NewSchemaExists == CheckStatusEnum.Exists ? "True" : "False",
            OldSchemaHasData = databasesStatus.OldSchemaHasData == CheckStatusEnum.Exists ? "True" : "False",
            NewSchemaHasData = databasesStatus.NewSchemaHasData == CheckStatusEnum.Exists ? "True" : "False",
        };

        return Json(dbStatuses);
    }

    [HttpPost]
    [Route("initialize-schema")]
    public IActionResult InitializeSchema([FromForm] string database)
    {
        if (!DatabaseEnumHelper.TryParseDatabase(database, out WhichDatabaseEnum dbEnum))
        {
            return StatusCode(400, new { message = $"Invalid database value: {database}", success = false });
        }

        var asRoot = false;
        if (dbEnum == WhichDatabaseEnum.OldDatabase)
        {
            asRoot = true;
        }

        ServiceActionStatus status = ExecuteSchemaAction(
            _schemaInitializerService.InitializeSchema,
            dbEnum, asRoot, "Schema initialized successfully.");

        if (status.Success)
        {
            return Ok(new { message = status.Message, success = true });
        }
        else
        {
            return StatusCode(500, new { message = status.Message, success = false });
        }
    }

    [HttpPost]
    [Route("initialize-schema-and-tables")]
    public IActionResult InitializeSchemaAndTables([FromForm] string database)
    {
        if (!DatabaseEnumHelper.TryParseDatabase(database, out WhichDatabaseEnum dbEnum))
        {
            return StatusCode(400, new { message = $"Invalid database value: {database}", success = false });
        }

        var asRoot = false;
        if (dbEnum == WhichDatabaseEnum.OldDatabase)
        {
            asRoot = true;
        }

        ServiceActionStatus status = ExecuteSchemaAction(
            _schemaInitializerService.InitializeSchemaAndTables,
            dbEnum, asRoot, "Schema and tables initialized successfully.");

        if (status.Success)
        {
            return Ok(new { message = status.Message, success = true });
        }
        else
        {
            return StatusCode(500, new { message = status.Message, success = false });
        }
    }

    [HttpPost]
    [Route("initialize-schema-tables-and-data")]
    public async Task<IActionResult> InitializeSchemaTablesAndData([FromForm] string database)
    {
        if (!DatabaseEnumHelper.TryParseDatabase(database, out WhichDatabaseEnum dbEnum))
        {
            return BadRequest(new { message = $"Invalid database value: {database}", success = false });
        }

        try
        {
            var mode = _utilitiesService.GetOnlineOrDockerMode();
            ServiceActionStatus status;
            switch (mode)
            {
                case ApplicationDestinationMode.Docker:
                    status = await _schemaInitializerService.InitializeSchemaTablesAndData(dbEnum, "root");
                    break;
                case ApplicationDestinationMode.Online:
                    status = await _schemaInitializerService.InitializeSchemaTablesAndData(dbEnum);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown mode {mode}");
            }

            if (status.Success)
            {
                return Ok(new { message = status.Message, success = true });
            }
            else
            {
                return StatusCode(500, new { message = status.Message, success = false });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Unhandled exception: {ex.Message}", success = false });
        }
    }

    [HttpPost]
    [Route("delete-schema")]
    public IActionResult DeleteSchema([FromForm] string database)
    {
        if (!DatabaseEnumHelper.TryParseDatabase(database, out WhichDatabaseEnum dbEnum))
        {
            return StatusCode(400, new { message = $"Invalid database value: {database}", success = false });
        }

        var asRoot = false;
        if (dbEnum == WhichDatabaseEnum.OldDatabase)
        {
            asRoot = true;
        }

        ServiceActionStatus status = ExecuteSchemaAction(
            _schemaInitializerService.DeleteSchema,
            dbEnum, asRoot, "Schema deleted successfully.");

        if (status.Success)
        {
            return Ok(new { message = status.Message, success = true });
        }
        else
        {
            return StatusCode(500, new { message = status.Message, success = false });
        }
    }

    [HttpPost]
    [Route("delete-tables")]
    public IActionResult DeleteTables([FromForm] string database)
    {
        if (!DatabaseEnumHelper.TryParseDatabase(database, out WhichDatabaseEnum dbEnum))
        {
            return StatusCode(400, new { message = $"Invalid database value: {database}", success = false });
        }

        var asRoot = false;
        if (dbEnum == WhichDatabaseEnum.OldDatabase)
        {
            asRoot = true;
        }

        ServiceActionStatus status = ExecuteSchemaAction(
            _schemaInitializerService.DeleteTables,
            dbEnum, asRoot, "Tables deleted successfully.");

        if (status.Success)
        {
            return Ok(new { message = status.Message, success = true });
        }
        else
        {
            return StatusCode(500, new { message = status.Message, success = false });
        }
    }

    [HttpPost]
    [Route("delete-data")]
    public IActionResult DeleteData([FromForm] string database)
    {
        if (!DatabaseEnumHelper.TryParseDatabase(database, out WhichDatabaseEnum dbEnum))
        {
            return StatusCode(400, new { message = $"Invalid database value: {database}", success = false });
        }

        var asRoot = false;
        if (dbEnum == WhichDatabaseEnum.OldDatabase)
        {
            asRoot = true;
        }

        ServiceActionStatus status = ExecuteSchemaAction(
            _schemaInitializerService.DeleteData,
            dbEnum, asRoot, "Data deleted successfully.");

        if (status.Success)
        {
            return Ok(new { message = status.Message, success = true });
        }
        else
        {
            return StatusCode(500, new { message = status.Message, success = false });
        }
    }

    [HttpPost]
    [Route("Home/delete-processor-logs")]
    public async Task<IActionResult> DeleteProcessorLogs()
    {
        var isRunning = await _processorService.DeleteProcessorLogs();
        if (isRunning)
        {
            return Ok(new { message = "Processor logs deleted successfully.", isRunning = true });
        }
        else
        {
            return StatusCode(500, new { message = "Error deleting processor logs.", isRunning = false });
        }
    }

    [HttpPost]
    [Route("Home/start-processor")]
    public async Task<IActionResult> StartProcessor()
    {
        var isRunning = await _processorService.StartProcessorAsync();
        if (isRunning)
        {
            return Ok(new { message = "Processor started successfully.", isRunning = true });
        }
        else
        {
            return StatusCode(500, new { message = "Error starting processor.", isRunning = false });
        }
    }

    [HttpPost]
    [Route("Home/stop-processor")]
    public IActionResult StopProcessor()
    {
        try
        {
            if (_processor != null && !_processor.HasExited)
            {
                _processor.CloseMainWindow();
                _processor.WaitForExit();
                return Ok(new { message = "Processor stopped successfully.", isRunning = false });
            }
            return Ok(new { message = "Processor is not running.", isRunning = false });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error stopping processor: {ex.Message}");
            return StatusCode(500, new { message = $"Error stopping processor: {ex.Message}", isRunning = false });
        }
    }

    [HttpPost]
    [Route("Home/terminate-processor")]
    public IActionResult TerminateProcessor()
    {
        try
        {
            if (_processor != null && !_processor.HasExited)
            {
                _processor.Kill();
                return Ok(new { message = "Processor terminated successfully.", isRunning = false });
            }
            return Ok(new { message = "Processor is not running.", isRunning = false });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error terminating processor: {ex.Message}");
            return StatusCode(500, new { message = $"Error terminating processor: {ex.Message}", isRunning = false });
        }
    }

    [HttpGet]
    [Route("Home/get-processor-error-count")]
    public IActionResult GetProcessorErrorCount()
    {
        var errorCount = 0;

        if (System.IO.File.Exists(_processorLogsFullPath))
        {
            try
            {
                using (var fileStream = new FileStream(_processorLogsFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var streamReader = new StreamReader(fileStream))
                {
                    var logLines = new List<string>();
                    string line;
                    while ((line = streamReader.ReadLine()) != null)
                    {
                        logLines.Add(line);
                    }
                    errorCount = logLines.Count(l => l.Contains("ERROR"));
                }
            }
            catch (IOException ex)
            {
                _logger.LogError($"Error reading log file: {ex.Message}");
                return StatusCode(500, new { message = $"Error reading log file: {ex.Message}" });
            }
        }

        var result = new { ErrorCount = errorCount };
        return Ok(result);
    }

    [HttpGet]
    [Route("Home/check-processor-status")]
    public IActionResult CheckProcessorStatus()
    {
        // REMARK: If it's running from the inside, it will also be visible outside so this logic cannot
        // help us distinguish between processor running once (either inside or outside) or running twice.

        bool isSpawned = _processor != null && !_processor.HasExited;
        string processName = "KUK.KafkaProcessor";
        bool isRunningOutside = Process.GetProcessesByName(processName).Any();

        ProcessorStatusEnum status = (isSpawned, isRunningOutside) switch
        {
            (true, true) => ProcessorStatusEnum.IsRunningTwice,
            (true, false) => ProcessorStatusEnum.IsSpawned,
            (false, true) => ProcessorStatusEnum.IsRunningOutside,
            _ => ProcessorStatusEnum.IsNotRunning
        };

        return Ok(new { status = status.ToString() });
    }

    [HttpPost]
    [Route("Home/clear-processor-logs")]
    public IActionResult ClearLogs()
    {
        try
        {
            if (System.IO.File.Exists(_processorLogsFullPath))
            {
                using (var fileStream = new FileStream(_processorLogsFullPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Delete))
                {
                    // Close file handle
                }
                System.IO.File.Delete(_processorLogsFullPath);
                return Ok(new { message = "Logs cleared successfully." });
            }
            return Ok(new { message = "Log file does not exist." });
        }
        catch (IOException ex) when ((ex.HResult & 0x0000FFFF) == 32) // ERROR_SHARING_VIOLATION
        {
            _logger.LogError($"Error clearing logs: {ex.Message}");
            return StatusCode(500, new { message = "The log file is currently in use by another process. Please try again later." });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error clearing logs: {ex.Message}");
            return StatusCode(500, new { message = $"Error clearing logs: {ex.Message}" });
        }
    }

    [HttpPost]
    [Route("Home/register-connector")]
    public async Task<IActionResult> RegisterConnector(string configPath)
    {
        try
        {
            var mode = _utilitiesService.GetOnlineOrDockerMode();
            if (mode == ApplicationDestinationMode.Online)
            {
                await _externalConnectionService.RegisterConnectorAsync(configPath);
            }
            else if (mode == ApplicationDestinationMode.Docker)
            {
                switch (configPath)
                {
                    case "old-to-new-connector":
                        configPath = _firstConnectorFullPath;
                        break;
                    case "new-to-old-connector":
                        configPath = _secondConnectorFullPath;
                        break;
                    default:
                        break;
                }

                var configJson = System.IO.File.ReadAllText(configPath);
                var content = new StringContent(configJson, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("http://localhost:8084/connectors", content);
                response.EnsureSuccessStatusCode();
            }

            return Ok(new { message = "Connector registered successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error registering connector: {ex.Message}");
            return StatusCode(500, new { message = $"Error registering connector: {ex.Message}" });
        }
    }

    [HttpPost]
    [Route("Home/delete-connector")]
    public async Task<IActionResult> DeleteConnector(string connectorName)
    {
        try
        {
            var mode = _utilitiesService.GetOnlineOrDockerMode();
            if (mode == ApplicationDestinationMode.Online)
            {
                await _externalConnectionService.DeleteConnectorsAsync(connectorName);
            }
            else if (mode == ApplicationDestinationMode.Docker)
            {
                var response = await _httpClient.DeleteAsync($"http://localhost:8084/connectors/{connectorName}");
                response.EnsureSuccessStatusCode();
            }

            return Ok(new { message = "Connector deleted successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error deleting connector: {ex.Message}");
            return StatusCode(500, new { message = $"Error deleting connector: {ex.Message}" });
        }
    }

    [HttpPost]
    [Route("Home/restart-connector")]
    public async Task<IActionResult> RestartConnector(string connectorName)
    {
        try
        {
            var mode = _utilitiesService.GetOnlineOrDockerMode();
            if (mode == ApplicationDestinationMode.Online)
            {
                await _externalConnectionService.RestartConnectorAsync(connectorName);
            }
            else if (mode == ApplicationDestinationMode.Docker)
            {
                var response = await _httpClient.PostAsync($"http://localhost:8084/connectors/{connectorName}/restart", null);
                response.EnsureSuccessStatusCode();
            }

            return Ok(new { message = "Connector restarted successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error restarting connector: {ex.Message}");
            return StatusCode(500, new { message = $"Error restarting connector: {ex.Message}" });
        }
    }

    [HttpGet]
    [Route("Home/get-connector-status")]
    public async Task<IActionResult> GetConnectorStatus(string connectorName)
    {
        try
        {
            var mode = _utilitiesService.GetOnlineOrDockerMode();
            if (mode == ApplicationDestinationMode.Online)
            {
                var status = await _externalConnectionService.GetConnectorStatusAsync(connectorName);
                return Ok(status);
            }
            else if (mode == ApplicationDestinationMode.Docker)
            {
                var response = await _httpClient.GetAsync($"http://localhost:8084/connectors/{connectorName}/status");
                response.EnsureSuccessStatusCode();
                var statusJson = await response.Content.ReadAsStringAsync();
                return Ok(statusJson);
            }
            else
            {
                throw new InvalidOperationException($"Unknown mode {mode}");
            }
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(500, new { message = $"Error getting connector status: {ex.Message}", state = "ERROR" });
        }
    }

    private ServiceActionStatus ExecuteSchemaAction(
        Func<WhichDatabaseEnum, bool, ServiceActionStatus> action,
        WhichDatabaseEnum database,
        bool asRoot,
        string successMessage)
    {
        try
        {
            var status = action(database, asRoot);
            if (status.Success)
            {
                return new ServiceActionStatus { Success = true, Message = successMessage };
            }
            else
            {
                return status;
            }
        }
        catch (Exception ex)
        {
            return new ServiceActionStatus { Success = false, Message = $"Unhandled exception: {ex.Message}" };
        }
    }

    private async Task<ServiceActionStatus> ExecuteSchemaAction(
        Func<WhichDatabaseEnum, bool, Task<ServiceActionStatus>> action,
        WhichDatabaseEnum database,
        bool asRoot,
        string successMessage)
    {
        try
        {
            var status = await action(database, asRoot);
            if (status.Success)
            {
                return new ServiceActionStatus { Success = true, Message = successMessage };
            }
            else
            {
                return status;
            }
        }
        catch (Exception ex)
        {
            return new ServiceActionStatus { Success = false, Message = $"Unhandled exception: {ex.Message}" };
        }
    }

    private async Task<ServiceActionStatus> ExecuteSchemaAction(
        Func<WhichDatabaseEnum, Task<ServiceActionStatus>> action,
        WhichDatabaseEnum database,
        string successMessage)
    {
        try
        {
            var status = await action(database);
            if (status.Success)
            {
                return new ServiceActionStatus { Success = true, Message = successMessage };
            }
            else
            {
                return status;
            }
        }
        catch (Exception ex)
        {
            return new ServiceActionStatus { Success = false, Message = $"Unhandled exception: {ex.Message}" };
        }
    }

    private ServiceActionStatus ExecuteInitializeSchemaAction(WhichDatabaseEnum database, string successMessage)
    {
        try
        {
            var status = _schemaInitializerService.InitializeSchema(database);
            if (status.Success)
            {
                return new ServiceActionStatus { Success = true, Message = successMessage };
            }
            else
            {
                return status;
            }
        }
        catch (Exception ex)
        {
            return new ServiceActionStatus { Success = false, Message = $"Unhandled exception: {ex.Message}" };
        }
    }
}
