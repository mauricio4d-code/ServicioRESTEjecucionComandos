using Microsoft.AspNetCore.Mvc;
using ServicioRESTEjecucionComandos.DTOs;
using ServicioRESTEjecucionComandos.Services;

namespace ServicioRESTEjecucionComandos.Controllers;

/// <summary>
/// Controller for authentication endpoints: Login, Refresh, Logout.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Authenticates a user and returns JWT access token + refresh token.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Validation failed.",
                Details = string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))
            });
        }

        var clientIp = GetClientIp();
        var userAgent = Request.Headers["User-Agent"].ToString();

        var response = await _authService.LoginAsync(request.Email, request.Password, clientIp, userAgent);

        if (response == null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid email, password, or user account is not active."
            });
        }

        return Ok(response);
    }

    /// <summary>
    /// Refreshes the JWT access token using a valid refresh token.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Refresh token is required."
            });
        }

        var clientIp = GetClientIp();
        var userAgent = Request.Headers["User-Agent"].ToString();

        var response = await _authService.RefreshTokenAsync(request.Token, clientIp, userAgent);

        if (response == null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid, expired, or revoked refresh token."
            });
        }

        return Ok(response);
    }

    /// <summary>
    /// Logs out the user by revoking the refresh token.
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Refresh token is required."
            });
        }

        var clientIp = GetClientIp();

        await _authService.LogoutAsync(request.Token, clientIp);

        return Ok(new { Message = "Logout successful." });
    }

    /// <summary>
    /// Gets the client IP address from the request context.
    /// </summary>
    private string? GetClientIp()
    {
        if (HttpContext.Connection.RemoteIpAddress != null)
        {
            return HttpContext.Connection.RemoteIpAddress.ToString();
        }
        return null;
    }
}