using Microsoft.EntityFrameworkCore;
using ServicioRESTEjecucionComandos.Data;
using ServicioRESTEjecucionComandos.Models;

namespace ServicioRESTEjecucionComandos.Repositories;

/// <summary>
/// Repository for RefreshToken operations against the SQLite database.
/// </summary>
public class RefreshTokenRepository
{
    private readonly RefreshTokenDbContext _context;

    public RefreshTokenRepository(RefreshTokenDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(RefreshToken token)
    {
        await _context.RefreshTokens.AddAsync(token);
    }

    public async Task<RefreshToken?> FindByTokenHashAsync(string tokenHash)
    {
        return await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);
    }

    public async Task<List<RefreshToken>> FindByUserIdAsync(int userId)
    {
        return await _context.RefreshTokens
            .Where(rt => rt.UserId == userId)
            .ToListAsync();
    }

    public async Task UpdateAsync(RefreshToken token)
    {
        _context.RefreshTokens.Update(token);
        await Task.CompletedTask;
    }

    public async Task DeleteByUserIdAsync(int userId)
    {
        var tokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId)
            .ToListAsync();
        _context.RefreshTokens.RemoveRange(tokens);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Revokes all refresh tokens for a given user without deleting them.
    /// </summary>
    public async Task<int> RevokeByUserIdAsync(int userId)
    {
        var tokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
            token.RevokedAtUtc = DateTime.UtcNow;
        }

        if (tokens.Any())
        {
            await _context.SaveChangesAsync();
        }

        return tokens.Count;
    }

    public async Task<List<RefreshToken>> FindExpiredAsync(DateTime utcNow)
    {
        return await _context.RefreshTokens
            .Where(rt => rt.ExpiresAtUtc < utcNow && !rt.IsRevoked)
            .ToListAsync();
    }

    public async Task<int> DeleteExpiredAsync(DateTime utcNow)
    {
        var expiredTokens = await _context.RefreshTokens
            .Where(rt => rt.ExpiresAtUtc < utcNow)
            .ToListAsync();

        if (expiredTokens.Any())
        {
            _context.RefreshTokens.RemoveRange(expiredTokens);
            await _context.SaveChangesAsync();
        }

        return expiredTokens.Count;
    }

    public async Task<int> DeleteOldAuditLogsAsync(DateTime cutoffDate)
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
