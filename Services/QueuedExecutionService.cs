using Microsoft.Extensions.DependencyInjection;
using ServicioRESTEjecucionComandos.Models;
using ServicioRESTEjecucionComandos.Repositories;

namespace ServicioRESTEjecucionComandos.Services;

/// <summary>
/// Background service that continuously dequeues and processes ExecutionQueueItem instances
/// with configurable parallel execution limits, updating ETLExecutionHistory status in the database.
/// </summary>
public class QueuedExecutionService : BackgroundService
{
    private readonly ExecutionQueue _queue;
    private readonly CommandExecutor _executor;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly int _waitSeconds;
    private readonly ILogger<QueuedExecutionService> _logger;
    private readonly SemaphoreSlim _semaphore;

    /// <summary>
    /// Initializes a new instance of the QueuedExecutionService.
    /// </summary>
    public QueuedExecutionService(
        ExecutionQueue queue,
        CommandExecutor executor,
        IServiceScopeFactory scopeFactory,
        int waitSeconds,
        int maxParallelExecutions,
        ILogger<QueuedExecutionService> logger)
    {
        _queue = queue;
        _executor = executor;
        _scopeFactory = scopeFactory;
        _waitSeconds = waitSeconds;
        _logger = logger;
        _semaphore = new SemaphoreSlim(maxParallelExecutions, maxParallelExecutions);
    }

    /// <summary>
    /// Main execution loop that continuously checks the queue for items to process.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("QueuedExecutionService started. Waiting {WaitSeconds}s between checks. Max parallel executions configured.", _waitSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Only dequeue when an execution slot is available,
                // so that at most MaxParallelExecutions items are ever in RUNNING state.
                if (_semaphore.CurrentCount > 0 && _queue.TryDequeue(out var item) && item != null)
                {
                    _logger.LogInformation("Dequeued item {ItemId} (History {HistoryId}). Acquiring execution slot.", item.Id, item.HistoryId);

                    // Acquire semaphore slot before marking as RUNNING
                    await _semaphore.WaitAsync(stoppingToken);

                    // Update ETLExecutionHistory status to EN PROCESO only after slot is acquired
                    await UpdateStatusInScopeAsync(
                        item.HistoryId,
                        "EN PROCESO",
                        executedAt: DateTime.UtcNow);

                    _logger.LogInformation("Acquired execution slot for item {ItemId}. Starting ProcessItemAsync.", item.Id);

                    // Process the item asynchronously without blocking the dequeue loop
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessItemAsync(item);
                        }
                        finally
                        {
                            // Release semaphore slot when done
                            _semaphore.Release();
                            _logger.LogInformation("Released execution slot after processing item {ItemId}.", item.Id);
                        }
                    }, stoppingToken);
                }
                else
                {
                    // Queue is empty or no execution slots available - wait before checking again
                    _logger.LogDebug("Queue empty or all slots busy. Waiting {WaitSeconds}s before next check.", _waitSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(_waitSeconds), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("QueuedExecutionService received cancellation signal.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in QueuedExecutionService loop. Continuing...");
                await Task.Delay(TimeSpan.FromSeconds(_waitSeconds), stoppingToken);
            }
        }

        _logger.LogInformation("QueuedExecutionService stopped.");
    }

    /// <summary>
    /// Processes a single queue item by executing the command through CommandExecutor
    /// and updating the ETLExecutionHistory record with final status.
    /// </summary>
    /// <param name="item">The queue item to process.</param>
    /// <returns>True if processing succeeded; otherwise, false.</returns>
    public async Task<bool> ProcessItemAsync(ExecutionQueueItem item)
    {
        _logger.LogInformation("Processing item {ItemId}. ETLExecutionHistory status is EN PROCESO.", item.Id);

        try
        {
            var result = await _executor.ExecuteAsync(item);

            var completedAt = DateTime.UtcNow;
            var status = result.Success ? "EXITOSO" : "FALLIDO";

            // Update ETLExecutionHistory with final status and execution details
            await UpdateStatusInScopeAsync(
                item.HistoryId,
                status,
                exitCode: result.ExitCode,
                output: result.Output,
                error: result.Error,
                completedAt: completedAt);

            if (result.Success)
            {
                _logger.LogInformation("Item {ItemId} processed successfully. ETLExecutionHistory status set to EXITOSO.", item.Id);
            }
            else
            {
                _logger.LogWarning("Item {ItemId} processing failed with exit code {ExitCode}. ETLExecutionHistory status set to FALLIDO.", item.Id, result.ExitCode);
            }

            // Update the in-memory item status as well
            item.Status = status;
            item.Result = result.Success ? result.Output : result.Error;
            item.CompletedAt = completedAt;

            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while processing item {ItemId}", item.Id);

            // Mark as FALLIDO in database on unexpected exception
            await UpdateStatusInScopeAsync(
                item.HistoryId,
                "FALLIDO",
                error: ex.Message,
                completedAt: DateTime.UtcNow);

            item.Status = "FALLIDO";
            item.Result = ex.Message;
            item.CompletedAt = DateTime.UtcNow;
            return false;
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