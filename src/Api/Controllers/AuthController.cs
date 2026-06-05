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
    private readonly IPasswordResetService _passwordResetService;

    public AuthController(IAuthService authService, IPasswordResetService passwordResetService)
    {
        _authService = authService;
        _passwordResetService = passwordResetService;
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

    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await _passwordResetService.SendResetEmailAsync(request, cancellationToken);

        // Always return success to prevent email enumeration attacks.
        return OkEnvelope("If an account with that email exists, a reset link has been sent.");
    }

    [HttpGet("reset-password/validate")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ValidateResetToken(
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        var response = await _passwordResetService.ValidateTokenAsync(token, cancellationToken);

        return OkEnvelope(response, "Token validated.");
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await _passwordResetService.ResetPasswordAsync(request, cancellationToken);

        return OkEnvelope("Password has been reset successfully.");
    }

    private string GetClientIp()
    {
        // X-Forwarded-For is populated by UseForwardedHeaders middleware
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
