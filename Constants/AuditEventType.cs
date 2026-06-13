namespace ServicioRESTEjecucionComandos.Constants;

/// <summary>
/// Audit event type values used for authentication and authorization logging.
/// Every auth event must log to both ILogger AND AuthAuditLogRepository.
/// </summary>
public static class AuditEventType
{
    /// <summary>
    /// User successfully logged in.
    /// </summary>
    public const string LoginSuccess = "LoginSuccess";

    /// <summary>
    /// User login attempt failed.
    /// </summary>
    public const string LoginFailed = "LoginFailed";

    /// <summary>
    /// Access token successfully refreshed.
    /// </summary>
    public const string RefreshSuccess = "RefreshSuccess";

    /// <summary>
    /// Token refresh attempt failed.
    /// </summary>
    public const string RefreshFailed = "RefreshFailed";

    /// <summary>
    /// User successfully logged out.
    /// </summary>
    public const string Logout = "Logout";

    /// <summary>
    /// Refresh token was rotated during normal refresh flow.
    /// </summary>
    public const string TokenRotated = "TokenRotated";

    /// <summary>
    /// Refresh token was revoked (e.g., during logout).
    /// </summary>
    public const string TokenRevoked = "TokenRevoked";

    /// <summary>
    /// Multiple refresh tokens were revoked in bulk operation.
    /// </summary>
    public const string BulkTokenRevoked = "BulkTokenRevoked";
}
