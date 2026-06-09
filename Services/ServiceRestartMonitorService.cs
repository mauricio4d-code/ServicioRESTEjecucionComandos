using System.ServiceProcess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using ServicioRESTEjecucionComandos.Data;
using ServicioRESTEjecucionComandos.Models;
using ServicioRESTEjecucionComandos.Repositories;

namespace ServicioRESTEjecucionComandos.Services;

/// <summary>
/// Background service that periodically checks the <c>reiniciar_servicio</c> table
/// in the service database. When the flag is <c>true</c>, it restarts the configured
/// Windows service (respecting a minimum restart margin) and resets the flag.
/// </summary>
public class ServiceRestartMonitorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ServiceRestartMonitorService> _logger;
    private readonly TimeSpan _checkInterval;
    private readonly int _minRestartMarginMinutes;
    private readonly string? _windowsServiceName;

    /// <summary>
    /// Initializes a new instance of ServiceRestartMonitorService.
    /// </summary>
    public ServiceRestartMonitorService(
        IServiceProvider serviceProvider,
        ILogger<ServiceRestartMonitorService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        _checkInterval = TimeSpan.FromMinutes(
            configuration.GetValue<int>("ServiceRestart:CheckIntervalMinutes", 5));
        _minRestartMarginMinutes = configuration.GetValue<int>(
            "ServiceRestart:MinRestartMarginMinutes", 30);
        _windowsServiceName = configuration.GetValue<string>("ServiceRestart:WindowsServiceName");
    }

    /// <summary>
    /// Main execution loop: polls the remote database and triggers restarts when needed.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "ServiceRestartMonitorService started. Interval: {Interval}min, MinMargin: {Margin}min, TargetService: {Service}.",
            _checkInterval.TotalMinutes, _minRestartMarginMinutes,
            string.IsNullOrEmpty(_windowsServiceName) ? "(not configured)" : _windowsServiceName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
                await CheckAndRestartAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during service restart check cycle.");
            }
        }

        _logger.LogInformation("ServiceRestartMonitorService stopped.");
    }

    /// <summary>
    /// Performs a single check cycle: query flag, validate margin, restart, reset flag, log.
    /// </summary>
    private async Task CheckAndRestartAsync(CancellationToken stoppingToken)
    {
        // If no service name configured, skip to save resources
        if (string.IsNullOrWhiteSpace(_windowsServiceName))
        {
            _logger.LogDebug("ServiceRestart: No WindowsServiceName configured. Skipping check.");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var serviceDbContext = scope.ServiceProvider.GetRequiredService<ServiceDbContext>();
        var restartLogRepo = scope.ServiceProvider.GetRequiredService<ServiceRestartLogRepository>();

        // 1. Query the reiniciar_servicio flag
        bool reiniciarFlag;
        try
        {
            string querySql = "SELECT \"id\", \"reiniciar\" FROM reiniciar_servicio";
            var statuses = await serviceDbContext
                .Database
                .SqlQueryRaw<ServicioRESTEjecucionComandos.Models.ServiceRestartStatus>(querySql)
                .ToListAsync(stoppingToken);

            if (!statuses.Any())
            {
                _logger.LogDebug("ServiceRestart: No rows found in reiniciar_servicio table.");
                return;
            }

            reiniciarFlag = statuses.First().Reiniciar;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServiceRestart: Failed to query reiniciar_servicio table.");
            return;
        }

        if (!reiniciarFlag)
        {
            _logger.LogDebug("ServiceRestart: Flag is false. No restart needed.");
            return;
        }

        _logger.LogInformation("ServiceRestart: Flag is true for service '{Service}'. Evaluating restart.", _windowsServiceName);

        // 2. Check minimum restart margin from local SQLite log
        try
        {
            var lastRestart = await restartLogRepo.GetLastRestartAsync(_windowsServiceName);
            if (lastRestart.HasValue)
            {
                var timeSinceLastRestart = (DateTime.UtcNow - lastRestart.Value).TotalMinutes;
                if (timeSinceLastRestart < _minRestartMarginMinutes)
                {
                    _logger.LogWarning(
                        "ServiceRestart: Last restart was {Minutes}min ago (margin: {Margin}min). Skipping restart for '{Service}'.",
                        timeSinceLastRestart, _minRestartMarginMinutes, _windowsServiceName);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServiceRestart: Failed to query last restart time. Proceeding with restart.");
        }

        // 3. Attempt to restart the Windows service
        bool restartSuccess = false;
        try
        {
            using var serviceController = new ServiceController(_windowsServiceName);

            if (!IsServiceRunning(serviceController))
            {
                _logger.LogInformation("ServiceRestart: Service '{Service}' is not running. Starting.", _windowsServiceName);
                serviceController.Start();
                serviceController.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            }
            else
            {
                _logger.LogInformation("ServiceRestart: Restarting service '{Service}'.", _windowsServiceName);
                serviceController.Stop();
                serviceController.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                serviceController.Start();
                serviceController.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            }

            restartSuccess = true;
            _logger.LogInformation("ServiceRestart: Service '{Service}' restarted successfully.", _windowsServiceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServiceRestart: Failed to restart service '{Service}'.", _windowsServiceName);
        }

        // 4. Reset the flag in the remote database
        try
        {
            await serviceDbContext.Database.ExecuteSqlRawAsync(
                "UPDATE reiniciar_servicio SET reiniciar = false", stoppingToken);
            _logger.LogInformation("ServiceRestart: Flag reset to false in reiniciar_servicio table.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServiceRestart: Failed to reset flag in reiniciar_servicio table.");
        }

        // 5. Log the restart event to local SQLite
        try
        {
            var logEntry = new ServiceRestartLog
            {
                ServiceName = _windowsServiceName,
                RestartedAt = DateTime.UtcNow,
                Status = restartSuccess ? "Success" : "Failed"
            };
            await restartLogRepo.SaveRestartAsync(logEntry);
            _logger.LogInformation("ServiceRestart: Restart event logged locally (Status: {Status}).", logEntry.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServiceRestart: Failed to save restart log entry.");
        }
    }

    /// <summary>
    /// Determines whether the specified service is currently running.
    /// </summary>
    private static bool IsServiceRunning(ServiceController serviceController)
    {
        try
        {
            return serviceController.Status == ServiceControllerStatus.Running;
        }
        catch
        {
            return false;
        }
    }
}
