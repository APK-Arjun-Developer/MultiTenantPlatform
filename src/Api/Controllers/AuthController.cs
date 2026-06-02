using Application.DTOs.Auth;
using Application.Interfaces.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ApiControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var response = await _authService.LoginAsync(request, GetClientIp());

        return OkEnvelope(response, "Login successful.");
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request)
    {
        var response = await _authService.RefreshTokenAsync(request, GetClientIp());

        return OkEnvelope(response, "Token refreshed.");
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutRequest request)
    {
        await _authService.LogoutAsync(request, GetClientIp());

        return OkEnvelope("Logged out.");
    }

    private string GetClientIp()
    {
        // X-Forwarded-For is populated by UseForwardedHeaders middleware
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
