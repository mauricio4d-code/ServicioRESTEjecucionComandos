using Hangfire;
using Hangfire.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServicioRESTEjecucionComandos.Models;
using ServicioRESTEjecucionComandos.Repositories;


namespace ServicioRESTEjecucionComandos.Services;

/// <summary>
/// Background service that synchronizes ETL schedules from the database with Hangfire recurring jobs.
/// Runs on startup and periodically to detect changes.
/// </summary>
public class ScheduleSyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScheduleSyncService> _logger;
    private readonly TimeSpan _syncInterval;

    /// <summary>
    /// Initializes a new instance of ScheduleSyncService.
    /// </summary>
    public ScheduleSyncService(
        IServiceProvider serviceProvider,
        ILogger<ScheduleSyncService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _syncInterval = TimeSpan.FromSeconds(
            configuration.GetValue<int>("Hangfire:SyncIntervalSeconds", 60));
    }

    /// <summary>
    /// Executes the background sync loop.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScheduleSyncService starting.");

        // Initial sync on startup
        await SyncSchedulesAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_syncInterval, stoppingToken);
                await SyncSchedulesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during schedule sync cycle.");
            }
        }

        _logger.LogInformation("ScheduleSyncService stopped.");
    }

    /// <summary>
    /// Synchronizes the database schedules with Hangfire recurring jobs.
    /// </summary>
    private async Task SyncSchedulesAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var scheduleRepo = scope.ServiceProvider.GetRequiredService<EtlScheduleRepository>();
            var activeSchedules = await scheduleRepo.GetActiveAsync();

            if (stoppingToken.IsCancellationRequested) return;

            var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

            // Build a set of job IDs that should exist
            var expectedJobIds = new HashSet<string>();

            foreach (var schedule in activeSchedules)
            {
                if (stoppingToken.IsCancellationRequested) return;

                var jobId = $"etl-schedule-{schedule.Id}";
                expectedJobIds.Add(jobId);

                try
                {
                    // Validate cron expression before registering
                    if (!IsValidCronExpression(schedule.CronExpression))
                    {
                        _logger.LogWarning(
                            "Invalid cron expression '{Cron}' for schedule {Id}. Skipping.",
                            schedule.CronExpression, schedule.Id);
                        continue;
                    }

                    // Register or update the recurring job (idempotent)
                    // Pass schedule ID so the job loads fresh data from the database at execution time
                    recurringJobManager.AddOrUpdate<EtlJobService>(
                        jobId,
                        service => service.ExecuteScheduledJobByIdAsync(schedule.Id),
                        schedule.CronExpression,
                        TimeZoneInfo.Local);

                    _logger.LogDebug(
                        "Synced recurring job {JobId} for schedule {ScheduleId} with cron '{Cron}'.",
                        jobId, schedule.Id, schedule.CronExpression);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex, "Failed to sync recurring job for schedule {ScheduleId}.", schedule.Id);
                }
            }

            // Remove jobs for schedules that are no longer active
            await CleanupInactiveJobsAsync(scheduleRepo, recurringJobManager);

            _logger.LogInformation(
                "Schedule sync completed. {ActiveCount} active schedules synced.",
                activeSchedules.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during schedule sync.");
        }
    }

    /// <summary>
    /// Attempts to remove recurring jobs for schedules that are no longer active.
    /// </summary>
    private async Task CleanupInactiveJobsAsync(
        EtlScheduleRepository scheduleRepo,
        IRecurringJobManager recurringJobManager)
    {
        var allSchedules = await scheduleRepo.GetAllAsync();
        var activeIds = new HashSet<Guid>(allSchedules.Where(s => s.IsActive).Select(s => s.Id));

        foreach (var schedule in allSchedules)
        {
            if (!activeIds.Contains(schedule.Id))
            {
                var jobId = $"etl-schedule-{schedule.Id}";
                try
                {
                    // Remove the recurring job using the Hangfire API
                    recurringJobManager.RemoveIfExists(jobId);
                    _logger.LogDebug("Removed recurring job {JobId} for inactive schedule.", jobId);
                }
                catch
                {
                    // Job may not exist - ignore
                }
            }
        }
    }

    /// <summary>
    /// Validates a cron expression by checking basic structure.
    /// Hangfire will reject invalid expressions when registering the job.
    /// </summary>
    private static bool IsValidCronExpression(string cron)
    {
        if (string.IsNullOrWhiteSpace(cron)) return false;

        // Basic validation: cron should have 5 space-separated parts
        var parts = cron.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5) return false;

        // Each part should be a valid cron segment (number, *, */N, N-N, N,N)
        var segmentPattern = @"^(\*|(\d+)(-\d+)?(,\d+(-\d+)?)*)$";
        foreach (var part in parts)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(part, segmentPattern))
            {
                // Allow */N syntax
                if (!System.Text.RegularExpressions.Regex.IsMatch(part, @"^\*/\d+$"))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
