using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ServicioRESTEjecucionComandos.Data;
using ServicioRESTEjecucionComandos.Models;
using ServicioRESTEjecucionComandos.Repositories;
using ServicioRESTEjecucionComandos.Services;
using Xunit;

namespace ServicioRESTEjecucionComandos.UnitTests.Services;

public class RefreshTokenCleanupServiceTests : IAsyncLifetime
{
    private readonly RefreshTokenDbContext _context;
    private readonly RefreshTokenCleanupService _service;

    public RefreshTokenCleanupServiceTests()
    {
        var options = new DbContextOptionsBuilder<RefreshTokenDbContext>()
            .UseSqlite("Filename=:memory:")
            .Options;
        _context = new RefreshTokenDbContext(options);

        var repository = new RefreshTokenRepository(_context, Mock.Of<ILogger<RefreshTokenRepository>>());
        var serviceProvider = new ServiceCollection()
            .AddSingleton(repository)
            .BuildServiceProvider();

        var loggerMock = Mock.Of<ILogger<RefreshTokenCleanupService>>();

        var configData = new Dictionary<string, string?>
        {
            ["RefreshTokenCleanup:CleanupIntervalMinutes"] = "1",
            ["RefreshTokenCleanup:AuditLogRetentionDays"] = "90"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        _service = new RefreshTokenCleanupService(serviceProvider, loggerMock, configuration);
    }

    public async Task InitializeAsync()
    {
        await _context.Database.OpenConnectionAsync();
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task DeleteExpiredAsync_CancellationTokenCancelled_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var repository = new RefreshTokenRepository(_context, Mock.Of<ILogger<RefreshTokenRepository>>());
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(()
            => repository.DeleteExpiredAsync(DateTime.UtcNow, cts.Token));
    }

    [Fact]
    public async Task DeleteOldAuditLogsAsync_CancellationTokenCancelled_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var repository = new RefreshTokenRepository(_context, Mock.Of<ILogger<RefreshTokenRepository>>());
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(()
            => repository.DeleteOldAuditLogsAsync(DateTime.UtcNow, cts.Token));
    }

    [Fact]
    public async Task DeleteExpiredAsync_ShouldRemoveExpiredTokens()
    {
        // Arrange - Insert an expired token
        var expiredToken = new RefreshToken
        {
            TokenHash = "hash-expired-123",
            UserId = 1,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(-1),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
            CreatedByIp = "192.168.1.1",
            IsRevoked = false
        };
        await _context.RefreshTokens.AddAsync(expiredToken);
        await _context.SaveChangesAsync();

        var countBefore = await _context.RefreshTokens.CountAsync();
        countBefore.Should().Be(1);

        // Act
        var repository = new RefreshTokenRepository(_context, Mock.Of<ILogger<RefreshTokenRepository>>());
        var utcNow = DateTime.UtcNow;
        var cancellationToken = CancellationToken.None;
        var deletedCount = await repository.DeleteExpiredAsync(utcNow, cancellationToken);

        // Assert
        deletedCount.Should().Be(1);
        var countAfter = await _context.RefreshTokens.CountAsync();
        countAfter.Should().Be(0);
    }

    [Fact]
    public async Task DeleteExpiredAsync_ShouldNotRemoveValidTokens()
    {
        // Arrange - Insert a valid (non-expired) token
        var validToken = new RefreshToken
        {
            TokenHash = "hash-valid-456",
            UserId = 1,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByIp = "192.168.1.1",
            IsRevoked = false
        };
        await _context.RefreshTokens.AddAsync(validToken);
        await _context.SaveChangesAsync();

        // Act
        var repository = new RefreshTokenRepository(_context, Mock.Of<ILogger<RefreshTokenRepository>>());
        var utcNow = DateTime.UtcNow;
        var cancellationToken = CancellationToken.None;
        var deletedCount = await repository.DeleteExpiredAsync(utcNow, cancellationToken);

        // Assert
        deletedCount.Should().Be(0);
        var countAfter = await _context.RefreshTokens.CountAsync();
        countAfter.Should().Be(1);
    }

    [Fact]
    public async Task DeleteOldAuditLogsAsync_ShouldRemoveOldAuditLogs()
    {
        // Arrange - Insert an old audit log
        var oldLog = new AuthAuditLog
        {
            EventType = "TestEvent",
            UserId = 1,
            Email = "testuser@example.com",
            ClientIp = "192.168.1.1",
            Message = "Test details",
            TimestampUtc = DateTime.UtcNow.AddDays(-100),
            Success = true
        };
        await _context.AuthAuditLogs.AddAsync(oldLog);
        await _context.SaveChangesAsync();

        var countBefore = await _context.AuthAuditLogs.CountAsync();
        countBefore.Should().Be(1);

        // Act
        var repository = new RefreshTokenRepository(_context, Mock.Of<ILogger<RefreshTokenRepository>>());
        var cutoffDate = DateTime.UtcNow.AddDays(-90);
        var cancellationToken = CancellationToken.None;
        var deletedCount = await repository.DeleteOldAuditLogsAsync(cutoffDate, cancellationToken);

        // Assert
        deletedCount.Should().Be(1);
        var countAfter = await _context.AuthAuditLogs.CountAsync();
        countAfter.Should().Be(0);
    }

    [Fact]
    public async Task DeleteOldAuditLogsAsync_ShouldNotRemoveRecentAuditLogs()
    {
        // Arrange - Insert a recent audit log
        var recentLog = new AuthAuditLog
        {
            EventType = "TestEvent",
            UserId = 1,
            Email = "testuser@example.com",
            ClientIp = "192.168.1.1",
            Message = "Test details",
            TimestampUtc = DateTime.UtcNow.AddDays(-1),
            Success = true
        };
        await _context.AuthAuditLogs.AddAsync(recentLog);
        await _context.SaveChangesAsync();

        // Act
        var repository = new RefreshTokenRepository(_context, Mock.Of<ILogger<RefreshTokenRepository>>());
        var cutoffDate = DateTime.UtcNow.AddDays(-90);
        var cancellationToken = CancellationToken.None;
        var deletedCount = await repository.DeleteOldAuditLogsAsync(cutoffDate, cancellationToken);

        // Assert
        deletedCount.Should().Be(0);
        var countAfter = await _context.AuthAuditLogs.CountAsync();
        countAfter.Should().Be(1);
    }

    [Fact]
    public async Task DeleteExpiredAsync_ShouldRemoveOnlyExpiredAndKeepValid()
    {
        // Arrange - Insert both expired and valid tokens
        var expiredToken = new RefreshToken
        {
            TokenHash = "hash-expired",
            UserId = 1,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(-1),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
            CreatedByIp = "192.168.1.1",
            IsRevoked = false
        };
        var validToken = new RefreshToken
        {
            TokenHash = "hash-valid",
            UserId = 1,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByIp = "192.168.1.1",
            IsRevoked = false
        };
        await _context.RefreshTokens.AddRangeAsync(expiredToken, validToken);
        await _context.SaveChangesAsync();

        // Act
        var repository = new RefreshTokenRepository(_context, Mock.Of<ILogger<RefreshTokenRepository>>());
        var utcNow = DateTime.UtcNow;
        var cancellationToken = CancellationToken.None;
        var deletedCount = await repository.DeleteExpiredAsync(utcNow, cancellationToken);

        // Assert
        deletedCount.Should().Be(1);
        var countAfter = await _context.RefreshTokens.CountAsync();
        countAfter.Should().Be(1);
        var remaining = await _context.RefreshTokens.FirstAsync();
        remaining.TokenHash.Should().Be("hash-valid");
    }

    [Fact]
    public async Task DeleteExpiredAsync_EmptyDatabase_ShouldReturnZero()
    {
        // Act
        var repository = new RefreshTokenRepository(_context, Mock.Of<ILogger<RefreshTokenRepository>>());
        var utcNow = DateTime.UtcNow;
        var cancellationToken = CancellationToken.None;
        var deletedCount = await repository.DeleteExpiredAsync(utcNow, cancellationToken);

        // Assert
        deletedCount.Should().Be(0);
    }

    [Fact]
    public async Task DeleteOldAuditLogsAsync_EmptyDatabase_ShouldReturnZero()
    {
        // Act
        var repository = new RefreshTokenRepository(_context, Mock.Of<ILogger<RefreshTokenRepository>>());
        var cutoffDate = DateTime.UtcNow.AddDays(-90);
        var cancellationToken = CancellationToken.None;
        var deletedCount = await repository.DeleteOldAuditLogsAsync(cutoffDate, cancellationToken);

        // Assert
        deletedCount.Should().Be(0);
    }
}
