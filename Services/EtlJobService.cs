using Hangfire;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly string[] _dailyCodes;
    private readonly SemaphoreSlim _semaphore;

    /// <summary>
    /// Initializes a new instance of EtlJobService.
    /// </summary>
    public EtlJobService(
        IServiceScopeFactory scopeFactory,
        CommandExecutor executor,
        ILogger<EtlJobService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _executor = executor;
        _logger = logger;
        _dailyCodes = configuration.GetSection("QueueConfig:DailyCodes").Get<string[]>() ?? Array.Empty<string>();
        var maxParallel = configuration.GetValue<int>("QueueConfig:MaxParallelExecutions", 1);
        _semaphore = new SemaphoreSlim(maxParallel, maxParallel);
        _logger.LogInformation("EtlJobService initialized with MaxParallelExecutions = {MaxParallel}.", maxParallel);
    }

    /// <summary>
    /// Enqueues a manual ETL execution via Hangfire, creating the history record first.
    /// </summary>
    /// <param name="tipoEntidad">Entity type for this execution.</param>
    /// <param name="codEnvio">Sending code identifier.</param>
    /// <param name="fechaDatos">Data date for this execution.</param>
    /// <param name="codigo">Database code to execute.</param>
    /// <param name="triggerType">Trigger type: MANUAL or REPROCESO.</param>
    /// <returns>The HistoryId of the created execution record.</returns>
    public async Task<Guid> EnqueueManualAsync(
        string tipoEntidad,
        string codEnvio,
        DateOnly fechaDatos,
        string codigo,
        string triggerType = "MANUAL")
    {
        Guid historyId;
        using (var scope = _scopeFactory.CreateScope())
        {
            var historyRepo = scope.ServiceProvider.GetRequiredService<ETLExecutionHistoryRepository>();

            // Prevent duplicate active executions for the same CodEnvio + Codigo combination.
            // If there's already a PENDIENTE or EN PROCESO record, return its ID instead of creating a new one.
            var existing = await historyRepo.GetActiveExecutionAsync(codEnvio, codigo);
            if (existing != null)
            {
                _logger.LogWarning(
                    "Duplicate enqueue prevented for CodEnvio={CodEnvio}, Codigo={Codigo}. " +
                    "Returning existing HistoryId={HistoryId} with status {Status}.",
                    codEnvio, codigo, existing.Id, existing.Status);
                return existing.Id;
            }

            // Create ETLExecutionHistory record with PENDIENTE status
            var history = new ETLExecutionHistory
            {
                CodEnvio = codEnvio,
                TipoEntidad = tipoEntidad,
                FechaDatos = fechaDatos,
                Codigo = codigo,
                Status = "PENDIENTE",
                TriggerType = triggerType
            };

            await historyRepo.CreateAsync(history);
            historyId = history.Id;
        }

        _logger.LogInformation("Created ETLExecutionHistory {HistoryId} with trigger type {TriggerType}.", historyId, triggerType);

        // Enqueue in Hangfire - only pass serializable parameters (Guid + string)
        BackgroundJob.Enqueue(
            () => ExecuteJobByIdAsync(historyId, triggerType));

        _logger.LogInformation("Enqueued manual ETL job for HistoryId {HistoryId} via Hangfire.", historyId);
        return historyId;
    }

    /// <summary>
    /// Enqueues a scheduled ETL execution via Hangfire.
    /// </summary>
    public async Task<Guid> EnqueueScheduledAsync(EtlSchedule schedule)
    {
        return await EnqueueManualAsync(
            schedule.TipoEntidad,
            schedule.CodEnvio,
            DateOnly.FromDateTime(DateTime.UtcNow),
            schedule.Codigo,
            triggerType: "PROGRAMADO");
    }

    /// <summary>
    /// Executes a scheduled ETL job by loading the schedule from the database by ID.
    /// This method is called by Hangfire recurring jobs and ensures fresh schedule data.
    /// </summary>
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteScheduledJobByIdAsync(Guid scheduleId)
    {
        _logger.LogInformation("Starting scheduled ETL job for ScheduleId {ScheduleId}.", scheduleId);

        EtlSchedule? schedule = null;

        try
        {
            // Load schedule from database in a scope
            using (var scope = _scopeFactory.CreateScope())
            {
                var scheduleRepo = scope.ServiceProvider.GetRequiredService<EtlScheduleRepository>();
                schedule = await scheduleRepo.GetByIdAsync(scheduleId);
            }

            if (schedule == null || !schedule.IsActive)
            {
                _logger.LogWarning(
                    "Schedule {ScheduleId} not found or inactive. Skipping execution.", scheduleId);
                return;
            }

            // Delegate to manual enqueue with PROGRAMADO trigger type
            await EnqueueManualAsync(
                schedule.TipoEntidad,
                schedule.CodEnvio,
                DateOnly.FromDateTime(DateTime.UtcNow),
                schedule.Codigo,
                triggerType: "PROGRAMADO");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while triggering scheduled ETL job for ScheduleId {ScheduleId}.", scheduleId);
        }
    }

    /// <summary>
    /// Executes a single ETL job by loading the history record, building the command,
    /// running CommandExecutor, and updating the history status.
    /// Called by Hangfire background jobs.
    /// </summary>
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteJobByIdAsync(Guid historyId, string triggerType)
    {
        _logger.LogInformation("Starting ETL job for HistoryId {HistoryId} (trigger: {TriggerType}).", historyId, triggerType);

        bool slotAcquired = false;
        try
        {
            // Wait for an execution slot before marking as EN PROCESO.
            // This ensures at most MaxParallelExecutions items are ever in EN PROCESO state.
            _logger.LogInformation("Waiting for execution slot for HistoryId {HistoryId}.", historyId);
            await _semaphore.WaitAsync();
            slotAcquired = true;
            _logger.LogInformation("Acquired execution slot for HistoryId {HistoryId}.", historyId);

            ETLExecutionHistory? history = null;

            // Load history record in a scope
            using (var scope = _scopeFactory.CreateScope())
            {
                var historyRepo = scope.ServiceProvider.GetRequiredService<ETLExecutionHistoryRepository>();
                history = await historyRepo.GetByIdAsync(historyId);
            }

            if (history == null)
            {
                _logger.LogError("ETLExecutionHistory {HistoryId} not found. Cannot execute job.", historyId);
                return;
            }

            // Update status to EN PROCESO only after acquiring a slot
            await UpdateStatusInScopeAsync(historyId, "EN PROCESO", executedAt: DateTime.UtcNow);

            // Determine Start/End dates based on TriggerType.
            // For MANUAL (Actualizar): FechaDatos already holds the target period
            //   (computed by the controller before enqueuing), so use it directly.
            // For REPROCESO: uses the same period as FechaDatos.
            // For PROGRAMADO: advances to the next period after FechaDatos.
            string startDate, endDate;
            bool isDayBased = _dailyCodes.Contains(history.Codigo, StringComparer.OrdinalIgnoreCase);
            bool isReproceso = history.TriggerType?.Equals("REPROCESO", StringComparison.OrdinalIgnoreCase) == true;
            bool isProgramado = history.TriggerType?.Equals("PROGRAMADO", StringComparison.OrdinalIgnoreCase) == true;

            if (isDayBased)
            {
                if (isReproceso)
                {
                    // Reprocesar: same day period
                    startDate = history.FechaDatos.ToString("yyyy-MM-dd");
                    endDate = history.FechaDatos.AddDays(1).ToString("yyyy-MM-dd");
                }
                else if (isProgramado)
                {
                    // Programado: next day period (FechaDatos = today, target = tomorrow)
                    var nextDay = history.FechaDatos.AddDays(1);
                    var nextNextDay = history.FechaDatos.AddDays(2);
                    startDate = nextDay.ToString("yyyy-MM-dd");
                    endDate = nextNextDay.ToString("yyyy-MM-dd");
                }
                else
                {
                    // MANUAL (Actualizar): FechaDatos is already the target day
                    startDate = history.FechaDatos.ToString("yyyy-MM-dd");
                    endDate = history.FechaDatos.AddDays(1).ToString("yyyy-MM-dd");
                }
            }
            else
            {
                if (isReproceso)
                {
                    // Reprocesar: same month period
                    startDate = new DateOnly(history.FechaDatos.Year, history.FechaDatos.Month, 1).ToString("yyyy-MM-dd");
                    var lastDay = DateTime.DaysInMonth(history.FechaDatos.Year, history.FechaDatos.Month);
                    endDate = new DateOnly(history.FechaDatos.Year, history.FechaDatos.Month, lastDay).ToString("yyyy-MM-dd");
                }
                else if (isProgramado)
                {
                    // Programado: next month period (FechaDatos = today, target = next month)
                    var nextMonth = history.FechaDatos.AddMonths(1);
                    startDate = new DateOnly(nextMonth.Year, nextMonth.Month, 1).ToString("yyyy-MM-dd");
                    var lastDay = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
                    endDate = new DateOnly(nextMonth.Year, nextMonth.Month, lastDay).ToString("yyyy-MM-dd");
                }
                else
                {
                    // MANUAL (Actualizar): FechaDatos is already the target month
                    startDate = new DateOnly(history.FechaDatos.Year, history.FechaDatos.Month, 1).ToString("yyyy-MM-dd");
                    var lastDay = DateTime.DaysInMonth(history.FechaDatos.Year, history.FechaDatos.Month);
                    endDate = new DateOnly(history.FechaDatos.Year, history.FechaDatos.Month, lastDay).ToString("yyyy-MM-dd");
                }
            }

            // Build queue item for CommandExecutor
            var queueItem = new ExecutionQueueItem
            {
                Id = Guid.NewGuid(),
                HistoryId = historyId,
                TipoEntidad = history.TipoEntidad,
                CodEnvio = history.CodEnvio,
                FechaDatos = history.FechaDatos,
                Code = history.Codigo,
                Start = startDate,
                End = endDate,
                Codesend = history.CodEnvio,
                Status = "EN PROCESO",
                CreatedAt = DateTime.UtcNow
            };

            // Execute command
            var result = await _executor.ExecuteAsync(queueItem);

            var completedAt = DateTime.UtcNow;
            var status = result.Success ? "EXITOSO" : "FALLIDO";

            // Update final status
            await UpdateStatusInScopeAsync(
                historyId,
                status,
                exitCode: result.ExitCode,
                output: result.Output,
                error: result.Error,
                completedAt: completedAt);

            if (result.Success)
            {
                _logger.LogInformation("ETL job {HistoryId} completed successfully.", historyId);
            }
            else
            {
                _logger.LogWarning("ETL job {HistoryId} failed with exit code {ExitCode}.", historyId, result.ExitCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while executing ETL job {HistoryId}", historyId);

            if (slotAcquired)
            {
                await UpdateStatusInScopeAsync(
                    historyId,
                    "FALLIDO",
                    error: ex.Message,
                    completedAt: DateTime.UtcNow);
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
    /// Creates a scoped service provider and calls UpdateStatusAsync on ETLExecutionHistoryRepository,
    /// ensuring the scoped DbContext is properly disposed after each call.
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
}