using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using ServicioRESTEjecucionComandos.Constants;
using ServicioRESTEjecucionComandos.Models;
using ServicioRESTEjecucionComandos.Repositories;

namespace ServicioRESTEjecucionComandos.Services;

/// <summary>
/// Central service for ETL execution that bridges Hangfire background jobs with
/// CommandExecutor and ETLExecutionHistory persistence.
/// </summary>
public class EtlJobService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CommandExecutor _executor;
    private readonly ILogger<EtlJobService> _logger;
    private readonly SemaphoreSlim _semaphore;
    private readonly ExecutionNotifier _notifier;
    private readonly string[] _dailyCodes;
    private readonly int _processCheckWaitSeconds;

    /// <summary>
    /// Initializes a new instance of EtlJobService.
    /// </summary>
    public EtlJobService(
        IServiceScopeFactory scopeFactory,
        CommandExecutor executor,
        ILogger<EtlJobService> logger,
        IConfiguration configuration,
        ExecutionNotifier notifier)
    {
        _scopeFactory = scopeFactory;
        _executor = executor;
        _logger = logger;
        var maxParallel = configuration.GetValue<int>("QueueConfig:MaxParallelExecutions", 1);
        _semaphore = new SemaphoreSlim(maxParallel, maxParallel);
        _logger.LogInformation("EtlJobService initialized with MaxParallelExecutions = {MaxParallel}.", maxParallel);
        _notifier = notifier;
        _dailyCodes = configuration.GetSection("QueueConfig:DailyCodes").Get<string[]>() ?? Array.Empty<string>();
        _processCheckWaitSeconds = configuration.GetValue<int>("QueueConfig:ProcessCheckWaitSeconds", 10);
    }

    /// <summary>
    /// Enqueues a manual ETL execution via Hangfire, creating the history record first.
    /// Uses an atomic upsert-or-get pattern to eliminate the N+1 check-then-insert query.
    /// </summary>
    /// <param name="tipoEntidad">Entity type for this execution.</param>
    /// <param name="codEnvio">Sending code identifier.</param>
    /// <param name="fechaDatos">Data date for this execution.</param>
    /// <param name="codigo">Database code to execute.</param>
    /// <param name="triggerType">Trigger type: MANUAL or REPROCESO.</param>
    /// <returns>The HistoryId of the created or existing execution record.</returns>
    public async Task<Guid> EnqueueManualAsync(
        string tipoEntidad,
        string codEnvio,
        DateOnly fechaDatos,
        string codigo,
        string triggerType = TriggerType.Manual)
    {
        Guid historyId;
        using (var scope = _scopeFactory.CreateScope())
        {
            var historyRepo = scope.ServiceProvider.GetRequiredService<ETLExecutionHistoryRepository>();

            // Atomic check-and-insert: returns existing active record or creates a new one in a single scope.
            _logger.LogInformation("[DB] Atomic upsert-or-get for active execution in hist_etl_execution for CodEnvio={CodEnvio}, Codigo={Codigo}.", codEnvio, codigo);
            var history = await historyRepo.UpsertOrGetActiveAsync(codEnvio, codigo, tipoEntidad, fechaDatos, triggerType);
            historyId = history.Id;
        }

        _logger.LogInformation("Using ETLExecutionHistory {HistoryId} with trigger type {TriggerType}.", historyId, triggerType);

        // Enqueue in Hangfire - only pass serializable parameters (Guid + string)
        BackgroundJob.Enqueue(
            () => ExecuteJobByIdAsync(historyId, triggerType));

        _logger.LogInformation("Enqueued manual ETL job for HistoryId {HistoryId} via Hangfire.", historyId);
        return historyId;
    }

    /// <summary>
    /// Enqueues a scheduled ETL execution via Hangfire, creating a history record in the scheduled table.
    /// </summary>
    public async Task<Guid> EnqueueScheduledAsync(EtlSchedule schedule)
    {
        Guid historyId;
        using (var scope = _scopeFactory.CreateScope())
        {
            var scheduledRepo = scope.ServiceProvider.GetRequiredService<ETLExecutionHistoryScheduledRepository>();

            // Create ETLExecutionHistoryScheduled record with PENDIENTE status
            var history = new ETLExecutionHistoryScheduled
            {
                ScheduleId = schedule.Id,
                Params = schedule.Params,
                Status = EtlStatus.Pending
            };

            _logger.LogInformation("[DB] Inserting new ETLExecutionHistoryScheduled record with status PENDIENTE for ScheduleId={ScheduleId}.", schedule.Id);
            await scheduledRepo.CreateAsync(history);
            historyId = history.Id;
        }

        _logger.LogInformation("Created ETLExecutionHistoryScheduled {HistoryId} for ScheduleId {ScheduleId}.", historyId, schedule.Id);

        // Enqueue in Hangfire
        BackgroundJob.Enqueue(
            () => ExecuteScheduledJobByHistoryIdAsync(historyId));

        _logger.LogInformation("Enqueued scheduled ETL job for HistoryId {HistoryId} via Hangfire.", historyId);
        return historyId;
    }

    /// <summary>
    /// Executes a scheduled ETL job by loading the schedule from the database by ID.
    /// This method is called by Hangfire recurring jobs and ensures fresh schedule data.
    /// </summary>
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteScheduledJobByIdAsync(Guid scheduleId)
    {
        _logger.LogInformation("Starting scheduled ETL job for ScheduleId {ScheduleId}.", scheduleId);

        try
        {
            // Load schedule from database in a scope
            using (var scope = _scopeFactory.CreateScope())
            {
                var scheduleRepo = scope.ServiceProvider.GetRequiredService<EtlScheduleRepository>();
                _logger.LogInformation("[DB] Loading EtlSchedule by Id {ScheduleId} from schedules table.", scheduleId);
                var schedule = await scheduleRepo.GetByIdAsync(scheduleId);
                _logger.LogInformation("[DB] Schedule load completed. Schedule found: {ScheduleFound}.", schedule != null);

                if (schedule == null || !schedule.IsActive)
                {
                    _logger.LogWarning(
                        "Schedule {ScheduleId} not found or inactive. Skipping execution.", scheduleId);
                    return;
                }

                // Delegate to scheduled enqueue
                await EnqueueScheduledAsync(schedule);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while triggering scheduled ETL job for ScheduleId {ScheduleId}.", scheduleId);
        }
    }

    /// <summary>
    /// Executes a scheduled ETL job by loading the scheduled history record,
    /// building the command from Params, running CommandExecutor, and updating the history status.
    /// All database operations are consolidated into a single scope to eliminate N+1 queries.
    /// Called by Hangfire background jobs for scheduled executions.
    /// </summary>
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteScheduledJobByHistoryIdAsync(Guid historyId)
    {
        _logger.LogInformation("Starting scheduled ETL job for ScheduledHistoryId {HistoryId}.", historyId);

        bool slotAcquired = false;
        long? dtxProcessId = null;

        try
        {
            // Wait for an execution slot before marking as EN PROCESO.
            _logger.LogInformation("Waiting for execution slot for ScheduledHistoryId {HistoryId}.", historyId);
            await _semaphore.WaitAsync();
            slotAcquired = true;
            _logger.LogInformation("Acquired execution slot for ScheduledHistoryId {HistoryId}.", historyId);

            // Single consolidated scope for all database operations in this job lifecycle.
            using var scope = _scopeFactory.CreateScope();
            var scheduledRepo = scope.ServiceProvider.GetRequiredService<ETLExecutionHistoryScheduledRepository>();
            var dtxProcessRepo = scope.ServiceProvider.GetRequiredService<DtxProcessRepository>();

            _logger.LogInformation("[DB] Loading ETLExecutionHistoryScheduled by Id {HistoryId}.", historyId);
            var history = await scheduledRepo.GetByIdAsync(historyId);
            _logger.LogInformation("[DB] Scheduled history load completed. Record found: {HistoryFound}.", history != null);

            if (history == null)
            {
                _logger.LogError("ETLExecutionHistoryScheduled {HistoryId} not found. Cannot execute job.", historyId);
                return;
            }

            // Check for existing RUNNING processes in dtx_process and wait if needed
            _logger.LogInformation("Checking dtx_process for RUNNING processes before executing ScheduledHistoryId {HistoryId}.", historyId);
            await WaitForNoRunningProcessesAsync(dtxProcessRepo);

            // Update status to EN PROCESO only after waiting for no running processes
            _logger.LogInformation("[DB] Updating ETLExecutionHistoryScheduled {HistoryId} status to {Status}.", historyId, EtlStatus.InProgress);
            await scheduledRepo.UpdateStatusAsync(historyId, EtlStatus.InProgress, executedAt: DateTime.UtcNow);

            // Create dtx_process record for this execution
            var dtxProcess = new Models.DtxProcess
            {
                AppName = "ETLDATAX",
                ProcessName = "-",
                Status = DtxProcessStatus.Running,
                StartTime = DateTime.UtcNow,
                Active = false,
                Details = history.Params,
                CreatedAt = DateTime.UtcNow
            };
            dtxProcessId = await dtxProcessRepo.InsertAsync(dtxProcess);
            _logger.LogInformation("Created dtx_process record {DtxProcessId} for ScheduledHistoryId {HistoryId}.", dtxProcessId, historyId);

            // Notify connected clients that the scheduled task has started
            await _notifier.BroadcastTaskStartedAsync(history.Params);

            // Build queue item for CommandExecutor using Params directly as command arguments
            var queueItem = new ExecutionQueueItem
            {
                Id = Guid.NewGuid(),
                HistoryId = historyId,
                Params = history.Params,
                Status = EtlStatus.InProgress,
                CreatedAt = DateTime.UtcNow
            };

            // Execute command
            var result = await _executor.ExecuteAsync(queueItem);

            // Update final status
            var status = result.Success ? EtlStatus.Success : EtlStatus.Failed;
            var completedAt = DateTime.UtcNow;

            _logger.LogInformation("[DB] Updating final status to {Status} for ScheduledHistoryId {HistoryId}.", status, historyId);
            await scheduledRepo.UpdateStatusAsync(
                historyId,
                status,
                exitCode: result.ExitCode,
                output: result.Output,
                error: result.Error,
                completedAt: completedAt);

            // Update dtx_process record
            if (dtxProcessId.HasValue)
            {
                await dtxProcessRepo.UpdateStatusAsync(
                    dtxProcessId.Value,
                    result.Success ? DtxProcessStatus.Completed : DtxProcessStatus.NotCompleted,
                    DateTime.UtcNow);
                _logger.LogInformation("Updated dtx_process record {DtxProcessId} to {Status}.", dtxProcessId.Value, result.Success ? DtxProcessStatus.Completed : DtxProcessStatus.NotCompleted);
            }

            if (result.Success)
            {
                _logger.LogInformation("Scheduled ETL job {HistoryId} completed successfully.", historyId);
            }
            else
            {
                _logger.LogWarning("Scheduled ETL job {HistoryId} failed with exit code {ExitCode}.", historyId, result.ExitCode);
            }

            // Notify connected clients that the scheduled task has completed
            await _notifier.BroadcastTaskCompletedAsync(result.Success, history.Params);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while executing scheduled ETL job {HistoryId}", historyId);

            if (slotAcquired)
            {
                await UpdateScheduledStatusInScopeAsync(
                    historyId,
                    EtlStatus.Failed,
                    error: ex.Message,
                    completedAt: DateTime.UtcNow);

                // Update dtx_process record if exists
                if (dtxProcessId.HasValue)
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var dtxProcessRepo = scope.ServiceProvider.GetRequiredService<DtxProcessRepository>();
                        await dtxProcessRepo.UpdateStatusAsync(dtxProcessId.Value, DtxProcessStatus.NotCompleted, DateTime.UtcNow);
                    }
                    catch (Exception dtxEx)
                    {
                        _logger.LogError(dtxEx, "Failed to update dtx_process record {DtxProcessId} on exception.", dtxProcessId.Value);
                    }
                }

                // Notify connected clients that the scheduled task failed
                await _notifier.BroadcastTaskCompletedAsync(false, null);
            }
        }
        finally
        {
            if (slotAcquired)
            {
                _semaphore.Release();
                _logger.LogInformation("Released execution slot after processing ScheduledHistoryId {HistoryId}.", historyId);
            }
        }
    }

    /// <summary>
    /// Executes a single ETL job by loading the history record, building the command,
    /// running CommandExecutor, and updating the history status.
    /// All database operations are consolidated into a single scope to eliminate N+1 queries.
    /// Called by Hangfire background jobs.
    /// </summary>
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteJobByIdAsync(Guid historyId, string triggerType)
    {
        _logger.LogInformation("Starting ETL job for HistoryId {HistoryId} (trigger: {TriggerType}).", historyId, triggerType);

        bool slotAcquired = false;
        long? dtxProcessId = null;

        try
        {
            // Wait for an execution slot before marking as EN PROCESO.
            _logger.LogInformation("Waiting for execution slot for HistoryId {HistoryId}.", historyId);
            await _semaphore.WaitAsync();
            slotAcquired = true;
            _logger.LogInformation("Acquired execution slot for HistoryId {HistoryId}.", historyId);

            // Single consolidated scope for all database operations in this job lifecycle.
            using var scope = _scopeFactory.CreateScope();
            var historyRepo = scope.ServiceProvider.GetRequiredService<ETLExecutionHistoryRepository>();
            var dtxProcessRepo = scope.ServiceProvider.GetRequiredService<DtxProcessRepository>();

            _logger.LogInformation("[DB] Loading ETLExecutionHistory by Id {HistoryId} from hist_etl_execution table.", historyId);
            var history = await historyRepo.GetByIdAsync(historyId);
            _logger.LogInformation("[DB] History load completed. Record found: {HistoryFound}.", history != null);

            if (history == null)
            {
                _logger.LogError("ETLExecutionHistory {HistoryId} not found. Cannot execute job.", historyId);
                return;
            }

            // Check for existing RUNNING processes in dtx_process and wait if needed
            _logger.LogInformation("Checking dtx_process for RUNNING processes before executing HistoryId {HistoryId}.", historyId);
            await WaitForNoRunningProcessesAsync(dtxProcessRepo);

            // Update status to EN PROCESO only after waiting for no running processes
            _logger.LogInformation("[DB] Updating ETLExecutionHistory {HistoryId} status to {Status}.", historyId, EtlStatus.InProgress);
            await historyRepo.UpdateStatusAsync(historyId, EtlStatus.InProgress, executedAt: DateTime.UtcNow);

            // Determine Start/End dates based on TriggerType.
            // For MANUAL (Actualizar): FechaDatos already holds the target period.
            // For REPROCESO: uses the same period as FechaDatos.
            string startDate, endDate;
            bool isDayBased = _dailyCodes.Contains(history.Codigo, StringComparer.OrdinalIgnoreCase);
            bool isReproceso = history.TriggerType?.Equals(TriggerType.Reproceso, StringComparison.OrdinalIgnoreCase) == true;

            if (isDayBased)
            {
                // Day-based: same day period
                startDate = history.FechaDatos.ToString("yyyy-MM-dd");
                endDate = history.FechaDatos.AddDays(1).ToString("yyyy-MM-dd");
            }
            else
            {
                // Month-based: same month period
                startDate = new DateOnly(history.FechaDatos.Year, history.FechaDatos.Month, 1).ToString("yyyy-MM-dd");
                var lastDay = DateTime.DaysInMonth(history.FechaDatos.Year, history.FechaDatos.Month);
                endDate = new DateOnly(history.FechaDatos.Year, history.FechaDatos.Month, lastDay).ToString("yyyy-MM-dd");
            }

            // Build queue item for CommandExecutor
            var queueItem = new ExecutionQueueItem
            {
                Id = Guid.NewGuid(),
                HistoryId = historyId,
                Params = $"-code {history.Codigo} -start {startDate} -end {endDate} -codesend {history.CodEnvio}",
                Status = EtlStatus.InProgress,
                CreatedAt = DateTime.UtcNow
            };

            // Create dtx_process record for this execution
            var dtxProcess = new Models.DtxProcess
            {
                AppName = "ETLDATAX",
                ProcessName = "-",
                Status = DtxProcessStatus.Running,
                StartTime = DateTime.UtcNow,
                Active = false,
                Details = $"HistoryId={historyId}, TriggerType={triggerType}",
                CreatedAt = DateTime.UtcNow
            };
            dtxProcessId = await dtxProcessRepo.InsertAsync(dtxProcess);
            _logger.LogInformation("Created dtx_process record {DtxProcessId} for HistoryId {HistoryId}.", dtxProcessId, historyId);

            // Execute command
            var result = await _executor.ExecuteAsync(queueItem);

            var completedAt = DateTime.UtcNow;

            if (result.Success)
            {
                // Only verify dtx_seguimiento when the ETL execution succeeded.
                // If the ETL failed, report the failure immediately without hiding it behind verification.
                var verificationResult = await historyRepo.VerifyDtxSeguimientoAsync(history.CodEnvio, history.Codigo);
                var verificationFechaDatos = verificationResult?.FechaDatos;

                bool targetPeriodMatched = false;

                if (verificationFechaDatos.HasValue)
                {
                    // Check if the returned FechaDatos falls within the target period defined by startDate/endDate.
                    var verificationDate = verificationFechaDatos.Value;
                    var start = DateOnly.Parse(startDate);
                    var end = DateOnly.Parse(endDate);
                    targetPeriodMatched = verificationDate >= start && verificationDate <= end;
                }

                if (targetPeriodMatched)
                {
                    // dtx_seguimiento record found for the target period - update normally
                    _logger.LogInformation("[DB] dtx_seguimiento verification passed for HistoryId {HistoryId}. Updating final status to EXITOSO with FechaDatos={FechaDatos}.",
                        historyId, verificationFechaDatos);
                    await historyRepo.UpdateStatusWithFechaDatosAsync(
                        historyId,
                        EtlStatus.Success,
                        fechaDatos: verificationFechaDatos,
                        exitCode: result.ExitCode,
                        output: result.Output,
                        error: result.Error,
                        completedAt: completedAt);

                    _logger.LogInformation("ETL job {HistoryId} completed successfully.", historyId);
                }
                else
                {
                    // No matching dtx_seguimiento record for the target period - mark as FALLIDO with descriptive error
                    string errorMessage;
                    if (isDayBased)
                    {
                        errorMessage = $"No se encontraron datos para ejecutar el ETL para la fecha {startDate}";
                    }
                    else
                    {
                        errorMessage = $"No se encontraron datos para ejecutar el ETL desde fecha {startDate} hasta fecha {endDate}";
                    }

                    _logger.LogWarning("[DB] dtx_seguimiento verification FAILED for HistoryId {HistoryId}. No matching record for target period [{Start}, {End}]. Marking as FALLIDO.",
                        historyId, startDate, endDate);
                    await historyRepo.UpdateStatusWithFechaDatosAsync(
                        historyId,
                        EtlStatus.Failed,
                        fechaDatos: verificationFechaDatos,
                        exitCode: result.ExitCode,
                        output: result.Output,
                        error: errorMessage,
                        completedAt: completedAt);
                }
            }
            else
            {
                // ETL execution failed - still verify dtx_seguimiento to populate FechaDatos
                _logger.LogWarning("ETL job {HistoryId} failed with exit code {ExitCode}. Verifying dtx_seguimiento before marking as FALLIDO.", historyId, result.ExitCode);
                var verificationResult = await historyRepo.VerifyDtxSeguimientoAsync(history.CodEnvio, history.Codigo);
                var verificationFechaDatos = verificationResult?.FechaDatos;

                _logger.LogInformation("[DB] Updating final status to FALLIDO for HistoryId {HistoryId} with FechaDatos={FechaDatos}.",
                    historyId, verificationFechaDatos);
                await historyRepo.UpdateStatusWithFechaDatosAsync(
                    historyId,
                    EtlStatus.Failed,
                    fechaDatos: verificationFechaDatos,
                    exitCode: result.ExitCode,
                    output: result.Output,
                    error: result.Error,
                    completedAt: completedAt);
            }

            // Update dtx_process record
            if (dtxProcessId.HasValue)
            {
                string finalStatus = result.Success ? DtxProcessStatus.Completed : DtxProcessStatus.NotCompleted;
                await dtxProcessRepo.UpdateStatusAsync(dtxProcessId.Value, finalStatus, DateTime.UtcNow);
                _logger.LogInformation("Updated dtx_process record {DtxProcessId} to {Status}.", dtxProcessId.Value, finalStatus);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while executing ETL job {HistoryId}", historyId);

            if (slotAcquired)
            {
                await UpdateStatusInScopeAsync(
                    historyId,
                    EtlStatus.Failed,
                    error: ex.Message,
                    completedAt: DateTime.UtcNow);

                // Update dtx_process record if exists
                if (dtxProcessId.HasValue)
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var dtxProcessRepo = scope.ServiceProvider.GetRequiredService<DtxProcessRepository>();
                        await dtxProcessRepo.UpdateStatusAsync(dtxProcessId.Value, DtxProcessStatus.NotCompleted, DateTime.UtcNow);
                    }
                    catch (Exception dtxEx)
                    {
                        _logger.LogError(dtxEx, "Failed to update dtx_process record {DtxProcessId} on exception.", dtxProcessId.Value);
                    }
                }
            }
        }
        finally
        {
            if (slotAcquired)
            {
                _semaphore.Release();
                _logger.LogInformation("Released execution slot after processing HistoryId {HistoryId}.", historyId);
            }
        }
    }

    /// <summary>
    /// Waits until no RUNNING processes exist in dtx_process table.
    /// Polls every _processCheckWaitSeconds until clear or timeout.
    /// </summary>
    private async Task WaitForNoRunningProcessesAsync(DtxProcessRepository dtxProcessRepo)
    {
        var maxWaitTime = TimeSpan.FromMinutes(5);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < maxWaitTime)
        {
            var hasRunningProcesses = await dtxProcessRepo.AreRunningProcessesExistAsync();
            if (!hasRunningProcesses)
            {
                _logger.LogInformation("No RUNNING processes found in dtx_process. Proceeding.");
                return;
            }

            _logger.LogInformation("RUNNING processes still exist in dtx_process. Waiting {WaitSeconds}s before retrying.", _processCheckWaitSeconds);
            await Task.Delay(_processCheckWaitSeconds * 1000);
        }

        _logger.LogWarning("Timeout waiting for RUNNING processes to clear in dtx_process after {Timeout}s. Proceeding anyway.", maxWaitTime.TotalSeconds);
    }

    /// <summary>
    /// Creates a scoped service provider and calls UpdateStatusAsync on ETLExecutionHistoryRepository,
    /// ensuring the scoped DbContext is properly disposed after each call.
    /// Used only as a fallback in exception handlers when the main scope may already be disposed.
    /// </summary>
    private async Task UpdateStatusInScopeAsync(
        Guid historyId,
        string status,
        int? exitCode = null,
        string? output = null,
        string? error = null,
        DateTime? executedAt = null,
        DateTime? completedAt = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ETLExecutionHistoryRepository>();
        await repo.UpdateStatusAsync(
            historyId,
            status,
            exitCode: exitCode,
            output: output,
            error: error,
            executedAt: executedAt,
            completedAt: completedAt);
    }

    /// <summary>
    /// Creates a scoped service provider and calls UpdateStatusAsync on ETLExecutionHistoryScheduledRepository,
    /// ensuring the scoped DbContext is properly disposed after each call.
    /// Used only as a fallback in exception handlers when the main scope may already be disposed.
    /// </summary>
    private async Task UpdateScheduledStatusInScopeAsync(
        Guid historyId,
        string status,
        int? exitCode = null,
        string? output = null,
        string? error = null,
        DateTime? executedAt = null,
        DateTime? completedAt = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ETLExecutionHistoryScheduledRepository>();
        await repo.UpdateStatusAsync(
            historyId,
            status,
            exitCode: exitCode,
            output: output,
            error: error,
            executedAt: executedAt,
            completedAt: completedAt);
    }
}
