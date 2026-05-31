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
    private readonly ILogger<EtlScheduleRepository> _logger;

    /// <summary>
    /// Initializes a new instance of EtlScheduleRepository.
    /// </summary>
    public EtlScheduleRepository(ScheduleDbContext context, ILogger<EtlScheduleRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new EtlSchedule record and saves it to the database.
    /// </summary>
    public async Task<EtlSchedule> CreateAsync(EtlSchedule schedule)
    {
        schedule.Id = Guid.NewGuid();
        schedule.CreatedAt = DateTime.UtcNow;
        schedule.UpdatedAt = DateTime.UtcNow;
        _logger.LogInformation("Creating new ETL schedule in database for code {Codigo}, type {TipoEntidad}.", schedule.Codigo, schedule.TipoEntidad);
        await _context.EtlSchedules.AddAsync(schedule);
        await _context.SaveChangesAsync();
        _logger.LogInformation("ETL schedule created successfully in database with Id {ScheduleId}.", schedule.Id);
        return schedule;
    }

    /// <summary>
    /// Gets an EtlSchedule record by its Id.
    /// </summary>
    public async Task<EtlSchedule?> GetByIdAsync(Guid id)
    {
        _logger.LogDebug("Querying ETL schedule from database by Id {ScheduleId}.", id);
        var result = await _context.EtlSchedules.FindAsync(id);
        _logger.LogDebug("ETL schedule query by Id {ScheduleId} returned {Found}.", id, result != null);
        return result;
    }

    /// <summary>
    /// Gets all EtlSchedule records, ordered by creation time descending.
    /// </summary>
    public async Task<List<EtlSchedule>> GetAllAsync()
    {
        _logger.LogDebug("Querying all ETL schedules from database.");
        var result = await _context.EtlSchedules.OrderByDescending(x => x.CreatedAt).ToListAsync();
        _logger.LogDebug("Retrieved {Count} ETL schedules from database.", result.Count);
        return result;
    }

    /// <summary>
    /// Gets all active EtlSchedule records.
    /// </summary>
    public async Task<List<EtlSchedule>> GetActiveAsync()
    {
        _logger.LogDebug("Querying active ETL schedules from database.");
        var result = await _context.EtlSchedules.Where(x => x.IsActive).ToListAsync();
        _logger.LogDebug("Retrieved {Count} active ETL schedules from database.", result.Count);
        return result;
    }

    /// <summary>
    /// Updates an existing EtlSchedule record and persists changes.
    /// </summary>
    public async Task UpdateAsync(EtlSchedule schedule)
    {
        _logger.LogInformation("Updating ETL schedule in database with Id {ScheduleId}.", schedule.Id);
        schedule.UpdatedAt = DateTime.UtcNow;
        _context.EtlSchedules.Update(schedule);
        await _context.SaveChangesAsync();
        _logger.LogInformation("ETL schedule updated successfully in database with Id {ScheduleId}.", schedule.Id);
    }

    /// <summary>
    /// Deletes an EtlSchedule record by its Id.
    /// </summary>
    public async Task DeleteAsync(Guid id)
    {
        _logger.LogInformation("Deleting ETL schedule from database with Id {ScheduleId}.", id);
        var schedule = await _context.EtlSchedules.FindAsync(id);
        if (schedule == null)
        {
            _logger.LogWarning("Cannot delete ETL schedule: no record found with Id {ScheduleId}.", id);
            return;
        }

        _context.EtlSchedules.Remove(schedule);
        await _context.SaveChangesAsync();
        _logger.LogInformation("ETL schedule deleted successfully from database with Id {ScheduleId}.", id);
    }

    /// <summary>
    /// Toggles the active state of an EtlSchedule record.
    /// </summary>
    public async Task ToggleActiveAsync(Guid id)
    {
        _logger.LogInformation("Toggling active state for ETL schedule in database with Id {ScheduleId}.", id);
        var schedule = await _context.EtlSchedules.FindAsync(id);
        if (schedule == null)
        {
            _logger.LogWarning("Cannot toggle ETL schedule: no record found with Id {ScheduleId}.", id);
            return;
        }

        schedule.IsActive = !schedule.IsActive;
        schedule.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _logger.LogInformation("ETL schedule active state toggled to {IsActive} in database for Id {ScheduleId}.", schedule.IsActive, id);
    }
}
