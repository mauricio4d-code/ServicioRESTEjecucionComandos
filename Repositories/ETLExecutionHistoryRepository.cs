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

    /// <summary>
    /// Initializes a new instance of ETLExecutionHistoryRepository.
    /// </summary>
    public ETLExecutionHistoryRepository(ServiceDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Creates a new ETLExecutionHistory record and saves it to the database.
    /// </summary>
    public async Task<ETLExecutionHistory> CreateAsync(ETLExecutionHistory item)
    {
        item.Id = Guid.NewGuid();
        item.Status = "PENDIENTE";
        await _context.ETLExecutionHistories.AddAsync(item);
        await _context.SaveChangesAsync();
        return item;
    }

    /// <summary>
    /// Gets an ETLExecutionHistory record by its Id.
    /// </summary>
    public async Task<ETLExecutionHistory?> GetByIdAsync(Guid id)
    {
        return await _context.ETLExecutionHistories.FindAsync(id);
    }

    /// <summary>
    /// Updates an existing ETLExecutionHistory record and persists changes.
    /// </summary>
    public async Task UpdateAsync(ETLExecutionHistory item)
    {
        _context.ETLExecutionHistories.Update(item);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Updates the status and related fields of an ETLExecutionHistory record atomically.
    /// </summary>
    public async Task UpdateStatusAsync(Guid id, string status, int? exitCode = null, string? output = null, string? error = null, DateTime? executedAt = null, DateTime? completedAt = null)
    {
        var item = await _context.ETLExecutionHistories.FindAsync(id);
        if (item == null) return;

        item.Status = status;
        if (exitCode.HasValue) item.ExitCode = exitCode;
        if (output != null) item.Output = output;
        if (error != null) item.Error = error;
        if (executedAt.HasValue) item.ExecutedAt = executedAt;
        if (completedAt.HasValue) item.CompletedAt = completedAt;

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Gets all ETLExecutionHistory records, ordered by creation time descending.
    /// </summary>
    public async Task<List<ETLExecutionHistory>> GetAllAsync()
    {
        return await _context.ETLExecutionHistories.OrderByDescending(x => x.Id).ToListAsync();
    }

    /// <summary>
    /// Gets ETLExecutionHistory records filtered by status.
    /// </summary>
    public async Task<List<ETLExecutionHistory>> GetByStatusAsync(string status)
    {
        return await _context.ETLExecutionHistories.Where(x => x.Status == status).ToListAsync();
    }
}
