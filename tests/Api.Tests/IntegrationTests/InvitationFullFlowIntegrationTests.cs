using System.Net;
using System.Text.Json;

namespace Api.Tests.IntegrationTests;

[Collection("Integration")]
public class InvitationFullFlowIntegrationTests : IntegrationTestBase
{
    public InvitationFullFlowIntegrationTests(CustomWebApplicationFactory factory) : base(factory) { }

    private static object AddressPayload() =>
        new { Line1 = "1 Test St", City = "Testville", PostalCode = "12345", Country = "US" };

    // Validate

    [Fact]
    public async Task ValidateInvitation_ValidToken_ReturnsValidTrue()
    {
        var response = await Client.GetAsync(
            $"/api/v1/invitations/validate?token={CustomWebApplicationFactory.TestValidateInviteToken}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadEnvelopeDataAsync<JsonElement>(response);
        Assert.True(data.GetProperty("isValid").GetBoolean());
        Assert.Equal("validate-invite@test.com", data.GetProperty("email").GetString());
    }

    // Accept TenantAdmin Invitation

    [Fact]
    public async Task AcceptTenantAdmin_ValidToken_Returns200()
    {
        var payload = new
        {
            Token = CustomWebApplicationFactory.TestTenantAdminInviteToken,
            FullName = "Accepted Admin User",
            Password = "P@ssw0rd1!",
            ConfirmPassword = "P@ssw0rd1!",
            Address = AddressPayload(),
        };

        var response = await Client.PostAsync("/api/v1/invitations/accept/tenant-admin",
            JsonContent(payload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadEnvelopeDataAsync<JsonElement>(response);
        Assert.Equal("ta-invite-accept@test.com", data.GetProperty("email").GetString());
        Assert.True(data.GetProperty("isActive").GetBoolean());
    }

    // Accept TenantUser Invitation

    [Fact]
    public async Task AcceptTenantUser_ValidToken_Returns200()
    {
        var payload = new
        {
            Token = CustomWebApplicationFactory.TestTenantUserInviteToken,
            FullName = "Accepted Tenant User",
            Password = "P@ssw0rd1!",
            ConfirmPassword = "P@ssw0rd1!",
            Address = AddressPayload(),
        };

        var response = await Client.PostAsync("/api/v1/invitations/accept/user",
            JsonContent(payload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadEnvelopeDataAsync<JsonElement>(response);
        Assert.Equal("tu-invite-accept@test.com", data.GetProperty("email").GetString());
        Assert.True(data.GetProperty("isActive").GetBoolean());
    }

    // Accept NewTenant Invitation

    [Fact]
    public async Task AcceptNewTenant_ValidToken_Returns200()
    {
        var addr = AddressPayload();
        var payload = new
        {
            Token = CustomWebApplicationFactory.TestNewTenantInviteToken,
            TenantName = $"New Tenant From Invite {Guid.NewGuid():N}",
            FullName = "New Tenant Founder",
            Password = "P@ssw0rd1!",
            ConfirmPassword = "P@ssw0rd1!",
            TenantAddress = addr,
            UserAddress = addr,
        };

        var response = await Client.PostAsync("/api/v1/invitations/accept/new-tenant",
            JsonContent(payload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadEnvelopeDataAsync<JsonElement>(response);
        Assert.Equal("nt-invite-accept@test.com", data.GetProperty("email").GetString());
        Assert.True(data.GetProperty("isActive").GetBoolean());
    }

    // Revoke Invitation

    [Fact]
    public async Task RevokeInvitation_ThenRevokeSame_SecondReturns409()
    {
        // Create, revoke once (OK), revoke again (Conflict)
        UseTenantAdminAuth();
        var inviteResponse = await Client.PostAsync("/api/v1/users/invite",
            JsonContent(new
            {
                Email = $"revoke-flow-{Guid.NewGuid():N}@test.com",
                RoleIds = new[] { CustomWebApplicationFactory.TestRoleId },
            }));
        Assert.Equal(HttpStatusCode.OK, inviteResponse.StatusCode);
        var inviteData = await ReadEnvelopeDataAsync<JsonElement>(inviteResponse);
        var invitationId = Guid.Parse(inviteData.GetProperty("invitationId").GetString()!);

        var firstRevoke = await Client.PostAsync(
            $"/api/v1/users/invitations/{invitationId}/revoke", null);
        Assert.Equal(HttpStatusCode.OK, firstRevoke.StatusCode);

        var secondRevoke = await Client.PostAsync(
            $"/api/v1/users/invitations/{invitationId}/revoke", null);
        Assert.Equal(HttpStatusCode.Conflict, secondRevoke.StatusCode);
    }

    // Resend Invitation

    [Fact]
    public async Task ResendInvitation_AsSystemAdmin_ReturnsOk()
    {
        UseAdminAuth();
        Client.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-Tenant-Id", CustomWebApplicationFactory.TestTenantId.ToString());

        var response = await Client.PostAsync(
            $"/api/v1/tenant-admins/invitations/{CustomWebApplicationFactory.TestResendInviteId}/resend",
            null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var msg = await ReadEnvelopeMessageAsync(response);
        Assert.Contains("resent", msg, StringComparison.OrdinalIgnoreCase);
    }

    // Invite + Revoke via TenantController

    [Fact]
    public async Task InviteTenant_ThenRevoke_ReturnsOk()
    {
        UseAdminAuth();
        var inviteEmail = $"tenant-invite-revoke-{Guid.NewGuid():N}@test.com";

        var inviteResponse = await Client.PostAsync("/api/v1/tenants/invite",
            JsonContent(new { Email = inviteEmail }));
        Assert.Equal(HttpStatusCode.OK, inviteResponse.StatusCode);
        var inviteData = await ReadEnvelopeDataAsync<JsonElement>(inviteResponse);
        var invitationId = Guid.Parse(inviteData.GetProperty("invitationId").GetString()!);

        var revokeResponse = await Client.PostAsync(
            $"/api/v1/tenants/invitations/{invitationId}/revoke", null);
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
    }

    [Fact]
    public async Task InviteTenant_ThenResend_ReturnsOk()
    {
        UseAdminAuth();
        var inviteEmail = $"tenant-invite-resend-{Guid.NewGuid():N}@test.com";

        var inviteResponse = await Client.PostAsync("/api/v1/tenants/invite",
            JsonContent(new { Email = inviteEmail }));
        Assert.Equal(HttpStatusCode.OK, inviteResponse.StatusCode);
        var inviteData = await ReadEnvelopeDataAsync<JsonElement>(inviteResponse);
        var invitationId = Guid.Parse(inviteData.GetProperty("invitationId").GetString()!);

        var resendResponse = await Client.PostAsync(
            $"/api/v1/tenants/invitations/{invitationId}/resend", null);
        Assert.Equal(HttpStatusCode.OK, resendResponse.StatusCode);
    }

    [Fact]
    public async Task InviteUser_ThenRevoke_ReturnsOk()
    {
        UseTenantAdminAuth();
        var inviteEmail = $"user-invite-revoke-{Guid.NewGuid():N}@test.com";

        var inviteResponse = await Client.PostAsync("/api/v1/users/invite",
            JsonContent(new
            {
                Email = inviteEmail,
                RoleIds = new[] { CustomWebApplicationFactory.TestRoleId },
            }));
        Assert.Equal(HttpStatusCode.OK, inviteResponse.StatusCode);
        var inviteData = await ReadEnvelopeDataAsync<JsonElement>(inviteResponse);
        var invitationId = Guid.Parse(inviteData.GetProperty("invitationId").GetString()!);

        var revokeResponse = await Client.PostAsync(
            $"/api/v1/users/invitations/{invitationId}/revoke", null);
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
    }

    [Fact]
    public async Task InviteUser_ThenResend_ReturnsOk()
    {
        UseTenantAdminAuth();
        var inviteEmail = $"user-invite-resend-{Guid.NewGuid():N}@test.com";

        var inviteResponse = await Client.PostAsync("/api/v1/users/invite",
            JsonContent(new
            {
                Email = inviteEmail,
                RoleIds = new[] { CustomWebApplicationFactory.TestRoleId },
            }));
        Assert.Equal(HttpStatusCode.OK, inviteResponse.StatusCode);
        var inviteData = await ReadEnvelopeDataAsync<JsonElement>(inviteResponse);
        var invitationId = Guid.Parse(inviteData.GetProperty("invitationId").GetString()!);

        var resendResponse = await Client.PostAsync(
            $"/api/v1/users/invitations/{invitationId}/resend", null);
        Assert.Equal(HttpStatusCode.OK, resendResponse.StatusCode);
    }

    [Fact]
    public async Task InviteTenantAdmin_ThenRevoke_ReturnsOk()
    {
        UseAdminAuth();
        var inviteEmail = $"ta-invite-revoke-{Guid.NewGuid():N}@test.com";

        var inviteResponse = await Client.PostAsync("/api/v1/tenant-admins/invite",
            JsonContent(new
            {
                TenantId = CustomWebApplicationFactory.TestTenantId,
                Email = inviteEmail,
            }));
        Assert.Equal(HttpStatusCode.OK, inviteResponse.StatusCode);
        var inviteData = await ReadEnvelopeDataAsync<JsonElement>(inviteResponse);
        var invitationId = Guid.Parse(inviteData.GetProperty("invitationId").GetString()!);

        var revokeResponse = await Client.PostAsync(
            $"/api/v1/tenant-admins/invitations/{invitationId}/revoke", null);
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
    }

    // Invitation status filters

    [Fact]
    public async Task GetTenantCreationInvitations_WithStatusFilter_ReturnsOk()
    {
        UseAdminAuth();
        var response = await Client.GetAsync("/api/v1/tenants/invitations?status=pending");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadEnvelopeDataAsync<JsonElement>(response);
        Assert.True(data.GetProperty("totalCount").GetInt32() >= 0);
    }

    [Fact]
    public async Task GetTenantAdminInvitations_WithStatusFilter_ReturnsOk()
    {
        UseAdminAuth();
        var response = await Client.GetAsync("/api/v1/tenant-admins/invitations?status=accepted");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetUserInvitations_WithStatusFilter_ReturnsOk()
    {
        UseTenantAdminAuth();
        var response = await Client.GetAsync("/api/v1/users/invitations?status=revoked");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
