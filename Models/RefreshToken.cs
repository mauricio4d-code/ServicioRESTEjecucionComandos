using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ServicioRESTEjecucionComandos.Models;

/// <summary>
/// Refresh Token entity stored in SQLite database.
/// Tokens are hashed with SHA256 before storage.
/// </summary>
[Table("refresh_tokens")]
public class RefreshToken
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("user_id")]
    public int UserId { get; set; }

    /// <summary>
    /// SHA256 hash of the original refresh token.
    /// </summary>
    [Required]
    [Column("token_hash")]
    public string TokenHash { get; set; } = string.Empty;

    [Required]
    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }

    [Required]
    [Column("expires_at_utc")]
    public DateTime ExpiresAtUtc { get; set; }

    [Column("revoked_at_utc")]
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>
    /// Hash of the token that replaced this one (for rotation tracking).
    /// </summary>
    [Column("replaced_by_token_hash")]
    public string? ReplacedByTokenHash { get; set; }

    [Column("created_by_ip")]
    public string? CreatedByIp { get; set; }

    [Column("revoked_by_ip")]
    public string? RevokedByIp { get; set; }

    [Column("user_agent")]
    public string? UserAgent { get; set; }

    [Required]
    [Column("is_revoked")]
    public bool IsRevoked { get; set; }
}
