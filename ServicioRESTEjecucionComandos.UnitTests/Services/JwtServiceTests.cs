using FluentAssertions;
using Microsoft.Extensions.Configuration;
using ServicioRESTEjecucionComandos.Models;
using ServicioRESTEjecucionComandos.Services;
using System.IdentityModel.Tokens.Jwt;
using Xunit;

namespace ServicioRESTEjecucionComandos.UnitTests.Services;

public class JwtServiceTests
{
    private readonly JwtService _jwtService;
    private readonly User _testUser;
    private readonly UserRole _testRole;

    public JwtServiceTests()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = "ThisIsAVerySecretKeyForJwtTokenGeneration!12345",
            ["Jwt:Issuer"] = "TestIssuer",
            ["Jwt:Audience"] = "TestAudience",
            ["Jwt:AccessTokenMinutes"] = "5"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        _jwtService = new JwtService(configuration);

        _testUser = new User
        {
            Id = 1,
            Name = "TestUser",
            Email = "test@example.com",
            Firstname = "Test",
            Lastname = "User",
            Password = "hashed_password",
            Userstate = "Activo"
        };

        _testRole = new UserRole
        {
            Id = 1,
            Name = "Administrador"
        };

        _testUser.UserRole = _testRole;
    }

    [Fact]
    public void GenerateToken_ShouldReturnValidJwtToken()
    {
        // Act
        var token = _jwtService.GenerateToken(_testUser, _testRole);

        // Assert
        token.Should().NotBeNullOrEmpty();
        token.Should().Contain(".");
        token.Split('.').Length.Should().Be(3);
    }

    [Fact]
    public void GenerateToken_ShouldContainCorrectClaims()
    {
        // Act
        var tokenString = _jwtService.GenerateToken(_testUser, _testRole);
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenString);

        // Assert
        jwtToken.Should().NotBeNull();
        jwtToken.Claims.Should().Contain(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier && c.Value == "1");
        jwtToken.Claims.Should().Contain(c => c.Type == System.Security.Claims.ClaimTypes.Name && c.Value == "TestUser");
        jwtToken.Claims.Should().Contain(c => c.Type == System.Security.Claims.ClaimTypes.Email && c.Value == "test@example.com");
        jwtToken.Claims.Should().Contain(c => c.Type == System.Security.Claims.ClaimTypes.GivenName && c.Value == "Test");
        jwtToken.Claims.Should().Contain(c => c.Type == System.Security.Claims.ClaimTypes.Surname && c.Value == "User");
        jwtToken.Claims.Should().Contain(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "Administrador");
    }

    [Fact]
    public void GenerateToken_ShouldHaveCorrectIssuerAndAudience()
    {
        // Act
        var tokenString = _jwtService.GenerateToken(_testUser, _testRole);
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenString);

        // Assert
        jwtToken.Issuer.Should().Be("TestIssuer");
        jwtToken.Audiences.Should().ContainSingle().Which.Should().Be("TestAudience");
    }

    [Fact]
    public void GenerateToken_ShouldHaveExpirationInFuture()
    {
        // Act
        var tokenString = _jwtService.GenerateToken(_testUser, _testRole);
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenString);

        // Assert
        jwtToken.ValidTo.Should().BeAfter(DateTime.UtcNow);
        jwtToken.ValidTo.Should().BeOnOrBefore(DateTime.UtcNow.AddMinutes(5).AddSeconds(5));
    }

    [Fact]
    public void GetExpiresInSeconds_ShouldReturnCorrectValue()
    {
        // Act
        var expiresInSeconds = _jwtService.GetExpiresInSeconds();

        // Assert
        expiresInSeconds.Should().Be(300); // 5 minutes * 60 seconds
    }

    [Fact]
    public void GenerateToken_WithNullFirstname_ShouldHandleGracefully()
    {
        // Arrange
        var user = new User
        {
            Id = 2,
            Name = "NoFirstName",
            Email = "no@first.com",
            Firstname = null,
            Lastname = null,
            Password = "hash",
            Userstate = "Activo"
        };
        var role = new UserRole { Id = 2, Name = "Usuario" };

        // Act
        var tokenString = _jwtService.GenerateToken(user, role);
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenString);

        // Assert
        jwtToken.Should().NotBeNull();
        jwtToken.Claims.Should().Contain(c => c.Type == System.Security.Claims.ClaimTypes.GivenName && c.Value == string.Empty);
        jwtToken.Claims.Should().Contain(c => c.Type == System.Security.Claims.ClaimTypes.Surname && c.Value == string.Empty);
    }

    [Theory]
    [InlineData("1", 60)]
    [InlineData("10", 600)]
    [InlineData("60", 3600)]
    public void GetExpiresInSeconds_ShouldScaleWithConfiguration(string minutes, int expectedSeconds)
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = "ThisIsAVerySecretKeyForJwtTokenGeneration!12345",
            ["Jwt:Issuer"] = "TestIssuer",
            ["Jwt:Audience"] = "TestAudience",
            ["Jwt:AccessTokenMinutes"] = minutes
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
        var service = new JwtService(configuration);

        // Act
        var result = service.GetExpiresInSeconds();

        // Assert
        result.Should().Be(expectedSeconds);
    }
}
