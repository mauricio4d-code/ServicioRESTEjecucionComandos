using Microsoft.EntityFrameworkCore;
using ServicioRESTEjecucionComandos.Models;

namespace ServicioRESTEjecucionComandos.Data;

/// <summary>
/// DbContext for the service database storing ETLExecutionHistory records and providing
/// read access to the legacy base_datos table. Auto-created on startup.
/// </summary>
public class ServiceDbContext : DbContext
{
    public ServiceDbContext(DbContextOptions<ServiceDbContext> options) : base(options)
    {
    }

    public DbSet<ETLExecutionHistory> ETLExecutionHistories => Set<ETLExecutionHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ETLExecutionHistory entity configuration - managed table (created via raw SQL on startup)
        modelBuilder.Entity<ETLExecutionHistory>(entity =>
        {
            entity.ToTable("hist_etl_execution");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.CodEnvio).HasColumnName("CodEnvio").IsRequired().HasMaxLength(100);
            entity.Property(e => e.TipoEntidad).HasColumnName("TipoEntidad").IsRequired();
            entity.Property(e => e.FechaDatos).HasColumnName("FechaDatos").IsRequired().HasColumnType("date");
            entity.Property(e => e.Codigo).HasColumnName("Codigo").IsRequired();
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