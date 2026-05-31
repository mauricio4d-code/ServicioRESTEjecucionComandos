using Microsoft.EntityFrameworkCore;
using ServicioRESTEjecucionComandos.Data;
using ServicioRESTEjecucionComandos.Models;

namespace ServicioRESTEjecucionComandos.Repositories;

/// <summary>
/// Repository for CRUD operations on ETLExecutionHistory entities.
/// </summary>
public class ETLExecutionHistoryRepository
{
    private readonly ServiceDbContext _context;
    private readonly ILogger<ETLExecutionHistoryRepository> _logger;

    /// <summary>
    /// Initializes a new instance of ETLExecutionHistoryRepository.
    /// </summary>
    public ETLExecutionHistoryRepository(ServiceDbContext context, ILogger<ETLExecutionHistoryRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new ETLExecutionHistory record and saves it to the database.
    /// </summary>
    public async Task<ETLExecutionHistory> CreateAsync(ETLExecutionHistory item)
    {
        item.Id = Guid.NewGuid();
        item.Status = "PENDIENTE";
        _logger.LogInformation("Creating new ETLExecutionHistory record in database for Codigo {Codigo}, CodEnvio {CodEnvio}.", item.Codigo, item.CodEnvio);
        await _context.ETLExecutionHistories.AddAsync(item);
        await _context.SaveChangesAsync();
        _logger.LogInformation("ETLExecutionHistory record created in database with Id {HistoryId} and status PENDIENTE.", item.Id);
        return item;
    }

    /// <summary>
    /// Gets an ETLExecutionHistory record by its Id.
    /// </summary>
    public async Task<ETLExecutionHistory?> GetByIdAsync(Guid id)
    {
        _logger.LogDebug("Querying ETLExecutionHistory from database by Id {HistoryId}.", id);
        var result = await _context.ETLExecutionHistories.FindAsync(id);
        _logger.LogDebug("ETLExecutionHistory query by Id {HistoryId} returned {Found}.", id, result != null);
        return result;
    }

    /// <summary>
    /// Updates an existing ETLExecutionHistory record and persists changes.
    /// </summary>
    public async Task UpdateAsync(ETLExecutionHistory item)
    {
        _logger.LogInformation("Updating ETLExecutionHistory record in database with Id {HistoryId}.", item.Id);
        _context.ETLExecutionHistories.Update(item);
        await _context.SaveChangesAsync();
        _logger.LogInformation("ETLExecutionHistory record updated in database with Id {HistoryId}.", item.Id);
    }

    /// <summary>
    /// Updates the status and related fields of an ETLExecutionHistory record atomically.
    /// </summary>
    public async Task UpdateStatusAsync(Guid id, string status, int? exitCode = null, string? output = null, string? error = null, DateTime? executedAt = null, DateTime? completedAt = null)
    {
        _logger.LogInformation("Updating status to {Status} for ETLExecutionHistory in database with Id {HistoryId}.", status, id);
        var item = await _context.ETLExecutionHistories.FindAsync(id);
        if (item == null)
        {
            _logger.LogWarning("Cannot update status: ETLExecutionHistory not found with Id {HistoryId}.", id);
            return;
        }

        item.Status = status;
        if (exitCode.HasValue) item.ExitCode = exitCode;
        if (output != null) item.Output = output;
        if (error != null) item.Error = error;
        if (executedAt.HasValue) item.ExecutedAt = executedAt;
        if (completedAt.HasValue) item.CompletedAt = completedAt;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Status updated to {Status} for ETLExecutionHistory in database with Id {HistoryId}.", status, id);
    }

    /// <summary>
    /// Gets all ETLExecutionHistory records, ordered by creation time descending.
    /// </summary>
    public async Task<List<ETLExecutionHistory>> GetAllAsync()
    {
        _logger.LogDebug("Querying all ETLExecutionHistory records from database.");
        var result = await _context.ETLExecutionHistories.OrderByDescending(x => x.Id).ToListAsync();
        _logger.LogDebug("Retrieved {Count} ETLExecutionHistory records from database.", result.Count);
        return result;
    }

    /// <summary>
    /// Gets ETLExecutionHistory records filtered by status.
    /// </summary>
    public async Task<List<ETLExecutionHistory>> GetByStatusAsync(string status)
    {
        _logger.LogDebug("Querying ETLExecutionHistory records from database filtered by status {Status}.", status);
        var result = await _context.ETLExecutionHistories.Where(x => x.Status == status).ToListAsync();
        _logger.LogDebug("Retrieved {Count} ETLExecutionHistory records with status {Status} from database.", result.Count, status);
        return result;
    }

    /// <summary>
    /// Gets the first active (PENDIENTE or EN PROCESO) execution for the given CodEnvio and Codigo, if any.
    /// </summary>
    public async Task<ETLExecutionHistory?> GetActiveExecutionAsync(string codEnvio, string codigo)
    {
        _logger.LogDebug("Querying active ETLExecutionHistory from database for CodEnvio {CodEnvio}, Codigo {Codigo}.", codEnvio, codigo);
        var result = await _context.ETLExecutionHistories
            .FirstOrDefaultAsync(x => x.CodEnvio == codEnvio
                && x.Codigo == codigo
                && (x.Status == "PENDIENTE" || x.Status == "EN PROCESO"));
        _logger.LogDebug("Active ETLExecutionHistory query for CodEnvio {CodEnvio}, Codigo {Codigo} returned {Found}.", codEnvio, codigo, result != null);
        return result;
    }
}
