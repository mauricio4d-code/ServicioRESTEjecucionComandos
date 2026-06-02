using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ServicioRESTEjecucionComandos.Data;
using ServicioRESTEjecucionComandos.Models;
using ServicioRESTEjecucionComandos.Repositories;
using Xunit;

namespace ServicioRESTEjecucionComandos.UnitTests.Repositories;

public class EtlScheduleRepositoryTests
{
    private readonly ScheduleDbContext _context;
    private readonly EtlScheduleRepository _repository;

    public EtlScheduleRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ScheduleDbContext>()
            .UseInMemoryDatabase(databaseName: $"EtlScheduleTest_{Guid.NewGuid()}")
            .Options;
        _context = new ScheduleDbContext(options);
        var loggerMock = Mock.Of<ILogger<EtlScheduleRepository>>();
        _repository = new EtlScheduleRepository(_context, loggerMock);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task CreateAsync_ShouldInsertAndReturnScheduleWithId()
    {
        // Arrange
        var schedule = new EtlSchedule
        {
            Params = "--test param",
            CronExpression = "0 0 * * *",
            IsActive = true
        };

        // Act
        var result = await _repository.CreateAsync(schedule);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBe(Guid.Empty);
        result.Params.Should().Be("--test param");
        result.CronExpression.Should().Be("0 0 * * *");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ShouldReturnSchedule()
    {
        // Arrange
        var schedule = new EtlSchedule
        {
            Params = "--existing",
            CronExpression = "0 12 * * *",
            IsActive = true
        };
        await _repository.CreateAsync(schedule);

        // Act
        var result = await _repository.GetByIdAsync(schedule.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Params.Should().Be("--existing");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ShouldReturnNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllSchedulesOrderedByCreatedAtDescending()
    {
        // Arrange
        var schedule1 = new EtlSchedule { Params = "--first", CronExpression = "0 0 * * *", IsActive = true };
        var schedule2 = new EtlSchedule { Params = "--second", CronExpression = "0 1 * * *", IsActive = false };
        await _repository.CreateAsync(schedule1);
        await Task.Delay(10);
        await _repository.CreateAsync(schedule2);

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].CreatedAt.Should().BeOnOrAfter(result[1].CreatedAt);
    }

    [Fact]
    public async Task GetActiveAsync_ShouldReturnOnlyActiveSchedules()
    {
        // Arrange
        var activeSchedule = new EtlSchedule { Params = "--active", CronExpression = "0 0 * * *", IsActive = true };
        var inactiveSchedule = new EtlSchedule { Params = "--inactive", CronExpression = "0 1 * * *", IsActive = false };
        await _repository.CreateAsync(activeSchedule);
        await _repository.CreateAsync(inactiveSchedule);

        // Act
        var result = await _repository.GetActiveAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Params.Should().Be("--active");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateScheduleAndUpdatedAt()
    {
        // Arrange
        var schedule = new EtlSchedule { Params = "--original", CronExpression = "0 0 * * *", IsActive = true };
        await _repository.CreateAsync(schedule);
        var originalUpdatedAt = schedule.UpdatedAt;
        await Task.Delay(10);

        schedule.Params = "--updated";
        schedule.IsActive = false;

        // Act
        await _repository.UpdateAsync(schedule);

        // Assert
        var updated = await _repository.GetByIdAsync(schedule.Id);
        updated.Should().NotBeNull();
        updated!.Params.Should().Be("--updated");
        updated.IsActive.Should().BeFalse();
        updated.UpdatedAt.Should().NotBeNull();
        updated.UpdatedAt!.Value.Should().BeAfter(originalUpdatedAt!.Value);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveSchedule()
    {
        // Arrange
        var schedule = new EtlSchedule { Params = "--delete", CronExpression = "0 0 * * *", IsActive = true };
        await _repository.CreateAsync(schedule);
        var id = schedule.Id;

        // Act
        await _repository.DeleteAsync(id);

        // Assert
        var result = await _repository.GetByIdAsync(id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistingId_ShouldNotThrow()
    {
        // Act
        var act = () => _repository.DeleteAsync(Guid.NewGuid());

        // Assert
        await act.Should().NotThrowAsync();
    }
}
