using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ServicioRESTEjecucionComandos.Models;

namespace ServicioRESTEjecucionComandos.Services;

/// <summary>
/// Service for generating and validating JWT access tokens.
/// </summary>
public class JwtService
{
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenMinutes;

    public JwtService(IConfiguration configuration)
    {
        _secretKey = configuration["Jwt:SecretKey"] 
            ?? throw new InvalidOperationException("JWT SecretKey is not configured.");
        _issuer = configuration["Jwt:Issuer"] 
            ?? throw new InvalidOperationException("JWT Issuer is not configured.");
        _audience = configuration["Jwt:Audience"] 
            ?? throw new InvalidOperationException("JWT Audience is not configured.");
        _accessTokenMinutes = configuration.GetValue<int>("Jwt:AccessTokenMinutes", 5);
    }

    /// <summary>
    /// Generates a JWT access token for the given user and role.
    /// </summary>
    public string GenerateToken(User user, UserRole role)
    {
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey)),
            SecurityAlgorithms.HmacSha256
        );

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.GivenName, user.Firstname ?? string.Empty),
            new Claim(ClaimTypes.Surname, user.Lastname ?? string.Empty),
            new Claim(ClaimTypes.Role, role.Name)
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_accessTokenMinutes),
            signingCredentials: signingCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Gets the token expiration in seconds.
    /// </summary>
    public int GetExpiresInSeconds()
    {
        return _accessTokenMinutes * 60;
    }
}
