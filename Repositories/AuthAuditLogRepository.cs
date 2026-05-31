using Microsoft.EntityFrameworkCore;
using ServicioRESTEjecucionComandos.Data;
using ServicioRESTEjecucionComandos.Models;

namespace ServicioRESTEjecucionComandos.Repositories;

/// <summary>
/// Repository for AuthAuditLog operations against the SQLite database.
/// </summary>
public class AuthAuditLogRepository
{
    private readonly RefreshTokenDbContext _context;
    private readonly ILogger<AuthAuditLogRepository> _logger;

    public AuthAuditLogRepository(RefreshTokenDbContext context, ILogger<AuthAuditLogRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task AddAsync(AuthAuditLog log)
    {
        _logger.LogInformation("Adding new auth audit log to database for event {EventType}, user {UserId}.", log.EventType, log.UserId);
        await _context.AuthAuditLogs.AddAsync(log);
        _logger.LogInformation("Auth audit log added to database for event {EventType}.", log.EventType);
    }

    public async Task<List<AuthAuditLog>> GetByUserIdAsync(int userId, int limit = 50)
    {
        _logger.LogDebug("Querying auth audit logs from database for user {UserId}.", userId);
        var result = await _context.AuthAuditLogs
            .Where(log => log.UserId == userId)
            .OrderByDescending(log => log.TimestampUtc)
            .Take(limit)
            .ToListAsync();
        _logger.LogDebug("Retrieved {Count} auth audit logs for user {UserId} from database.", result.Count, userId);
        return result;
    }

    public async Task<List<AuthAuditLog>> GetByEventTypeAsync(string eventType, int limit = 50)
    {
        _logger.LogDebug("Querying auth audit logs from database filtered by event type {EventType}.", eventType);
        var result = await _context.AuthAuditLogs
            .Where(log => log.EventType == eventType)
            .OrderByDescending(log => log.TimestampUtc)
            .Take(limit)
            .ToListAsync();
        _logger.LogDebug("Retrieved {Count} auth audit logs with event type {EventType} from database.", result.Count, eventType);
        return result;
    }

    public async Task<List<AuthAuditLog>> GetByDateRangeAsync(DateTime start, DateTime end, int limit = 100)
    {
        _logger.LogDebug("Querying auth audit logs from database for date range {Start} to {End}.", start, end);
        var result = await _context.AuthAuditLogs
            .Where(log => log.TimestampUtc >= start && log.TimestampUtc <= end)
            .OrderByDescending(log => log.TimestampUtc)
            .Take(limit)
            .ToListAsync();
        _logger.LogDebug("Retrieved {Count} auth audit logs for date range from database.", result.Count);
        return result;
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoffDate)
    {
        _logger.LogInformation("Deleting old auth audit logs from database (before {CutoffDate}).", cutoffDate);
        var oldLogs = await _context.AuthAuditLogs
            .Where(log => log.TimestampUtc < cutoffDate)
            .ToListAsync();

        if (oldLogs.Any())
        {
            _context.AuthAuditLogs.RemoveRange(oldLogs);
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("Deleted {Count} old auth audit logs from database.", oldLogs.Count);
        return oldLogs.Count;
    }

    public async Task SaveChangesAsync()
    {
        _logger.LogDebug("Persisting pending changes to auth audit logs in SQLite database.");
        await _context.SaveChangesAsync();
        _logger.LogDebug("Changes persisted to auth audit logs in SQLite database successfully.");
    }
}
