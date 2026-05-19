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

    public AuthAuditLogRepository(RefreshTokenDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AuthAuditLog log)
    {
        await _context.AuthAuditLogs.AddAsync(log);
    }

    public async Task<List<AuthAuditLog>> GetByUserIdAsync(int userId, int limit = 50)
    {
        return await _context.AuthAuditLogs
            .Where(log => log.UserId == userId)
            .OrderByDescending(log => log.TimestampUtc)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<AuthAuditLog>> GetByEventTypeAsync(string eventType, int limit = 50)
    {
        return await _context.AuthAuditLogs
            .Where(log => log.EventType == eventType)
            .OrderByDescending(log => log.TimestampUtc)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<AuthAuditLog>> GetByDateRangeAsync(DateTime start, DateTime end, int limit = 100)
    {
        return await _context.AuthAuditLogs
            .Where(log => log.TimestampUtc >= start && log.TimestampUtc <= end)
            .OrderByDescending(log => log.TimestampUtc)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoffDate)
    {
        var oldLogs = await _context.AuthAuditLogs
            .Where(log => log.TimestampUtc < cutoffDate)
            .ToListAsync();

        if (oldLogs.Any())
        {
            _context.AuthAuditLogs.RemoveRange(oldLogs);
            await _context.SaveChangesAsync();
        }

        return oldLogs.Count;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
