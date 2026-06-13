using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using ServicioRESTEjecucionComandos.Constants;
using ServicioRESTEjecucionComandos.Data;
using ServicioRESTEjecucionComandos.Models;
using ServicioRESTEjecucionComandos.Repositories;
using ServicioRESTEjecucionComandos.Services;
using Xunit;

namespace ServicioRESTEjecucionComandos.UnitTests.Services;

public class RefreshTokenServiceTests : IAsyncLifetime
{
    private readonly RefreshTokenDbContext _context;
    private readonly RefreshTokenRepository _repository;
    private readonly AuthAuditLogRepository _auditLogRepository;
    private readonly RefreshTokenService _service;

    public RefreshTokenServiceTests()
    {
        var options = new DbContextOptionsBuilder<RefreshTokenDbContext>()
            .UseSqlite("Filename=:memory:")
            .Options;
        _context = new RefreshTokenDbContext(options);

        var loggerMock = Mock.Of<ILogger<RefreshTokenRepository>>();
        _repository = new RefreshTokenRepository(_context, loggerMock);

        var auditLoggerMock = Mock.Of<ILogger<AuthAuditLogRepository>>();
        _auditLogRepository = new AuthAuditLogRepository(_context, auditLoggerMock);

        var serviceLoggerMock = Mock.Of<ILogger<RefreshTokenService>>();

        var configData = new Dictionary<string, string?>
        {
            ["Jwt:RefreshTokenDays"] = "30"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        _service = new RefreshTokenService(
            _repository,
            _auditLogRepository,
            serviceLoggerMock,
            configuration);
    }

    public async Task InitializeAsync()
    {
        // Create SQLite in-memory database schema
        await _context.Database.OpenConnectionAsync();
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task GenerateAsync_ShouldReturnValidToken()
    {
        // Act
        var token = await _service.GenerateAsync(userId: 1, clientIp: "192.168.1.1");

        // Assert
        token.Should().NotBeNullOrEmpty();
        token.Length.Should().BeGreaterThan(0);

        // Verify token was stored in database
        var storedToken = await _context.RefreshTokens.FirstOrDefaultAsync();
        storedToken.Should().NotBeNull();
        storedToken!.UserId.Should().Be(1);
        storedToken.CreatedByIp.Should().Be("192.168.1.1");
    }

    [Fact]
    public async Task GenerateAsync_ShouldStoreTokenWithCorrectUserId()
    {
        // Act
        await _service.GenerateAsync(userId: 42);

        // Assert
        var storedToken = await _context.RefreshTokens.FirstOrDefaultAsync();
        storedToken.Should().NotBeNull();
        storedToken!.UserId.Should().Be(42);
        storedToken.IsRevoked.Should().BeFalse();
        storedToken.CreatedByIp.Should().BeNull();
    }

    [Fact]
    public async Task GenerateAsync_ShouldSetExpirationCorrectly()
    {
        // Act
        await _service.GenerateAsync(userId: 1);

        // Assert
        var storedToken = await _context.RefreshTokens.FirstOrDefaultAsync();
        storedToken.Should().NotBeNull();
        var expectedExpiry = storedToken!.CreatedAtUtc.AddDays(30);
        storedToken.ExpiresAtUtc.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ValidateAndRotateAsync_ValidToken_ShouldReturnNewToken()
    {
        // Arrange
        var oldToken = await _service.GenerateAsync(userId: 5);

        // Act
        var result = await _service.ValidateAndRotateAsync(oldToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.NewToken.Should().NotBeNullOrEmpty();
        result.NewToken.Should().NotBe(oldToken);
        result.UserId.Should().Be(5);
    }

    [Fact]
    public async Task ValidateAndRotateAsync_TokenNotFound_ShouldReturnFailure()
    {
        // Act
        var result = await _service.ValidateAndRotateAsync("nonexistent_token_hash");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task ValidateAndRotateAsync_RevokedToken_ShouldReturnFailure()
    {
        // Arrange
        var token = await _service.GenerateAsync(userId: 5);
        await _service.RevokeAsync(token);

        // Act
        var result = await _service.ValidateAndRotateAsync(token);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("revoked");
    }

    [Fact]
    public async Task ValidateAndRotateAsync_ExpiredToken_ShouldReturnFailure()
    {
        // Arrange - Insert an expired token directly
        var randomBytes = new byte[64];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            rng.GetBytes(randomBytes);
        var token = Convert.ToBase64String(randomBytes);

        var tokenHash = ComputeSha256Hash(token);

        var expiredToken = new RefreshToken
        {
            UserId = 5,
            TokenHash = tokenHash,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-60),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(-30),
            IsRevoked = false
        };
        await _context.RefreshTokens.AddAsync(expiredToken);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ValidateAndRotateAsync(token);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("expired");
    }

    [Fact]
    public async Task RevokeAsync_ValidToken_ShouldReturnTrue()
    {
        // Arrange
        var token = await _service.GenerateAsync(userId: 5, clientIp: "192.168.1.1");

        // Act
        var result = await _service.RevokeAsync(token, "192.168.1.100");

        // Assert
        result.Should().BeTrue();

        var storedToken = await _context.RefreshTokens.FirstOrDefaultAsync();
        storedToken.Should().NotBeNull();
        storedToken!.IsRevoked.Should().BeTrue();
        storedToken.RevokedByIp.Should().Be("192.168.1.100");
    }

    [Fact]
    public async Task RevokeAsync_NonExistentToken_ShouldReturnFalse()
    {
        // Act
        var result = await _service.RevokeAsync("nonexistent_token");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RevokeAsync_AlreadyRevokedToken_ShouldReturnFalse()
    {
        // Arrange
        var token = await _service.GenerateAsync(userId: 5);
        await _service.RevokeAsync(token);

        // Act
        var result = await _service.RevokeAsync(token);

        // Assert
        result.Should().BeFalse();
    }
    
    private static string ComputeSha256Hash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        var builder = new StringBuilder();
        foreach (var b in bytes)
            builder.Append(b.ToString("x2"));
        return builder.ToString();
    }

    [Fact]
    public async Task RevokeAllForUserAsync_ShouldRevokeAllTokensForUser()
    {
        // Arrange
        await _service.GenerateAsync(userId: 42);
        await _service.GenerateAsync(userId: 42);
        await _service.GenerateAsync(userId: 99);

        // Act
        await _service.RevokeAllForUserAsync(42);

        // Assert
        var user42Tokens = await _context.RefreshTokens.Where(t => t.UserId == 42).ToListAsync();
        user42Tokens.Should().HaveCount(2);
        user42Tokens.All(t => t.IsRevoked).Should().BeTrue();

        var user99Tokens = await _context.RefreshTokens.Where(t => t.UserId == 99).ToListAsync();
        user99Tokens.Should().HaveCount(1);
        user99Tokens.All(t => !t.IsRevoked).Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAndRotateAsync_ShouldCreateAuditLog()
    {
        // Arrange
        var token = await _service.GenerateAsync(userId: 5);

        // Act
        await _service.ValidateAndRotateAsync(token);

        // Assert
        var auditLogs = await _context.AuthAuditLogs.ToListAsync();
        auditLogs.Should().NotBeEmpty();
        auditLogs.Any(log => log.EventType == AuditEventType.TokenRotated && log.UserId == 5 && log.Success).Should().BeTrue();
    }
}
