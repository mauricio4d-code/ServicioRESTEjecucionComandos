using ServicioRESTEjecucionComandos.Models;

namespace ServicioRESTEjecucionComandos.Services;

/// <summary>
/// Background service that continuously dequeues and processes ExecutionQueueItem instances
/// with configurable parallel execution limits.
/// </summary>
public class QueuedExecutionService : BackgroundService
{
    private readonly ExecutionQueue _queue;
    private readonly CommandExecutor _executor;
    private readonly int _waitSeconds;
    private readonly ILogger<QueuedExecutionService> _logger;
    private readonly SemaphoreSlim _semaphore;

    /// <summary>
    /// Initializes a new instance of the QueuedExecutionService.
    /// </summary>
    public QueuedExecutionService(
        ExecutionQueue queue,
        CommandExecutor executor,
        int waitSeconds,
        int maxParallelExecutions,
        ILogger<QueuedExecutionService> logger)
    {
        _queue = queue;
        _executor = executor;
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
                if (_queue.TryDequeue(out var item) && item != null)
                {
                    _logger.LogInformation("Dequeued item {ItemId}. Attempting to acquire execution slot.", item.Id);

                    // Acquire semaphore slot - limits parallel executions
                    await _semaphore.WaitAsync(stoppingToken);

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
                    // Queue is empty - wait for configured duration before checking again
                    _logger.LogDebug("Queue empty. Waiting {WaitSeconds}s before next check.", _waitSeconds);
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
    /// Processes a single queue item by executing the command through CommandExecutor.
    /// </summary>
    /// <param name="item">The queue item to process.</param>
    /// <returns>True if processing succeeded; otherwise, false.</returns>
    public async Task<bool> ProcessItemAsync(ExecutionQueueItem item)
    {
        _logger.LogInformation("Processing item {ItemId}. Setting status to Running.", item.Id);
        item.Status = "Running";

        try
        {
            var success = await _executor.ExecuteAsync(item);

            if (success)
            {
                _logger.LogInformation("Item {ItemId} processed successfully.", item.Id);
            }
            else
            {
                _logger.LogWarning("Item {ItemId} processing failed. Status: {Status}", item.Id, item.Status);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while processing item {ItemId}", item.Id);
            item.Status = "Error";
            item.Result = ex.Message;
            item.CompletedAt = DateTime.UtcNow;
            return false;
        }
    }
}