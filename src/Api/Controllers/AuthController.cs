using Application.DTOs.Auth;
using Application.Interfaces.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ApiControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(
        IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var response = await _authService.LoginAsync(request, GetIpAddress());

        return OkEnvelope(response, "Login successful.");
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request)
    {
        var response = await _authService.RefreshTokenAsync(request, GetIpAddress());

        return OkEnvelope(response, "Token refreshed.");
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutRequest request)
    {
        await _authService.LogoutAsync(request, GetIpAddress());

        return OkEnvelope("Logged out.");
    }

    private string GetIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}