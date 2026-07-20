using Application.DTOs.Auth;
using System.Net;
using System.Text.Json;

namespace Api.Tests.IntegrationTests;

[Collection("Integration")]
public class AuthIntegrationTests : IntegrationTestBase
{
    public AuthIntegrationTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Login ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidAdminCredentials_ReturnsOkWithTokens()
    {
        var response = await Client.PostAsync("/api/v1/auth/login",
            JsonContent(new { Email = CustomWebApplicationFactory.AdminEmail, Password = CustomWebApplicationFactory.AdminPassword }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var msg = await ReadEnvelopeMessageAsync(response);
        Assert.Equal("Login successful.", msg);

        // Access token cookie should be set
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies, c => c.StartsWith("access_token="));
    }

    [Fact]
    public async Task Login_ValidTenantAdminCredentials_ReturnsOk()
    {
        var response = await Client.PostAsync("/api/v1/auth/login",
            JsonContent(new { Email = CustomWebApplicationFactory.TenantAdminEmail, Password = CustomWebApplicationFactory.UserPassword }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns400()
    {
        var response = await Client.PostAsync("/api/v1/auth/login",
            JsonContent(new { Email = CustomWebApplicationFactory.AdminEmail, Password = "WrongPassword1!" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns400()
    {
        var response = await Client.PostAsync("/api/v1/auth/login",
            JsonContent(new { Email = "nobody@nowhere.com", Password = "SomePass1!" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_InvalidEmailFormat_Returns400()
    {
        var response = await Client.PostAsync("/api/v1/auth/login",
            JsonContent(new { Email = "not-an-email", Password = "P@ssw0rd!" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_EmptyPassword_Returns400()
    {
        var response = await Client.PostAsync("/api/v1/auth/login",
            JsonContent(new { Email = CustomWebApplicationFactory.AdminEmail, Password = "" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Me ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Me_AuthenticatedAdmin_ReturnsUserInfo()
    {
        UseAdminAuth();
        var response = await Client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadEnvelopeDataAsync<JsonElement>(response);
        Assert.Equal(CustomWebApplicationFactory.AdminEmail, data.GetProperty("email").GetString());
        Assert.Equal("SystemAdmin", data.GetProperty("systemRole").GetString());
        // SystemAdmin should have all permissions
        var perms = data.GetProperty("permissions").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.NotEmpty(perms);
    }

    [Fact]
    public async Task Me_Unauthenticated_Returns401()
    {
        ClearAuth();
        var response = await Client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_TenantAdmin_ReturnsTenantPermissions()
    {
        UseTenantAdminAuth();
        var response = await Client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadEnvelopeDataAsync<JsonElement>(response);
        Assert.Equal(CustomWebApplicationFactory.TenantAdminEmail, data.GetProperty("email").GetString());
        Assert.Equal("TenantAdmin", data.GetProperty("systemRole").GetString());
    }

    [Fact]
    public async Task Me_TenantUser_ReturnsEmptyPermissions()
    {
        UseTenantUserAuth();
        var response = await Client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadEnvelopeDataAsync<JsonElement>(response);
        // TenantUser with no custom role assignments has empty permissions
        var perms = data.GetProperty("permissions").EnumerateArray().ToList();
        Assert.Empty(perms);
    }

    // ── Logout ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_Authenticated_ReturnsOk()
    {
        UseAdminAuth();
        var response = await Client.PostAsync("/api/v1/auth/logout", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var msg = await ReadEnvelopeMessageAsync(response);
        Assert.Contains("Logged out", msg);
    }

    [Fact]
    public async Task Logout_Unauthenticated_Returns401()
    {
        ClearAuth();
        var response = await Client.PostAsync("/api/v1/auth/logout", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── ForgotPassword ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_AnyEmail_ReturnsOk()
    {
        var response = await Client.PostAsync("/api/v1/auth/forgot-password",
            JsonContent(new { Email = "anyone@example.com" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_KnownEmail_StillReturnsOk()
    {
        var response = await Client.PostAsync("/api/v1/auth/forgot-password",
            JsonContent(new { Email = CustomWebApplicationFactory.AdminEmail }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var msg = await ReadEnvelopeMessageAsync(response);
        Assert.Contains("password reset link", msg, StringComparison.OrdinalIgnoreCase);
    }

    // ── Refresh ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_MissingCookie_Returns401()
    {
        ClearAuth();
        var response = await Client.PostAsync("/api/v1/auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── VerifyEmail / ResendVerification ──────────────────────────────────────

    [Fact]
    public async Task VerifyEmail_InvalidOtp_Returns400()
    {
        var response = await Client.PostAsync("/api/v1/auth/verify-email",
            JsonContent(new { Email = "user@test.com", Otp = "000000" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResendVerification_AnyEmail_ReturnsOk()
    {
        var response = await Client.PostAsync("/api/v1/auth/resend-verification",
            JsonContent(new { Email = "anyone@example.com" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Full login+refresh flow ─────────────────────────────────────────────────

    [Fact]
    public async Task LoginThenRefresh_Success()
    {
        // Step 1: login
        var loginResponse = await Client.PostAsync("/api/v1/auth/login",
            JsonContent(new { Email = CustomWebApplicationFactory.AdminEmail, Password = CustomWebApplicationFactory.AdminPassword }));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        // Step 2: use the refresh token cookie from login to call refresh
        // The TestServer HTTP client manages cookies automatically via HandleCookies = true
        var refreshResponse = await Client.PostAsync("/api/v1/auth/refresh", null);

        // Should succeed because the cookie was set by login
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var msg = await ReadEnvelopeMessageAsync(refreshResponse);
        Assert.Contains("refreshed", msg, StringComparison.OrdinalIgnoreCase);
    }
}
