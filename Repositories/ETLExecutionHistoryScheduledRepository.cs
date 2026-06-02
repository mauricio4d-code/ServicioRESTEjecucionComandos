using Microsoft.EntityFrameworkCore;
using ServicioRESTEjecucionComandos.Data;
using ServicioRESTEjecucionComandos.Models;

namespace ServicioRESTEjecucionComandos.Repositories;

/// <summary>
/// Repository for CRUD operations on ETLExecutionHistoryScheduled entities.
/// </summary>
public class ETLExecutionHistoryScheduledRepository
{
    private readonly ServiceDbContext _context;
    private readonly ILogger<ETLExecutionHistoryScheduledRepository> _logger;

    /// <summary>
    /// Initializes a new instance of ETLExecutionHistoryScheduledRepository.
    /// </summary>
    public ETLExecutionHistoryScheduledRepository(ServiceDbContext context, ILogger<ETLExecutionHistoryScheduledRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new ETLExecutionHistoryScheduled record and saves it to the database.
    /// </summary>
    public async Task<ETLExecutionHistoryScheduled> CreateAsync(ETLExecutionHistoryScheduled item)
    {
        item.Id = Guid.NewGuid();
        item.Status = "PENDIENTE";
        item.CreatedAt = DateTime.UtcNow;
        _logger.LogInformation("Creating new ETLExecutionHistoryScheduled record in database for ScheduleId {ScheduleId}.", item.ScheduleId);
        await _context.ETLExecutionHistoryScheduleds.AddAsync(item);
        await _context.SaveChangesAsync();
        _logger.LogInformation("ETLExecutionHistoryScheduled record created in database with Id {HistoryId} and status PENDIENTE.", item.Id);
        return item;
    }

    /// <summary>
    /// Gets an ETLExecutionHistoryScheduled record by its Id.
    /// </summary>
    public async Task<ETLExecutionHistoryScheduled?> GetByIdAsync(Guid id)
    {
        _logger.LogDebug("Querying ETLExecutionHistoryScheduled from database by Id {HistoryId}.", id);
        var result = await _context.ETLExecutionHistoryScheduleds.FindAsync(id);
        _logger.LogDebug("ETLExecutionHistoryScheduled query by Id {HistoryId} returned {Found}.", id, result != null);
        return result;
    }

    /// <summary>
    /// Gets all ETLExecutionHistoryScheduled records, ordered by creation time descending.
    /// </summary>
    public async Task<List<ETLExecutionHistoryScheduled>> GetAllAsync()
    {
        _logger.LogDebug("Querying all ETLExecutionHistoryScheduled records from database.");
        var result = await _context.ETLExecutionHistoryScheduleds.OrderByDescending(x => x.CreatedAt).ToListAsync();
        _logger.LogDebug("Retrieved {Count} ETLExecutionHistoryScheduled records from database.", result.Count);
        return result;
    }

    /// <summary>
    /// Gets ETLExecutionHistoryScheduled records filtered by status.
    /// </summary>
    public async Task<List<ETLExecutionHistoryScheduled>> GetByStatusAsync(string status)
    {
        _logger.LogDebug("Querying ETLExecutionHistoryScheduled records from database filtered by status {Status}.", status);
        var result = await _context.ETLExecutionHistoryScheduleds.Where(x => x.Status == status).ToListAsync();
        _logger.LogDebug("Retrieved {Count} ETLExecutionHistoryScheduled records with status {Status} from database.", result.Count, status);
        return result;
    }

    /// <summary>
    /// Updates the status and related fields of an ETLExecutionHistoryScheduled record atomically.
    /// </summary>
    public async Task UpdateStatusAsync(
        Guid id,
        string status,
        int? exitCode = null,
        string? output = null,
        string? error = null,
        DateTime? executedAt = null,
        DateTime? completedAt = null)
    {
        _logger.LogInformation("Updating status to {Status} for ETLExecutionHistoryScheduled in database with Id {HistoryId}.", status, id);
        var item = await _context.ETLExecutionHistoryScheduleds.FindAsync(id);
        if (item == null)
        {
            _logger.LogWarning("Cannot update status: ETLExecutionHistoryScheduled not found with Id {HistoryId}.", id);
            return;
        }

        item.Status = status;
        if (exitCode.HasValue) item.ExitCode = exitCode;
        if (output != null) item.Output = output;
        if (error != null) item.Error = error;
        if (executedAt.HasValue) item.ExecutedAt = executedAt;
        if (completedAt.HasValue) item.CompletedAt = completedAt;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Status updated to {Status} for ETLExecutionHistoryScheduled in database with Id {HistoryId}.", status, id);
    }
}
