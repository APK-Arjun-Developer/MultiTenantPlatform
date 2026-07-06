using Application.DTOs.Auth;
using Application.Interfaces.Authentication;
using Infrastructure.Authentication.JWT;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/impersonation")]
[Authorize]
public class ImpersonationController : ApiControllerBase
{
    private readonly IImpersonationService _impersonationService;
    private readonly JwtSettings _jwtSettings;

    public ImpersonationController(
        IImpersonationService impersonationService,
        IOptions<JwtSettings> jwtSettings)
    {
        _impersonationService = impersonationService;
        _jwtSettings = jwtSettings.Value;
    }

    [HttpPost("start")]
    [Authorize(Policy = "SystemAdminOnly")]
    public async Task<IActionResult> Start([FromBody] StartImpersonationRequest request)
    {
        var adminUserId = Guid.Parse(User.FindFirstValue("user_id")!);

        var tenantIdHeader = Request.Headers["X-Tenant-Id"].ToString();
        if (!Guid.TryParse(tenantIdHeader, out var tenantId) || tenantId == Guid.Empty)
            return BadRequest(new { message = "X-Tenant-Id header is required to start impersonation." });

        var restoreToken = Request.Cookies["refresh_token"];
        if (string.IsNullOrWhiteSpace(restoreToken))
            return BadRequest(new { message = "No active session found. Please log in again." });

        var result = await _impersonationService.StartAsync(adminUserId, request.TargetUserId, tenantId, GetClientIp());

        Response.Cookies.Append("impersonation_restore_token", restoreToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = HttpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays),
        });

        SetAccessTokenCookie(result.AccessToken, result.ExpiresAt);
        Response.Cookies.Delete("refresh_token");

        return OkEnvelope(result, $"Now impersonating {result.FullName}.");
    }

    [HttpPost("stop")]
    public async Task<IActionResult> Stop()
    {
        var restoreToken = Request.Cookies["impersonation_restore_token"];
        if (string.IsNullOrWhiteSpace(restoreToken))
            return BadRequest(new { message = "No active impersonation session found." });

        var result = await _impersonationService.StopAsync(restoreToken, GetClientIp());

        Response.Cookies.Delete("impersonation_restore_token");
        SetAccessTokenCookie(result.AccessToken, result.ExpiresAt);
        SetRefreshTokenCookie(result.RefreshToken);

        return OkEnvelope(result, "Impersonation ended. Restored admin session.");
    }

    private void SetAccessTokenCookie(string token, DateTime expiresAt)
    {
        Response.Cookies.Append("access_token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = HttpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = expiresAt,
        });
    }

    private void SetRefreshTokenCookie(string token)
    {
        Response.Cookies.Append("refresh_token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = HttpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays),
        });
    }

    private string GetClientIp() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
