using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ServicioRESTEjecucionComandos.Models;

/// <summary>
/// Audit log entry for authentication events stored in SQLite.
/// Provides persistent audit trail for compliance and investigation.
/// </summary>
[Table("auth_audit_logs")]
public class AuthAuditLog
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [Column("timestamp_utc")]
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// Event type: LoginSuccess, LoginFailed, Logout, RefreshSuccess, RefreshFailed,
    /// TokenRevoked, TokenRotated, UnauthorizedAccess
    /// </summary>
    [Required]
    [Column("event_type")]
    public string EventType { get; set; } = string.Empty;

    [Column("user_id")]
    public int? UserId { get; set; }

    [Column("email")]
    public string? Email { get; set; }

    [Column("client_ip")]
    public string? ClientIp { get; set; }

    [Column("user_agent")]
    public string? UserAgent { get; set; }

    [Required]
    [Column("message")]
    public string Message { get; set; } = string.Empty;

    [Required]
    [Column("success")]
    public bool Success { get; set; }
}
