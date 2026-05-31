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
    private readonly ILogger<RefreshTokenRepository> _logger;

    public RefreshTokenRepository(RefreshTokenDbContext context, ILogger<RefreshTokenRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task AddAsync(RefreshToken token)
    {
        _logger.LogInformation("Adding new refresh token to database for user {UserId}.", token.UserId);
        await _context.RefreshTokens.AddAsync(token);
        _logger.LogInformation("Refresh token added to database for user {UserId}.", token.UserId);
    }

    public async Task<RefreshToken?> FindByTokenHashAsync(string tokenHash)
    {
        _logger.LogDebug("Querying refresh token from database by token hash.");
        var result = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);
        _logger.LogDebug("Refresh token query by hash returned {Found}.", result != null);
        return result;
    }

    public async Task<List<RefreshToken>> FindByUserIdAsync(int userId)
    {
        _logger.LogDebug("Querying all refresh tokens from database for user {UserId}.", userId);
        var result = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId)
            .ToListAsync();
        _logger.LogDebug("Retrieved {Count} refresh tokens for user {UserId} from database.", result.Count, userId);
        return result;
    }

    public async Task UpdateAsync(RefreshToken token)
    {
        _logger.LogInformation("Updating refresh token in database for user {UserId}.", token.UserId);
        _context.RefreshTokens.Update(token);
        await Task.CompletedTask;
        _logger.LogInformation("Refresh token updated in database for user {UserId}.", token.UserId);
    }

    public async Task DeleteByUserIdAsync(int userId)
    {
        _logger.LogInformation("Deleting all refresh tokens from database for user {UserId}.", userId);
        var tokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId)
            .ToListAsync();
        _context.RefreshTokens.RemoveRange(tokens);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Deleted {Count} refresh tokens from database for user {UserId}.", tokens.Count, userId);
    }

    /// <summary>
    /// Revokes all refresh tokens for a given user without deleting them.
    /// </summary>
    public async Task<int> RevokeByUserIdAsync(int userId)
    {
        _logger.LogInformation("Revoking all active refresh tokens in database for user {UserId}.", userId);
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

        _logger.LogInformation("Revoked {Count} refresh tokens in database for user {UserId}.", tokens.Count, userId);
        return tokens.Count;
    }

    public async Task<List<RefreshToken>> FindExpiredAsync(DateTime utcNow)
    {
        _logger.LogDebug("Querying expired refresh tokens from database (before {UtcNow}).", utcNow);
        var result = await _context.RefreshTokens
            .Where(rt => rt.ExpiresAtUtc < utcNow && !rt.IsRevoked)
            .ToListAsync();
        _logger.LogDebug("Found {Count} expired refresh tokens in database.", result.Count);
        return result;
    }

    public async Task<int> DeleteExpiredAsync(DateTime utcNow)
    {
        _logger.LogInformation("Deleting expired refresh tokens from database (before {UtcNow}).", utcNow);
        var expiredTokens = await _context.RefreshTokens
            .Where(rt => rt.ExpiresAtUtc < utcNow)
            .ToListAsync();

        if (expiredTokens.Any())
        {
            _context.RefreshTokens.RemoveRange(expiredTokens);
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("Deleted {Count} expired refresh tokens from database.", expiredTokens.Count);
        return expiredTokens.Count;
    }

    public async Task<int> DeleteOldAuditLogsAsync(DateTime cutoffDate)
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
        _logger.LogDebug("Persisting pending changes to SQLite database.");
        await _context.SaveChangesAsync();
        _logger.LogDebug("Changes persisted to SQLite database successfully.");
    }
}
