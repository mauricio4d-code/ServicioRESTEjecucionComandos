using System.Collections.Concurrent;
using ServicioRESTEjecucionComandos.Models;

namespace ServicioRESTEjecucionComandos.Services;

/// <summary>
/// Thread-safe queue for managing ExecutionQueueItem instances.
/// </summary>
public class ExecutionQueue
{
    private readonly ConcurrentQueue<ExecutionQueueItem> _queue = new();

    /// <summary>
    /// Enqueues a new execution item and returns its unique identifier.
    /// </summary>
    /// <param name="item">The item to enqueue.</param>
    /// <returns>The unique identifier (Guid) of the enqueued item.</returns>
    public Guid Enqueue(ExecutionQueueItem item)
    {
        item.Id = Guid.NewGuid();
        item.Status = "PENDIENTE";
        item.CreatedAt = DateTime.UtcNow;
        _queue.Enqueue(item);
        return item.Id;
    }

    /// <summary>
    /// Attempts to dequeue an item from the queue.
    /// </summary>
    /// <param name="item">The dequeued item, or null if the queue is empty.</param>
    /// <returns>True if an item was dequeued; otherwise, false.</returns>
    public bool TryDequeue(out ExecutionQueueItem? item)
    {
        return _queue.TryDequeue(out item);
    }

    /// <summary>
    /// Gets the current number of items in the queue.
    /// </summary>
    public int Count => _queue.Count;
}