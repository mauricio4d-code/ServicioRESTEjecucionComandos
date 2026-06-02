using FluentAssertions;
using ServicioRESTEjecucionComandos.Interfaces;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace ServicioRESTEjecucionComandos.UnitTests.Services;

public class LegacyPasswordValidatorTests
{
    private readonly LegacyPasswordValidator _validator;

    public LegacyPasswordValidatorTests()
    {
        _validator = new LegacyPasswordValidator();
    }

    [Fact]
    public async Task ValidateAsync_WithCorrectPassword_ShouldReturnTrue()
    {
        // Arrange
        string plainPassword = "MySecurePassword123";
        string storedHash = ComputeMd5(plainPassword);

        // Act
        var result = await _validator.ValidateAsync(storedHash, plainPassword);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithIncorrectPassword_ShouldReturnFalse()
    {
        // Arrange
        string correctPassword = "CorrectPassword";
        string wrongPassword = "WrongPassword";
        string storedHash = ComputeMd5(correctPassword);

        // Act
        var result = await _validator.ValidateAsync(storedHash, wrongPassword);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_WithEmptyPassword_ShouldReturnFalse()
    {
        // Arrange
        string storedHash = ComputeMd5("SomePassword");

        // Act
        var result = await _validator.ValidateAsync(storedHash, "");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_WithNullInputPassword_ShouldThrowException()
    {
        // Arrange
        string storedHash = ComputeMd5("SomePassword");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _validator.ValidateAsync(storedHash, null!));
    }

    [Fact]
    public async Task ValidateAsync_WithNullStoredPassword_ShouldThrowException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _validator.ValidateAsync(null!, "password"));
    }

    [Fact]
    public async Task ValidateAsync_CaseInsensitiveComparison_ShouldReturnTrue()
    {
        // Arrange
        string plainPassword = "TestPassword";
        string storedHash = ComputeMd5(plainPassword).ToUpperInvariant();

        // Act
        var result = await _validator.ValidateAsync(storedHash, plainPassword);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithSpecialCharacters_ShouldWork()
    {
        // Arrange
        string plainPassword = "P@ssw0rd!#$%^&*()";
        string storedHash = ComputeMd5(plainPassword);

        // Act
        var result = await _validator.ValidateAsync(storedHash, plainPassword);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithUnicodeCharacters_ShouldWork()
    {
        // Arrange
        string plainPassword = "Contraseña123";
        string storedHash = ComputeMd5(plainPassword);

        // Act
        var result = await _validator.ValidateAsync(storedHash, plainPassword);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithDifferentPasswords_SameLength_ShouldReturnFalse()
    {
        // Arrange
        string password1 = "Password1";
        string password2 = "Password2";
        string storedHash = ComputeMd5(password1);

        // Act
        var result = await _validator.ValidateAsync(storedHash, password2);

        // Assert
        result.Should().BeFalse();
    }

    private static string ComputeMd5(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        byte[] hash = MD5.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
