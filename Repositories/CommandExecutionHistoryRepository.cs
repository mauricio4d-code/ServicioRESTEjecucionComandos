using Microsoft.EntityFrameworkCore;
using ServicioRESTEjecucionComandos.Data;
using ServicioRESTEjecucionComandos.Models;

namespace ServicioRESTEjecucionComandos.Repositories;

/// <summary>
/// Repository for CRUD operations on CommandExecutionHistory entities.
/// </summary>
public class CommandExecutionHistoryRepository
{
    private readonly ServiceDbContext _context;

    /// <summary>
    /// Initializes a new instance of CommandExecutionHistoryRepository.
    /// </summary>
    public CommandExecutionHistoryRepository(ServiceDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Creates a new CommandExecutionHistory record and saves it to the database.
    /// </summary>
    public async Task<CommandExecutionHistory> CreateAsync(CommandExecutionHistory item)
    {
        item.Id = Guid.NewGuid();
        item.Status = "PENDIENTE";
        await _context.CommandExecutionHistories.AddAsync(item);
        await _context.SaveChangesAsync();
        return item;
    }

    /// <summary>
    /// Gets a CommandExecutionHistory record by its Id.
    /// </summary>
    public async Task<CommandExecutionHistory?> GetByIdAsync(Guid id)
    {
        return await _context.CommandExecutionHistories.FindAsync(id);
    }

    /// <summary>
    /// Updates an existing CommandExecutionHistory record and persists changes.
    /// </summary>
    public async Task UpdateAsync(CommandExecutionHistory item)
    {
        _context.CommandExecutionHistories.Update(item);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Updates the status and related fields of a CommandExecutionHistory record atomically.
    /// </summary>
    public async Task UpdateStatusAsync(Guid id, string status, int? exitCode = null, string? output = null, string? error = null, DateTime? executedAt = null, DateTime? completedAt = null)
    {
        var item = await _context.CommandExecutionHistories.FindAsync(id);
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
    /// Gets all CommandExecutionHistory records, ordered by creation time descending.
    /// </summary>
    public async Task<List<CommandExecutionHistory>> GetAllAsync()
    {
        return await _context.CommandExecutionHistories.OrderByDescending(x => x.Id).ToListAsync();
    }

    /// <summary>
    /// Gets CommandExecutionHistory records filtered by status.
    /// </summary>
    public async Task<List<CommandExecutionHistory>> GetByStatusAsync(string status)
    {
        return await _context.CommandExecutionHistories.Where(x => x.Status == status).ToListAsync();
    }
}
