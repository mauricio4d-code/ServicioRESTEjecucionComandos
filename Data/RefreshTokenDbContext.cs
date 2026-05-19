using Microsoft.EntityFrameworkCore;
using ServicioRESTEjecucionComandos.Models;

namespace ServicioRESTEjecucionComandos.Data;

/// <summary>
/// DbContext for the SQLite database storing refresh tokens and audit logs.
/// This database is auto-created on startup.
/// </summary>
public class RefreshTokenDbContext : DbContext
{
    public RefreshTokenDbContext(DbContextOptions<RefreshTokenDbContext> options) : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuthAuditLog> AuthAuditLogs => Set<AuthAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // RefreshToken entity configuration
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.TokenHash).HasColumnName("token_hash").IsRequired().HasMaxLength(64);
            entity.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(e => e.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
            entity.Property(e => e.RevokedAtUtc).HasColumnName("revoked_at_utc");
            entity.Property(e => e.ReplacedByTokenHash).HasColumnName("replaced_by_token_hash").HasMaxLength(64);
            entity.Property(e => e.CreatedByIp).HasColumnName("created_by_ip").HasMaxLength(45);
            entity.Property(e => e.RevokedByIp).HasColumnName("revoked_by_ip").HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasColumnName("user_agent").HasMaxLength(500);
            entity.Property(e => e.IsRevoked).HasColumnName("is_revoked").IsRequired();

            // Indexes for performance
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ExpiresAtUtc);
        });

        // AuthAuditLog entity configuration
        modelBuilder.Entity<AuthAuditLog>(entity =>
        {
            entity.ToTable("auth_audit_logs");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.TimestampUtc).HasColumnName("timestamp_utc").IsRequired();
            entity.Property(e => e.EventType).HasColumnName("event_type").IsRequired().HasMaxLength(50);
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255);
            entity.Property(e => e.ClientIp).HasColumnName("client_ip").HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasColumnName("user_agent").HasMaxLength(500);
            entity.Property(e => e.Message).HasColumnName("message").IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Success).HasColumnName("success").IsRequired();

            // Indexes for querying
            entity.HasIndex(e => e.TimestampUtc);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.EventType);
        });
    }
}