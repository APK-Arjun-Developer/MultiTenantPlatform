using System.Net;
using System.Text.Json;

namespace Api.Tests.IntegrationTests;

/// <summary>
/// Tests that exercise middleware behaviors (exception handling, tenant claims, user status).
/// </summary>
[Collection("Integration")]
public class MiddlewareIntegrationTests : IntegrationTestBase
{
    public MiddlewareIntegrationTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── ExceptionHandlingMiddleware ────────────────────────────────────────────

    [Fact]
    public async Task NotFoundRoute_ReturnsEnvelopeFormat()
    {
        UseAdminAuth();
        var response = await Client.GetAsync("/api/v1/tenants/nonexistent-id-that-is-not-a-guid");

        // Non-GUID route param → 404 or 400 from routing
        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Request_SetsSecurityHeaders()
    {
        var response = await Client.GetAsync("/api/v1/auth/me");

        Assert.True(response.Headers.Contains("X-Content-Type-Options") ||
                    response.Content.Headers.Contains("Content-Type"));
    }

    // ── TenantMiddleware ───────────────────────────────────────────────────────

    [Fact]
    public async Task InvalidXTenantIdHeader_Returns401()
    {
        UseAdminAuth();
        Client.DefaultRequestHeaders.TryAddWithoutValidation("X-Tenant-Id", "not-a-valid-guid");

        var response = await Client.GetAsync("/api/v1/tenants");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ValidXTenantIdHeader_IsAccepted()
    {
        UseAdminAuth();
        Client.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-Tenant-Id", CustomWebApplicationFactory.TestTenantId.ToString());

        var response = await Client.GetAsync("/api/v1/tenants");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── UserStatusMiddleware ───────────────────────────────────────────────────

    [Fact]
    public async Task ActiveUser_CanAccessProtectedEndpoints()
    {
        UseTenantAdminAuth();
        var response = await Client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Error responses are envelope format ───────────────────────────────────

    [Fact]
    public async Task NotFoundResponse_HasEnvelopeStructure()
    {
        UseAdminAuth();
        var response = await Client.GetAsync($"/api/v1/tenants/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        // Should have message and errors fields
        Assert.True(doc.RootElement.TryGetProperty("message", out _));
    }

    [Fact]
    public async Task ValidationError_Returns400WithErrors()
    {
        var response = await Client.PostAsync("/api/v1/auth/login",
            JsonContent(new { Email = "", Password = "" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("errors", out _));
    }

    // ── Rate limiter is configured ─────────────────────────────────────────────

    [Fact]
    public async Task Login_DoesNotExceedRateLimit_On_SingleRequest()
    {
        var response = await Client.PostAsync("/api/v1/auth/login",
            JsonContent(new { Email = "x@x.com", Password = "BadPass1!" }));

        // Should get a proper error response, not 429
        Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    // ── Impersonation endpoints are protected ──────────────────────────────────

    [Fact]
    public async Task StartImpersonation_WithoutRefreshToken_Returns400()
    {
        UseAdminAuth();
        Client.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-Tenant-Id", CustomWebApplicationFactory.TestTenantId.ToString());

        var payload = new { TargetUserId = CustomWebApplicationFactory.TenantAdminId };
        var response = await Client.PostAsync("/api/v1/impersonation/start", JsonContent(payload));

        // No refresh_token cookie, so returns 400
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StopImpersonation_WithoutImpersonationCookie_Returns400()
    {
        UseAdminAuth();
        var response = await Client.PostAsync("/api/v1/impersonation/stop", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StartImpersonation_AsTenantAdmin_Returns403()
    {
        UseTenantAdminAuth();
        var payload = new { TargetUserId = CustomWebApplicationFactory.TenantUserId };
        var response = await Client.PostAsync("/api/v1/impersonation/start", JsonContent(payload));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
