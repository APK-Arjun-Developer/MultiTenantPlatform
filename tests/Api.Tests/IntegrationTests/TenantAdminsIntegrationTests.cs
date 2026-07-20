using System.Net;
using System.Text.Json;

namespace Api.Tests.IntegrationTests;

[Collection("Integration")]
public class TenantAdminsIntegrationTests : IntegrationTestBase
{
    public TenantAdminsIntegrationTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── GetAll ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_AsSystemAdmin_ReturnsPagedTenantAdmins()
    {
        UseAdminAuth();
        var response = await Client.GetAsync("/api/v1/tenant-admins");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadEnvelopeDataAsync<JsonElement>(response);
        Assert.True(data.GetProperty("totalCount").GetInt32() >= 1);
    }

    [Fact]
    public async Task GetAll_WithSearch_ReturnsFilteredResults()
    {
        UseAdminAuth();
        var response = await Client.GetAsync("/api/v1/tenant-admins?search=Test");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_AsTenantAdmin_Returns403()
    {
        UseTenantAdminAuth();
        var response = await Client.GetAsync("/api/v1/tenant-admins");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── GetById ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ExistingTenantAdmin_ReturnsOk()
    {
        UseAdminAuth();
        var response = await Client.GetAsync(
            $"/api/v1/tenant-admins/{CustomWebApplicationFactory.TenantAdminId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        UseAdminAuth();
        var response = await Client.GetAsync($"/api/v1/tenant-admins/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Update ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ValidRequest_ReturnsOk()
    {
        UseAdminAuth();
        var payload = new
        {
            FullName = "Updated Tenant Admin",
        };

        var response = await Client.PutAsync(
            $"/api/v1/tenant-admins/{CustomWebApplicationFactory.TenantAdminId}",
            JsonContent(payload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Invitations (via tenant-admins) ───────────────────────────────────────

    [Fact]
    public async Task InviteTenantAdmin_AsSystemAdmin_ReturnsOk()
    {
        UseAdminAuth();
        Client.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-Tenant-Id", CustomWebApplicationFactory.TestTenantId.ToString());

        // InviteTenantAdminRequest requires TenantId (not just Email)
        var payload = new
        {
            TenantId = CustomWebApplicationFactory.TestTenantId,
            Email = $"new-admin-{Guid.NewGuid():N}@test.com",
        };
        var response = await Client.PostAsync("/api/v1/tenant-admins/invite", JsonContent(payload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTenantAdminInvitations_AsSystemAdmin_ReturnsOk()
    {
        UseAdminAuth();
        Client.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-Tenant-Id", CustomWebApplicationFactory.TestTenantId.ToString());

        var response = await Client.GetAsync("/api/v1/tenant-admins/invitations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── AccountSetup ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateSetupToken_InvalidToken_ReturnsOkWithInvalid()
    {
        var response = await Client.GetAsync("/api/v1/account-setup/validate?token=invalid-token");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var msg = await ReadEnvelopeMessageAsync(response);
        Assert.NotNull(msg);
    }

    [Fact]
    public async Task SetPassword_InvalidToken_Returns400()
    {
        var payload = new { Token = "invalid-token", Password = "NewPass1!" };
        var response = await Client.PostAsync("/api/v1/account-setup/set-password", JsonContent(payload));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
