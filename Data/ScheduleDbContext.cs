using Microsoft.EntityFrameworkCore;
using ServicioRESTEjecucionComandos.Models;

namespace ServicioRESTEjecucionComandos.Data;

/// <summary>
/// DbContext for the <c>etl_schedule</c> table stored in SQLite.
/// Shares the same database file as Hangfire storage and RefreshToken data.
/// </summary>
public class ScheduleDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of ScheduleDbContext.
    /// </summary>
    public ScheduleDbContext(DbContextOptions<ScheduleDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// DbSet for ETL schedule records.
    /// </summary>
    public DbSet<EtlSchedule> EtlSchedules => Set<EtlSchedule>();

    /// <summary>
    /// Configures the model for the etl_schedule table.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<EtlSchedule>(entity =>
        {
            entity.ToTable("etl_schedule");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.CodEnvio).HasColumnName("CodEnvio").IsRequired().HasMaxLength(100);
            entity.Property(e => e.TipoEntidad).HasColumnName("TipoEntidad").IsRequired().HasMaxLength(100);
            entity.Property(e => e.Codigo).HasColumnName("Codigo").IsRequired().HasMaxLength(100);
            entity.Property(e => e.CronExpression).HasColumnName("CronExpression").IsRequired().HasMaxLength(100);
            entity.Property(e => e.IsActive).HasColumnName("IsActive");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.Codigo);
        });
    }
}
