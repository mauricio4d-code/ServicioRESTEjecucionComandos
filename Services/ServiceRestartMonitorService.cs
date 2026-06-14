using System.ServiceProcess;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using ServicioRESTEjecucionComandos.Constants;
using ServicioRESTEjecucionComandos.Data;
using ServicioRESTEjecucionComandos.Models;
using ServicioRESTEjecucionComandos.Repositories;

namespace ServicioRESTEjecucionComandos.Services;

/// <summary>
/// Background service that periodically checks the <c>dtx_process</c> table
/// in the service database for long-running processes. When a RUNNING process
/// exceeds the configured <c>RunningMinutesThreshold</c>, it restarts the
/// configured Windows service and updates the process status in <c>dtx_process</c>.
/// </summary>
public class ServiceRestartMonitorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ServiceRestartMonitorService> _logger;
    private readonly TimeSpan _checkInterval;
    private readonly TimeSpan _runningMinutesThreshold;
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
        _runningMinutesThreshold = TimeSpan.FromMinutes(
            configuration.GetValue<int>("ServiceRestart:RunningMinutesThreshold", 30));
        _windowsServiceName = configuration.GetValue<string>("ServiceRestart:WindowsServiceName");
    }

    /// <summary>
    /// Main execution loop: polls the dtx_process table and triggers restarts when needed.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "ServiceRestartMonitorService started. Interval: {Interval}min, RunningThreshold: {Threshold}min, TargetService: {Service}.",
            _checkInterval.TotalMinutes, _runningMinutesThreshold.TotalMinutes,
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
    /// Performs a single check cycle: query dtx_process for long-running processes,
    /// validate conditions, restart the Windows service, and update process status.
    /// </summary>
    private async Task CheckAndRestartAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ServiceRestart: Starting dtx_process check cycle for service '{Service}'.", _windowsServiceName);

        // If no service name configured, skip to save resources
        if (string.IsNullOrWhiteSpace(_windowsServiceName))
        {
            _logger.LogInformation("ServiceRestart: No WindowsServiceName configured. Skipping check.");
            return;
        }

        // ServiceController is Windows-only; skip gracefully on non-Windows platforms
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _logger.LogWarning("ServiceRestart: ServiceController API is not available on non-Windows platforms. Skipping restart check.");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var dtxProcessRepo = scope.ServiceProvider.GetRequiredService<DtxProcessRepository>();

        // 1. Query dtx_process for RUNNING process IDs older than the threshold
        List<long> longRunningProcessIds;
        try
        {
            _logger.LogInformation("ServiceRestart: Querying dtx_process for RUNNING processes older than {Threshold}min.", _runningMinutesThreshold.TotalMinutes);
            longRunningProcessIds = await dtxProcessRepo.GetRunningProcessIdsOlderThanAsync(_runningMinutesThreshold);
            _logger.LogInformation("ServiceRestart: Found {Count} long-running processes.", longRunningProcessIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServiceRestart: Failed to query dtx_process table.");
            return;
        }

        if (longRunningProcessIds.Count == 0)
        {
            _logger.LogInformation("ServiceRestart: No long-running processes found. No restart needed.");
            return;
        }

        _logger.LogInformation("ServiceRestart: Found long-running processes. Evaluating restart for '{Service}'.", _windowsServiceName);

        // 2. Attempt to restart the Windows service
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

        // 3. Update dtx_process status for each long-running process
        foreach (var idProcess in longRunningProcessIds)
        {
            try
            {
                string newStatus = restartSuccess ? DtxProcessStatus.Completed : DtxProcessStatus.NotCompleted;
                _logger.LogInformation("ServiceRestart: Updating dtx_process record {IdProcess} to status {Status}.", idProcess, newStatus);
                await dtxProcessRepo.UpdateStatusAsync(idProcess, newStatus, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ServiceRestart: Failed to update dtx_process record {IdProcess}.", idProcess);
            }
        }

        if (restartSuccess)
        {
            _logger.LogInformation("ServiceRestart: Restart completed successfully. Updated {Count} process records.", longRunningProcessIds.Count);
        }
        else
        {
            _logger.LogWarning("ServiceRestart: Restart failed. Process records marked as NOTCOMPLETED and will be retried on the next check cycle.");
        }
    }

    /// <summary>
    /// Determines whether the specified service is currently running.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
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
