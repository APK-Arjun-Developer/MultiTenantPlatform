using Application.DTOs.Auth;
using Application.Interfaces.Authentication;
using Infrastructure.Authentication.JWT;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ApiControllerBase
{
    private readonly IAuthService _authService;
    private readonly IPasswordResetService _passwordResetService;
    private readonly JwtSettings _jwtSettings;

    public AuthController(
        IAuthService authService,
        IPasswordResetService passwordResetService,
        IOptions<JwtSettings> jwtSettings)
    {
        _authService = authService;
        _passwordResetService = passwordResetService;
        _jwtSettings = jwtSettings.Value;
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var response = await _authService.GetMeAsync(User);
        return OkEnvelope(response);
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var response = await _authService.LoginAsync(request, GetClientIp());
        SetAccessTokenCookie(response.AccessToken, response.ExpiresAt);
        SetRefreshTokenCookie(response.RefreshToken);
        return OkEnvelope(response, "Login successful.");
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh()
    {
        var refreshToken = Request.Cookies["refresh_token"];
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized(new { message = "Refresh token cookie is missing." });
        }

        var response = await _authService.RefreshTokenAsync(refreshToken, GetClientIp());
        SetAccessTokenCookie(response.AccessToken, response.ExpiresAt);
        SetRefreshTokenCookie(response.RefreshToken);
        return OkEnvelope(response, "Token refreshed.");
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = Request.Cookies["refresh_token"];
        await _authService.LogoutAsync(refreshToken, GetClientIp());
        Response.Cookies.Delete("access_token");
        Response.Cookies.Delete("refresh_token");
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

    private void SetAccessTokenCookie(string token, DateTime expiresAt)
    {
        Response.Cookies.Append("access_token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = HttpContext.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = expiresAt,
        });
    }

    private void SetRefreshTokenCookie(string token)
    {
        Response.Cookies.Append("refresh_token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = HttpContext.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays),
        });
    }

    private string GetClientIp()
    {
        // X-Forwarded-For is populated by UseForwardedHeaders middleware
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
