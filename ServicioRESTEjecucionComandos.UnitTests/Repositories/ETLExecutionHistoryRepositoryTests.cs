using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServicioRESTEjecucionComandos.Data;
using ServicioRESTEjecucionComandos.Models;
using ServicioRESTEjecucionComandos.Repositories;
using Xunit;

namespace ServicioRESTEjecucionComandos.UnitTests.Repositories;

public class ETLExecutionHistoryRepositoryTests : IDisposable
{
    private readonly ServiceDbContext _context;
    private readonly ETLExecutionHistoryRepository _repository;

    public ETLExecutionHistoryRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ServiceDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new ServiceDbContext(options);
        _context.Database.EnsureCreated();

        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ETLExecutionHistoryRepository>.Instance;
        _repository = new ETLExecutionHistoryRepository(_context, logger);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateRecordWithId()
    {
        // Arrange
        var item = new ETLExecutionHistory
        {
            CodEnvio = "ENV001",
            TipoEntidad = "TEST",
            FechaDatos = DateOnly.FromDateTime(DateTime.Today),
            Codigo = "COD001",
            TriggerType = "MANUAL"
        };

        // Act
        var created = await _repository.CreateAsync(item);

        // Assert
        created.Id.Should().NotBe(Guid.Empty);
        created.Status.Should().Be("PENDIENTE");
        created.CodEnvio.Should().Be("ENV001");
    }

    [Fact]
    public async Task GetByIdAsync_ExistingRecord_ShouldReturnRecord()
    {
        // Arrange
        var item = new ETLExecutionHistory
        {
            Id = Guid.NewGuid(),
            CodEnvio = "ENV001",
            TipoEntidad = "TEST",
            FechaDatos = DateOnly.FromDateTime(DateTime.Today),
            Codigo = "COD001",
            Status = "EXITOSO"
        };
        await _context.ETLExecutionHistories.AddAsync(item);
        await _context.SaveChangesAsync();

        // Act
        var found = await _repository.GetByIdAsync(item.Id);

        // Assert
        found.Should().NotBeNull();
        found!.Id.Should().Be(item.Id);
        found.Status.Should().Be("EXITOSO");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentRecord_ShouldReturnNull()
    {
        // Act
        var found = await _repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        found.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllRecords()
    {
        // Arrange
        var items = new List<ETLExecutionHistory>
        {
            new()
            {
                Id = Guid.NewGuid(),
                CodEnvio = "ENV001",
                TipoEntidad = "TEST",
                FechaDatos = DateOnly.FromDateTime(DateTime.Today),
                Codigo = "COD001",
                Status = "PENDIENTE"
            },
            new()
            {
                Id = Guid.NewGuid(),
                CodEnvio = "ENV002",
                TipoEntidad = "TEST",
                FechaDatos = DateOnly.FromDateTime(DateTime.Today),
                Codigo = "COD002",
                Status = "EXITOSO"
            }
        };
        await _context.ETLExecutionHistories.AddRangeAsync(items);
        await _context.SaveChangesAsync();

        // Act
        var all = await _repository.GetAllAsync();

        // Assert
        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByStatusAsync_ShouldFilterByStatus()
    {
        // Arrange
        var items = new List<ETLExecutionHistory>
        {
            new()
            {
                Id = Guid.NewGuid(),
                CodEnvio = "ENV001",
                TipoEntidad = "TEST",
                FechaDatos = DateOnly.FromDateTime(DateTime.Today),
                Codigo = "COD001",
                Status = "PENDIENTE"
            },
            new()
            {
                Id = Guid.NewGuid(),
                CodEnvio = "ENV002",
                TipoEntidad = "TEST",
                FechaDatos = DateOnly.FromDateTime(DateTime.Today),
                Codigo = "COD002",
                Status = "EXITOSO"
            },
            new()
            {
                Id = Guid.NewGuid(),
                CodEnvio = "ENV003",
                TipoEntidad = "TEST",
                FechaDatos = DateOnly.FromDateTime(DateTime.Today),
                Codigo = "COD003",
                Status = "PENDIENTE"
            }
        };
        await _context.ETLExecutionHistories.AddRangeAsync(items);
        await _context.SaveChangesAsync();

        // Act
        var pending = await _repository.GetByStatusAsync("PENDIENTE");

        // Assert
        pending.Should().HaveCount(2);
        pending.Should().OnlyContain(x => x.Status == "PENDIENTE");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateRecord()
    {
        // Arrange
        var item = new ETLExecutionHistory
        {
            Id = Guid.NewGuid(),
            CodEnvio = "ENV001",
            TipoEntidad = "TEST",
            FechaDatos = DateOnly.FromDateTime(DateTime.Today),
            Codigo = "COD001",
            Status = "PENDIENTE"
        };
        await _context.ETLExecutionHistories.AddAsync(item);
        await _context.SaveChangesAsync();

        // Act
        item.Status = "EXITOSO";
        item.ExitCode = 0;
        item.Output = "Success output";
        await _repository.UpdateAsync(item);

        // Assert
        var updated = await _repository.GetByIdAsync(item.Id);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be("EXITOSO");
        updated.ExitCode.Should().Be(0);
        updated.Output.Should().Be("Success output");
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldUpdateStatusAndFields()
    {
        // Arrange
        var item = new ETLExecutionHistory
        {
            Id = Guid.NewGuid(),
            CodEnvio = "ENV001",
            TipoEntidad = "TEST",
            FechaDatos = DateOnly.FromDateTime(DateTime.Today),
            Codigo = "COD001",
            Status = "PENDIENTE"
        };
        await _context.ETLExecutionHistories.AddAsync(item);
        await _context.SaveChangesAsync();

        var executedAt = DateTime.UtcNow;

        // Act
        await _repository.UpdateStatusAsync(
            item.Id,
            "EN PROCESO",
            executedAt: executedAt);

        // Assert
        var updated = await _repository.GetByIdAsync(item.Id);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be("EN PROCESO");
        updated.ExecutedAt.Should().Be(executedAt);
    }

    [Fact]
    public async Task UpdateStatusAsync_NonExistentId_ShouldNotThrow()
    {
        // Act
        var act = () => _repository.UpdateStatusAsync(
            Guid.NewGuid(),
            "FALLIDO",
            exitCode: 1,
            error: "Test error");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetActiveExecutionAsync_ShouldReturnPendingOrInProgress()
    {
        // Arrange
        var pendingItem = new ETLExecutionHistory
        {
            Id = Guid.NewGuid(),
            CodEnvio = "ENV001",
            TipoEntidad = "TEST",
            FechaDatos = DateOnly.FromDateTime(DateTime.Today),
            Codigo = "COD001",
            Status = "PENDIENTE"
        };
        var completedItem = new ETLExecutionHistory
        {
            Id = Guid.NewGuid(),
            CodEnvio = "ENV001",
            TipoEntidad = "TEST",
            FechaDatos = DateOnly.FromDateTime(DateTime.Today),
            Codigo = "COD001",
            Status = "EXITOSO"
        };
        await _context.ETLExecutionHistories.AddRangeAsync(pendingItem, completedItem);
        await _context.SaveChangesAsync();

        // Act
        var active = await _repository.GetActiveExecutionAsync("ENV001", "COD001");

        // Assert
        active.Should().NotBeNull();
        active!.Status.Should().BeOneOf("PENDIENTE", "EN PROCESO");
    }

    [Fact]
    public async Task GetActiveExecutionAsync_NoActive_ShouldReturnNull()
    {
        // Arrange
        var item = new ETLExecutionHistory
        {
            Id = Guid.NewGuid(),
            CodEnvio = "ENV001",
            TipoEntidad = "TEST",
            FechaDatos = DateOnly.FromDateTime(DateTime.Today),
            Codigo = "COD001",
            Status = "EXITOSO"
        };
        await _context.ETLExecutionHistories.AddAsync(item);
        await _context.SaveChangesAsync();

        // Act
        var active = await _repository.GetActiveExecutionAsync("ENV001", "COD001");

        // Assert
        active.Should().BeNull();
    }

    [Fact]
    public async Task UpdateStatusWithFechaDatosAsync_ShouldUpdateFechaDatos()
    {
        // Arrange
        var item = new ETLExecutionHistory
        {
            Id = Guid.NewGuid(),
            CodEnvio = "ENV001",
            TipoEntidad = "TEST",
            FechaDatos = DateOnly.FromDateTime(DateTime.Today),
            Codigo = "COD001",
            Status = "PENDIENTE"
        };
        await _context.ETLExecutionHistories.AddAsync(item);
        await _context.SaveChangesAsync();

        var newFechaDatos = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        // Act
        await _repository.UpdateStatusWithFechaDatosAsync(
            item.Id,
            "EXITOSO",
            fechaDatos: newFechaDatos,
            exitCode: 0);

        // Assert
        var updated = await _repository.GetByIdAsync(item.Id);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be("EXITOSO");
        updated.FechaDatos.Should().Be(newFechaDatos);
        updated.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task GetAllActiveAsync_ShouldReturnOnlyPendingAndInProgress()
    {
        // Arrange
        var pendingItem = new ETLExecutionHistory
        {
            Id = Guid.NewGuid(),
            CodEnvio = "ENV001",
            TipoEntidad = "TEST",
            FechaDatos = DateOnly.FromDateTime(DateTime.Today),
            Codigo = "COD001",
            Status = "PENDIENTE"
        };
        var inProgressItem = new ETLExecutionHistory
        {
            Id = Guid.NewGuid(),
            CodEnvio = "ENV002",
            TipoEntidad = "TEST",
            FechaDatos = DateOnly.FromDateTime(DateTime.Today),
            Codigo = "COD002",
            Status = "EN PROCESO"
        };
        var successItem = new ETLExecutionHistory
        {
            Id = Guid.NewGuid(),
            CodEnvio = "ENV003",
            TipoEntidad = "TEST",
            FechaDatos = DateOnly.FromDateTime(DateTime.Today),
            Codigo = "COD003",
            Status = "EXITOSO"
        };
        var failedItem = new ETLExecutionHistory
        {
            Id = Guid.NewGuid(),
            CodEnvio = "ENV004",
            TipoEntidad = "TEST",
            FechaDatos = DateOnly.FromDateTime(DateTime.Today),
            Codigo = "COD004",
            Status = "FALLIDO"
        };

        await _context.ETLExecutionHistories.AddRangeAsync(pendingItem, inProgressItem, successItem, failedItem);
        await _context.SaveChangesAsync();

        // Act
        var active = await _repository.GetAllActiveAsync();

        // Assert
        active.Should().HaveCount(2);
        active.Should().OnlyContain(x => x.Status == "PENDIENTE" || x.Status == "EN PROCESO");
        active.Select(x => x.Id).Should().Contain(pendingItem.Id);
        active.Select(x => x.Id).Should().Contain(inProgressItem.Id);
    }

    [Fact]
    public async Task GetAllActiveAsync_EmptyDatabase_ShouldReturnEmptyList()
    {
        // Act
        var active = await _repository.GetAllActiveAsync();

        // Assert
        active.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllActiveAsync_AllCompleted_ShouldReturnEmptyList()
    {
        // Arrange
        var items = new List<ETLExecutionHistory>
        {
            new()
            {
                Id = Guid.NewGuid(),
                CodEnvio = "ENV001",
                TipoEntidad = "TEST",
                FechaDatos = DateOnly.FromDateTime(DateTime.Today),
                Codigo = "COD001",
                Status = "EXITOSO"
            },
            new()
            {
                Id = Guid.NewGuid(),
                CodEnvio = "ENV002",
                TipoEntidad = "TEST",
                FechaDatos = DateOnly.FromDateTime(DateTime.Today),
                Codigo = "COD002",
                Status = "FALLIDO"
            }
        };
        await _context.ETLExecutionHistories.AddRangeAsync(items);
        await _context.SaveChangesAsync();

        // Act
        var active = await _repository.GetAllActiveAsync();

        // Assert
        active.Should().BeEmpty();
    }

    [Fact]
    public async Task UpsertOrGetActiveAsync_NoActiveExecution_ShouldCreateNew()
    {
        // Arrange
        string codEnvio = "ENV001";
        string codigo = "COD001";
        string tipoEntidad = "TEST";
        var fechaDatos = DateOnly.FromDateTime(DateTime.Today);

        // Act
        var result = await _repository.UpsertOrGetActiveAsync(codEnvio, codigo, tipoEntidad, fechaDatos, "MANUAL");

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be("PENDIENTE");
        result.CodEnvio.Should().Be(codEnvio);
        result.Codigo.Should().Be(codigo);
        result.TipoEntidad.Should().Be(tipoEntidad);
        result.FechaDatos.Should().Be(fechaDatos);
        result.TriggerType.Should().Be("MANUAL");
    }

    [Fact]
    public async Task UpsertOrGetActiveAsync_ExistingActiveExecution_ShouldReturnExisting()
    {
        // Arrange
        var existing = new ETLExecutionHistory
        {
            Id = Guid.NewGuid(),
            CodEnvio = "ENV001",
            TipoEntidad = "TEST",
            FechaDatos = DateOnly.FromDateTime(DateTime.Today),
            Codigo = "COD001",
            Status = "EN PROCESO",
            TriggerType = "MANUAL"
        };
        await _context.ETLExecutionHistories.AddAsync(existing);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.UpsertOrGetActiveAsync(
            "ENV001", "COD001", "TEST", DateOnly.FromDateTime(DateTime.Today), "MANUAL");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(existing.Id);
        result.Status.Should().Be("EN PROCESO");
    }

    [Fact]
    public async Task UpsertOrGetActiveAsync_ExistingCompletedExecution_ShouldCreateNew()
    {
        // Arrange
        var completed = new ETLExecutionHistory
        {
            Id = Guid.NewGuid(),
            CodEnvio = "ENV001",
            TipoEntidad = "TEST",
            FechaDatos = DateOnly.FromDateTime(DateTime.Today),
            Codigo = "COD001",
            Status = "EXITOSO",
            TriggerType = "MANUAL"
        };
        await _context.ETLExecutionHistories.AddAsync(completed);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.UpsertOrGetActiveAsync(
            "ENV001", "COD001", "TEST", DateOnly.FromDateTime(DateTime.Today), "MANUAL");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().NotBe(completed.Id);
        result.Status.Should().Be("PENDIENTE");
    }

    [Fact]
    public async Task UpsertOrGetActiveAsync_ExistingFailedExecution_ShouldCreateNew()
    {
        // Arrange
        var failed = new ETLExecutionHistory
        {
            Id = Guid.NewGuid(),
            CodEnvio = "ENV001",
            TipoEntidad = "TEST",
            FechaDatos = DateOnly.FromDateTime(DateTime.Today),
            Codigo = "COD001",
            Status = "FALLIDO",
            TriggerType = "MANUAL"
        };
        await _context.ETLExecutionHistories.AddAsync(failed);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.UpsertOrGetActiveAsync(
            "ENV001", "COD001", "TEST", DateOnly.FromDateTime(DateTime.Today), "MANUAL");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().NotBe(failed.Id);
        result.Status.Should().Be("PENDIENTE");
    }
}
