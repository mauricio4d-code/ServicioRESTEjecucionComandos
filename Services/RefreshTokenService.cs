using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ServicioRESTEjecucionComandos.Data;
using ServicioRESTEjecucionComandos.Models;
using ServicioRESTEjecucionComandos.Repositories;

namespace ServicioRESTEjecucionComandos.Services;

/// <summary>
/// Result of a refresh token validation and rotation operation.
/// </summary>
public class RefreshTokenResult
{
    public bool Success { get; set; }
    public string? NewToken { get; set; }
    public int? UserId { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Service for generating, validating, rotating, and revoking refresh tokens.
/// </summary>
public class RefreshTokenService
{
    private readonly RefreshTokenRepository _repository;
    private readonly AuthAuditLogRepository _auditLogRepository;
    private readonly ILogger<RefreshTokenService> _logger;
    private readonly int _refreshTokenDays;

    public RefreshTokenService(
        RefreshTokenRepository repository,
        AuthAuditLogRepository auditLogRepository,
        ILogger<RefreshTokenService> logger,
        IConfiguration configuration)
    {
        _repository = repository;
        _auditLogRepository = auditLogRepository;
        _logger = logger;
        _refreshTokenDays = configuration.GetValue<int>("Jwt:RefreshTokenDays", 30);
    }

    /// <summary>
    /// Generates a new refresh token for the given user.
    /// </summary>
    public async Task<string> GenerateAsync(int userId, string? clientIp = null, string? userAgent = null)
    {
        // Generate cryptographically secure random token
        var randomBytes = new byte[64];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        var token = Convert.ToBase64String(randomBytes);

        // Hash the token with SHA256 before storage
        var tokenHash = ComputeSha256Hash(token);

        var refreshToken = new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_refreshTokenDays),
            CreatedByIp = clientIp,
            UserAgent = userAgent,
            IsRevoked = false
        };

        await _repository.AddAsync(refreshToken);
        await _repository.SaveChangesAsync();

        _logger.LogInformation("Refresh token generated for user {UserId}", userId);

        return token;
    }

    /// <summary>
    /// Validates and rotates a refresh token. Returns a new token if valid.
    /// Implements token rotation to detect replay attacks.
    /// </summary>
    public async Task<RefreshTokenResult> ValidateAndRotateAsync(string token, string? clientIp = null, string? userAgent = null)
    {
        var tokenHash = ComputeSha256Hash(token);
        var storedToken = await _repository.FindByTokenHashAsync(tokenHash);

        if (storedToken == null)
        {
            _logger.LogWarning("Refresh token not found. Possible replay attack from IP {ClientIp}", clientIp);
            return new RefreshTokenResult
            {
                Success = false,
                ErrorMessage = "Refresh token not found."
            };
        }

        if (storedToken.IsRevoked)
        {
            _logger.LogWarning("Revoked refresh token used by user {UserId} from IP {ClientIp}", storedToken.UserId, clientIp);
            return new RefreshTokenResult
            {
                Success = false,
                ErrorMessage = "Refresh token has been revoked."
            };
        }

        if (storedToken.ExpiresAtUtc < DateTime.UtcNow)
        {
            _logger.LogWarning("Expired refresh token used by user {UserId} from IP {ClientIp}", storedToken.UserId, clientIp);
            return new RefreshTokenResult
            {
                Success = false,
                ErrorMessage = "Refresh token has expired."
            };
        }

        // Mark old token as revoked
        storedToken.IsRevoked = true;
        storedToken.RevokedAtUtc = DateTime.UtcNow;
        storedToken.RevokedByIp = clientIp;
        await _repository.UpdateAsync(storedToken);

        // Generate new token (rotation)
        var newToken = await GenerateAsync(storedToken.UserId, clientIp, userAgent);
        var newTokenHash = ComputeSha256Hash(newToken);

        // Update replaced_by reference
        storedToken.ReplacedByTokenHash = newTokenHash;
        await _repository.UpdateAsync(storedToken);
        await _repository.SaveChangesAsync();

        _logger.LogInformation("Refresh token rotated for user {UserId}", storedToken.UserId);

        // Log audit event
        var auditLog = new AuthAuditLog
        {
            TimestampUtc = DateTime.UtcNow,
            EventType = "TokenRotated",
            UserId = storedToken.UserId,
            ClientIp = clientIp,
            UserAgent = userAgent,
            Message = $"Refresh token rotated for user {storedToken.UserId}",
            Success = true
        };
        await _auditLogRepository.AddAsync(auditLog);
        await _auditLogRepository.SaveChangesAsync();

        return new RefreshTokenResult
        {
            Success = true,
            NewToken = newToken,
            UserId = storedToken.UserId
        };
    }

    /// <summary>
    /// Revokes a refresh token.
    /// </summary>
    public async Task<bool> RevokeAsync(string token, string? clientIp = null)
    {
        var tokenHash = ComputeSha256Hash(token);
        var storedToken = await _repository.FindByTokenHashAsync(tokenHash);

        if (storedToken == null || storedToken.IsRevoked)
        {
            return false;
        }

        storedToken.IsRevoked = true;
        storedToken.RevokedAtUtc = DateTime.UtcNow;
        storedToken.RevokedByIp = clientIp;
        await _repository.UpdateAsync(storedToken);
        await _repository.SaveChangesAsync();

        _logger.LogInformation("Refresh token revoked for user {UserId}", storedToken.UserId);

        // Log audit event
        var auditLog = new AuthAuditLog
        {
            TimestampUtc = DateTime.UtcNow,
            EventType = "TokenRevoked",
            UserId = storedToken.UserId,
            ClientIp = clientIp,
            Message = $"Refresh token revoked for user {storedToken.UserId}",
            Success = true
        };
        await _auditLogRepository.AddAsync(auditLog);
        await _auditLogRepository.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Revokes all refresh tokens for a given user.
    /// </summary>
    public async Task RevokeAllForUserAsync(int userId)
    {
        await _repository.DeleteByUserIdAsync(userId);
        _logger.LogInformation("All refresh tokens revoked for user {UserId}", userId);
    }

    /// <summary>
    /// Computes SHA256 hash of the input string.
    /// </summary>
    private static string ComputeSha256Hash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        var builder = new StringBuilder();
        foreach (var b in bytes)
        {
            builder.Append(b.ToString("x2"));
        }
        return builder.ToString();
    }
}
