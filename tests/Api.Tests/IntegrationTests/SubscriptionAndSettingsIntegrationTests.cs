using System.Net;
using System.Text.Json;

namespace Api.Tests.IntegrationTests;

[Collection("Integration")]
public class SubscriptionAndSettingsIntegrationTests : IntegrationTestBase
{
    public SubscriptionAndSettingsIntegrationTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Subscription Plans ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetPlans_AsSystemAdmin_ReturnsOk()
    {
        UseAdminAuth();
        var response = await Client.GetAsync("/api/v1/subscriptions/plans");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var msg = await ReadEnvelopeMessageAsync(response);
        Assert.Contains("plans retrieved", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetPlans_Unauthenticated_Returns401()
    {
        ClearAuth();
        var response = await Client.GetAsync("/api/v1/subscriptions/plans");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPlans_AsTenantAdmin_Returns403()
    {
        UseTenantAdminAuth();
        var response = await Client.GetAsync("/api/v1/subscriptions/plans");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTenantPlan_AsSystemAdmin_ReturnsOk()
    {
        UseAdminAuth();
        var payload = new
        {
            TenantId = CustomWebApplicationFactory.TestTenantId,
            PlanType = "Free",
        };

        var response = await Client.PutAsync("/api/v1/subscriptions/tenant-plan", JsonContent(payload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Tenant Settings ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTenantSettings_AsTenantAdmin_ReturnsOk()
    {
        UseTenantAdminAuth();
        var response = await Client.GetAsync("/api/v1/tenant-settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var msg = await ReadEnvelopeMessageAsync(response);
        Assert.Contains("settings retrieved", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetTenantSettings_AsSystemAdmin_Returns403()
    {
        UseAdminAuth();
        var response = await Client.GetAsync("/api/v1/tenant-settings");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTenantSettings_AsTenantAdmin_ReturnsOk()
    {
        UseTenantAdminAuth();
        var payload = new { Name = "Test Corp Updated Settings" };

        var response = await Client.PutAsync("/api/v1/tenant-settings", JsonContent(payload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTenantSettings_AsTenantUser_Returns403()
    {
        UseTenantUserAuth();
        var payload = new { Name = "Unauthorized Update" };

        var response = await Client.PutAsync("/api/v1/tenant-settings", JsonContent(payload));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
