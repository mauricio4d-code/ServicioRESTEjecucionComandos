using Microsoft.EntityFrameworkCore;
using ServicioRESTEjecucionComandos.Models;

namespace ServicioRESTEjecucionComandos.Data;

/// <summary>
/// DbContext for the legacy authentication database (PostgreSQL/SQLServer/SQLite).
/// Maps to existing "user" and "userrole" tables without migrations.
/// </summary>
public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User entity configuration - maps to legacy "user" table
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("user");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(255);
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(500);
            entity.Property(e => e.Password).HasColumnName("password").IsRequired().HasMaxLength(500);
            entity.Property(e => e.Firstname).HasColumnName("firstname").HasMaxLength(255);
            entity.Property(e => e.Lastname).HasColumnName("lastname").HasMaxLength(255);
            entity.Property(e => e.Email).HasColumnName("email").IsRequired().HasMaxLength(255);
            entity.Property(e => e.Userroleid).HasColumnName("userroleid");
            entity.Property(e => e.Userstate).HasColumnName("userstate").IsRequired().HasMaxLength(50);

            entity.HasOne(u => u.UserRole)
                .WithMany()
                .HasForeignKey(u => u.Userroleid)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // UserRole entity configuration - maps to legacy "userrole" table
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("userrole");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(100);

            entity.HasIndex(e => e.Name).IsUnique();
        });
    }
}
