using Microsoft.EntityFrameworkCore;
using ServicioRESTEjecucionComandos.Models;

namespace ServicioRESTEjecucionComandos.Data;

/// <summary>
/// DbContext for the service database storing ServiceItem records and providing
/// read access to the legacy base_datos table. Auto-created on startup.
/// </summary>
public class ServiceDbContext : DbContext
{
    public ServiceDbContext(DbContextOptions<ServiceDbContext> options) : base(options)
    {
    }

    public DbSet<ServiceItem> ServiceItems => Set<ServiceItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ServiceItem entity configuration - managed table (created via raw SQL on startup)
        modelBuilder.Entity<ServiceItem>(entity =>
        {
            entity.ToTable("ServiceItem");
            entity.HasKey(e => e.ItemId);

            entity.Property(e => e.ItemId).HasColumnName("ItemId").ValueGeneratedOnAdd();
            entity.Property(e => e.Status).HasColumnName("Status").IsRequired().HasMaxLength(20);
            entity.Property(e => e.ExitCode).HasColumnName("ExitCode");
            entity.Property(e => e.Output).HasColumnName("Output");
            entity.Property(e => e.Error).HasColumnName("Error");
            entity.Property(e => e.ExecutedAt).HasColumnName("ExecutedAt");
            entity.Property(e => e.CompletedAt).HasColumnName("CompletedAt");

            entity.HasIndex(e => e.Status);
        });

        // BaseDatos is a read-only legacy table - excluded from migrations.
        // Query it directly via Database.SqlQueryRaw<BaseDatos>().
        modelBuilder.Ignore<BaseDatos>();
    }
}