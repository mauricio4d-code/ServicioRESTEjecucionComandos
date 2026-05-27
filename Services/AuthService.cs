using Microsoft.EntityFrameworkCore;
using ServicioRESTEjecucionComandos.Data;
using ServicioRESTEjecucionComandos.DTOs;
using ServicioRESTEjecucionComandos.Interfaces;
using ServicioRESTEjecucionComandos.Models;
using ServicioRESTEjecucionComandos.Repositories;

namespace ServicioRESTEjecucionComandos.Services;

/// <summary>
/// Orchestrates authentication flows: Login, Refresh, Logout.
/// Implements dual logging pattern (ILogger + AuthAuditLog).
/// </summary>
public class AuthService
{
    private readonly AuthDbContext _authDbContext;
    private readonly IPasswordValidator _passwordValidator;
    private readonly JwtService _jwtService;
    private readonly RefreshTokenService _refreshTokenService;
    private readonly AuthAuditLogRepository _auditLogRepository;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AuthDbContext authDbContext,
        IPasswordValidator passwordValidator,
        JwtService jwtService,
        RefreshTokenService refreshTokenService,
        AuthAuditLogRepository auditLogRepository,
        ILogger<AuthService> logger)
    {
        _authDbContext = authDbContext;
        _passwordValidator = passwordValidator;
        _jwtService = jwtService;
        _refreshTokenService = refreshTokenService;
        _auditLogRepository = auditLogRepository;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a user and returns access + refresh tokens.
    /// </summary>
    public async Task<LoginResponse?> LoginAsync(string email, string password, string? clientIp = null, string? userAgent = null)
    {
        // Find user by email with role included
        var user = await _authDbContext.Users
            .Include(u => u.UserRole)
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            _logger.LogWarning("Login attempt for non-existent email: {Email} from IP {ClientIp}", email, clientIp);

            await LogAuditEventAsync("LoginFailed", null, email, clientIp, userAgent,
                $"Login failed: User not found with email {email}", false);

            return null;
        }

        // Validate user state
        if (user.Userstate != "Activo")
        {
            _logger.LogWarning("Login attempt for inactive user: {Email} from IP {ClientIp}", email, clientIp);

            await LogAuditEventAsync("LoginFailed", user.Id, email, clientIp, userAgent,
                $"Login failed: User {email} is not active (state: {user.Userstate})", false);

            return null;
        }

        // Validate password using legacy validator
        var isValid = await _passwordValidator.ValidateAsync(user.Password, password);

        if (!isValid)
        {
            _logger.LogWarning("Invalid password for user: {Email} from IP {ClientIp}", email, clientIp);

            await LogAuditEventAsync("LoginFailed", user.Id, email, clientIp, userAgent,
                $"Login failed: Invalid password for user {email}", false);

            return null;
        }

        // User must have a role
        if (user.UserRole == null)
        {
            _logger.LogWarning("User has no role assigned: {Email}", email);

            await LogAuditEventAsync("LoginFailed", user.Id, email, clientIp, userAgent,
                $"Login failed: User {email} has no role assigned", false);

            return null;
        }

        // Generate JWT access token
        var accessToken = _jwtService.GenerateToken(user, user.UserRole);

        // Generate refresh token
        var refreshToken = await _refreshTokenService.GenerateAsync(user.Id, clientIp, userAgent);

        _logger.LogInformation("Successful login for user: {Email} from IP {ClientIp}", email, clientIp);

        await LogAuditEventAsync("LoginSuccess", user.Id, email, clientIp, userAgent,
            $"Successful login for user {email}", true);

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = _jwtService.GetExpiresInSeconds()
        };
    }

    /// <summary>
    /// Refreshes the access token using a valid refresh token.
    /// </summary>
    public async Task<LoginResponse?> RefreshTokenAsync(string refreshToken, string? clientIp = null, string? userAgent = null)
    {
        var result = await _refreshTokenService.ValidateAndRotateAsync(refreshToken, clientIp, userAgent);

        if (!result.Success)
        {
            _logger.LogWarning("Token refresh failed: {Error} from IP {ClientIp}", result.ErrorMessage, clientIp);

            await LogAuditEventAsync("RefreshFailed", null, null, clientIp, userAgent,
                $"Token refresh failed: {result.ErrorMessage}", false);

            return null;
        }

        // Find user to generate new JWT
        var user = await _authDbContext.Users
            .Include(u => u.UserRole)
            .FirstOrDefaultAsync(u => u.Id == result.UserId);

        if (user == null || user.UserRole == null)
        {
            _logger.LogWarning("User not found during token refresh for userId: {UserId}", result.UserId);

            await LogAuditEventAsync("RefreshFailed", result.UserId, null, clientIp, userAgent,
                $"Token refresh failed: User not found", false);

            return null;
        }

        // Validate user state - deactivated users cannot refresh tokens
        if (user.Userstate != "Activo")
        {
            _logger.LogWarning("Token refresh blocked for inactive user: {UserId} from IP {ClientIp}", result.UserId, clientIp);

            await LogAuditEventAsync("RefreshFailed", result.UserId, user.Email, clientIp, userAgent,
                $"Token refresh blocked: User {user.Email} is not active (state: {user.Userstate})", false);

            return null;
        }

        var accessToken = _jwtService.GenerateToken(user, user.UserRole);

        _logger.LogInformation("Token refresh successful for user: {UserId}", result.UserId);

        await LogAuditEventAsync("RefreshSuccess", result.UserId, user.Email, clientIp, userAgent,
            $"Token refresh successful for user {user.Email}", true);

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = result.NewToken!,
            ExpiresIn = _jwtService.GetExpiresInSeconds()
        };
    }

    /// <summary>
    /// Logs out the user by revoking the refresh token.
    /// </summary>
    public async Task LogoutAsync(string refreshToken, string? clientIp = null)
    {
        var revoked = await _refreshTokenService.RevokeAsync(refreshToken, clientIp);

        if (revoked)
        {
            _logger.LogInformation("Logout successful from IP {ClientIp}", clientIp);

            await LogAuditEventAsync("Logout", null, null, clientIp, null,
                $"Logout successful from IP {clientIp}", true);
        }
        else
        {
            _logger.LogWarning("Logout failed - token not found or already revoked from IP {ClientIp}", clientIp);

            await LogAuditEventAsync("Logout", null, null, clientIp, null,
                $"Logout failed: Token not found or already revoked", false);
        }
    }

    /// <summary>
    /// Logs an authentication event to the audit log.
    /// </summary>
    private async Task LogAuditEventAsync(string eventType, int? userId, string? email, 
        string? clientIp, string? userAgent, string message, bool success)
    {
        var auditLog = new AuthAuditLog
        {
            TimestampUtc = DateTime.UtcNow,
            EventType = eventType,
            UserId = userId,
            Email = email,
            ClientIp = clientIp,
            UserAgent = userAgent,
            Message = message,
            Success = success
        };

        await _auditLogRepository.AddAsync(auditLog);
        await _auditLogRepository.SaveChangesAsync();
    }
}