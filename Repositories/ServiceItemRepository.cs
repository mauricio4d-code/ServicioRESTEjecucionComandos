using Microsoft.EntityFrameworkCore;
using ServicioRESTEjecucionComandos.Data;
using ServicioRESTEjecucionComandos.Models;

namespace ServicioRESTEjecucionComandos.Repositories;

/// <summary>
/// Repository for CRUD operations on ServiceItem entities.
/// </summary>
public class ServiceItemRepository
{
    private readonly ServiceDbContext _context;

    /// <summary>
    /// Initializes a new instance of ServiceItemRepository.
    /// </summary>
    public ServiceItemRepository(ServiceDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Creates a new ServiceItem and saves it to the database.
    /// </summary>
    public async Task<ServiceItem> CreateAsync(ServiceItem item)
    {
        item.ItemId = Guid.NewGuid();
        item.Status = "PENDING";
        await _context.ServiceItems.AddAsync(item);
        await _context.SaveChangesAsync();
        return item;
    }

    /// <summary>
    /// Gets a ServiceItem by its ItemId.
    /// </summary>
    public async Task<ServiceItem?> GetByIdAsync(Guid itemId)
    {
        return await _context.ServiceItems.FindAsync(itemId);
    }

    /// <summary>
    /// Updates an existing ServiceItem and persists changes.
    /// </summary>
    public async Task UpdateAsync(ServiceItem item)
    {
        _context.ServiceItems.Update(item);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Updates the status and related fields of a ServiceItem atomically.
    /// </summary>
    public async Task UpdateStatusAsync(Guid itemId, string status, int? exitCode = null, string? output = null, string? error = null, DateTime? executedAt = null, DateTime? completedAt = null)
    {
        var item = await _context.ServiceItems.FindAsync(itemId);
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
    /// Gets all ServiceItems, ordered by creation time descending.
    /// </summary>
    public async Task<List<ServiceItem>> GetAllAsync()
    {
        return await _context.ServiceItems.OrderByDescending(x => x.ItemId).ToListAsync();
    }

    /// <summary>
    /// Gets ServiceItems filtered by status.
    /// </summary>
    public async Task<List<ServiceItem>> GetByStatusAsync(string status)
    {
        return await _context.ServiceItems.Where(x => x.Status == status).ToListAsync();
    }
}
