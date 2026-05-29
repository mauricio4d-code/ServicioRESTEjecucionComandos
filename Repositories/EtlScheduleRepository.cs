using Microsoft.EntityFrameworkCore;
using ServicioRESTEjecucionComandos.Data;
using ServicioRESTEjecucionComandos.Models;

namespace ServicioRESTEjecucionComandos.Repositories;

/// <summary>
/// Repository for CRUD operations on EtlSchedule entities.
/// </summary>
public class EtlScheduleRepository
{
    private readonly ScheduleDbContext _context;

    /// <summary>
    /// Initializes a new instance of EtlScheduleRepository.
    /// </summary>
    public EtlScheduleRepository(ScheduleDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Creates a new EtlSchedule record and saves it to the database.
    /// </summary>
    public async Task<EtlSchedule> CreateAsync(EtlSchedule schedule)
    {
        schedule.Id = Guid.NewGuid();
        schedule.CreatedAt = DateTime.UtcNow;
        schedule.UpdatedAt = DateTime.UtcNow;
        await _context.EtlSchedules.AddAsync(schedule);
        await _context.SaveChangesAsync();
        return schedule;
    }

    /// <summary>
    /// Gets an EtlSchedule record by its Id.
    /// </summary>
    public async Task<EtlSchedule?> GetByIdAsync(Guid id)
    {
        return await _context.EtlSchedules.FindAsync(id);
    }

    /// <summary>
    /// Gets all EtlSchedule records, ordered by creation time descending.
    /// </summary>
    public async Task<List<EtlSchedule>> GetAllAsync()
    {
        return await _context.EtlSchedules.OrderByDescending(x => x.CreatedAt).ToListAsync();
    }

    /// <summary>
    /// Gets all active EtlSchedule records.
    /// </summary>
    public async Task<List<EtlSchedule>> GetActiveAsync()
    {
        return await _context.EtlSchedules.Where(x => x.IsActive).ToListAsync();
    }

    /// <summary>
    /// Updates an existing EtlSchedule record and persists changes.
    /// </summary>
    public async Task UpdateAsync(EtlSchedule schedule)
    {
        schedule.UpdatedAt = DateTime.UtcNow;
        _context.EtlSchedules.Update(schedule);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Deletes an EtlSchedule record by its Id.
    /// </summary>
    public async Task DeleteAsync(Guid id)
    {
        var schedule = await _context.EtlSchedules.FindAsync(id);
        if (schedule == null) return;

        _context.EtlSchedules.Remove(schedule);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Toggles the active state of an EtlSchedule record.
    /// </summary>
    public async Task ToggleActiveAsync(Guid id)
    {
        var schedule = await _context.EtlSchedules.FindAsync(id);
        if (schedule == null) return;

        schedule.IsActive = !schedule.IsActive;
        schedule.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}
