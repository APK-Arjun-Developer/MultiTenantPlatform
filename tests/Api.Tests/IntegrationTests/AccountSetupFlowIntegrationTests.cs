using System.Net;
using System.Text.Json;

namespace Api.Tests.IntegrationTests;

[Collection("Integration")]
public class AccountSetupFlowIntegrationTests : IntegrationTestBase
{
    public AccountSetupFlowIntegrationTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Validate setup token ──────────���───────────────────────────────────────

    [Fact]
    public async Task ValidateSetupToken_ValidToken_ReturnsValidTrue()
    {
        // TestResendSetupToken is not consumed by SetPassword (only ValidateToken checks it)
        var response = await Client.GetAsync(
            $"/api/v1/account-setup/validate?token={CustomWebApplicationFactory.TestResendSetupToken}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadEnvelopeDataAsync<JsonElement>(response);
        Assert.True(data.GetProperty("isValid").GetBoolean());
        Assert.Equal(CustomWebApplicationFactory.TestValidateTokenEmail,
            data.GetProperty("email").GetString());
    }

    // ── Set password (activates account) ─────────────────────────────────────

    [Fact]
    public async Task SetPassword_ValidToken_Returns200AndActivatesAccount()
    {
        var payload = new
        {
            Token = CustomWebApplicationFactory.TestAccountSetupToken,
            Password = "NewP@ssw0rd1!",
            ConfirmPassword = "NewP@ssw0rd1!",
            FullName = "Activated User",
        };

        var response = await Client.PostAsync("/api/v1/account-setup/set-password",
            JsonContent(payload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadEnvelopeDataAsync<JsonElement>(response);
        Assert.True(data.GetProperty("isActive").GetBoolean());
        Assert.Equal(CustomWebApplicationFactory.TestInactiveUserEmail,
            data.GetProperty("email").GetString());
    }

    [Fact]
    public async Task SetPassword_AlreadyActive_Returns409()
    {
        // After SetPassword_ValidToken_Returns200AndActivatesAccount runs, the inactive user
        // is now active. Attempting to use the same token again returns 400 (invalid/used).
        var payload = new
        {
            Token = CustomWebApplicationFactory.TestAccountSetupToken,
            Password = "Another1!",
            ConfirmPassword = "Another1!",
        };

        var response = await Client.PostAsync("/api/v1/account-setup/set-password",
            JsonContent(payload));

        // 400 = token is used/invalid; 409 = account already set up
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Conflict);
    }

    // ── Resend setup email ────────────────────────────────────────────────────

    [Fact]
    public async Task ResendSetupEmail_TenantUser_ReturnsOk()
    {
        UseTenantAdminAuth();

        // Create a new inactive user dynamically (CreateUserAsync sets IsActive = false)
        var email = $"resend-setup-{Guid.NewGuid():N}@test.com";
        var createResponse = await Client.PostAsync("/api/v1/users",
            JsonContent(new
            {
                FullName = "Resend Test User",
                Email = email,
                RoleIds = new[] { CustomWebApplicationFactory.TestRoleId },
            }));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await ReadEnvelopeDataAsync<JsonElement>(createResponse);
        var userId = Guid.Parse(created.GetProperty("id").GetString()!);

        // Resend setup email for this inactive user
        var resendResponse = await Client.PostAsync(
            $"/api/v1/users/{userId}/resend", null);

        Assert.Equal(HttpStatusCode.OK, resendResponse.StatusCode);
        var msg = await ReadEnvelopeMessageAsync(resendResponse);
        Assert.Contains("resent", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResendSetupEmail_TenantAdmin_ReturnsOk()
    {
        UseAdminAuth();
        Client.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-Tenant-Id", CustomWebApplicationFactory.TestTenantId.ToString());

        // Create an inactive tenant admin via direct-create endpoint
        var email = $"resend-tadmin-{Guid.NewGuid():N}@test.com";
        var createResponse = await Client.PostAsync("/api/v1/tenant-admins",
            JsonContent(new
            {
                TenantId = CustomWebApplicationFactory.TestTenantId,
                FullName = "Resend TenantAdmin Test",
                Email = email,
                Address = new { Line1 = "1 Admin St", City = "Testville", PostalCode = "12345", Country = "US" },
            }));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await ReadEnvelopeDataAsync<JsonElement>(createResponse);
        var userId = Guid.Parse(created.GetProperty("userId").GetString()!);

        // Resend setup email for this inactive tenant admin
        var resendResponse = await Client.PostAsync(
            $"/api/v1/tenant-admins/{userId}/resend", null);

        Assert.Equal(HttpStatusCode.OK, resendResponse.StatusCode);
        var msg = await ReadEnvelopeMessageAsync(resendResponse);
        Assert.Contains("resent", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResendSetupEmail_ActiveUser_Returns409()
    {
        UseTenantAdminAuth();

        // Active user — resend should fail with 409 (user is not inactive)
        var resendResponse = await Client.PostAsync(
            $"/api/v1/users/{CustomWebApplicationFactory.TenantUserId}/resend", null);

        Assert.Equal(HttpStatusCode.Conflict, resendResponse.StatusCode);
    }

    // ── DirectCreate (POST /api/v1/users/direct-create) ─────────────────────

    [Fact]
    public async Task DirectCreate_ValidRequest_ReturnsOk()
    {
        UseTenantAdminAuth();
        var email = $"direct-create-{Guid.NewGuid():N}@test.com";
        var payload = new
        {
            FullName = "Direct Created User",
            Email = email,
            RoleIds = new[] { CustomWebApplicationFactory.TestRoleId },
            Address = new { Line1 = "1 Test St", City = "Testville", PostalCode = "12345", Country = "US" },
        };

        var response = await Client.PostAsync("/api/v1/users/direct-create",
            JsonContent(payload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadEnvelopeDataAsync<JsonElement>(response);
        Assert.Equal(email, data.GetProperty("email").GetString());
        Assert.False(data.GetProperty("isActive").GetBoolean()); // inactive until setup
    }

    [Fact]
    public async Task DirectCreate_DuplicateEmail_Returns409()
    {
        UseTenantAdminAuth();
        var payload = new
        {
            FullName = "Dup",
            Email = CustomWebApplicationFactory.TenantUserEmail, // already exists
            RoleIds = new[] { CustomWebApplicationFactory.TestRoleId },
            Address = new { Line1 = "1 Test St", City = "Testville", PostalCode = "12345", Country = "US" },
        };

        var response = await Client.PostAsync("/api/v1/users/direct-create",
            JsonContent(payload));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── CreateTenantAdmin (POST /api/v1/tenant-admins) ──────────────��────────

    [Fact]
    public async Task CreateTenantAdmin_AsSystemAdmin_ReturnsOk()
    {
        UseAdminAuth();
        Client.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-Tenant-Id", CustomWebApplicationFactory.TestTenantId.ToString());

        var email = $"new-ta-direct-{Guid.NewGuid():N}@test.com";
        var payload = new
        {
            TenantId = CustomWebApplicationFactory.TestTenantId,
            FullName = "New Direct TenantAdmin",
            Email = email,
            Address = new { Line1 = "1 St", City = "City", PostalCode = "12345", Country = "US" },
        };

        var response = await Client.PostAsync("/api/v1/tenant-admins", JsonContent(payload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadEnvelopeDataAsync<JsonElement>(response);
        Assert.Equal(email, data.GetProperty("email").GetString());
        Assert.False(data.GetProperty("isActive").GetBoolean()); // inactive until setup
    }
}
