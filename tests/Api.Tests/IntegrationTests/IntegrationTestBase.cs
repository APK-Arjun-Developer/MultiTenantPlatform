using Domain.Enums;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Api.Tests.IntegrationTests;

public abstract class IntegrationTestBase : IClassFixture<CustomWebApplicationFactory>
{
    protected readonly CustomWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
    }

    // ── JWT helpers ────────────────────────────────────────────────────────────

    protected string GenerateToken(
        Guid userId,
        string email,
        string fullName,
        Guid tenantId,
        SystemRole role,
        IEnumerable<(Guid Id, string Name)>? roles = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("user_id", userId.ToString()),
            new("tenant_id", tenantId.ToString()),
            new("full_name", fullName),
            new("system_role", ((int)role).ToString()),
        };

        if (roles != null)
        {
            foreach (var (id, name) in roles)
            {
                claims.Add(new Claim("role_ids", id.ToString()));
                claims.Add(new Claim(ClaimTypes.Role, name));
            }
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(CustomWebApplicationFactory.TestJwtKey))
        {
            KeyId = "test-key",  // must match KeyId set in CustomWebApplicationFactory PostConfigure
        };
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: CustomWebApplicationFactory.TestJwtIssuer,
            audience: CustomWebApplicationFactory.TestJwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected string AdminToken() => GenerateToken(
        CustomWebApplicationFactory.AdminUserId,
        CustomWebApplicationFactory.AdminEmail,
        "Test Admin",
        Guid.Empty,
        SystemRole.SystemAdmin);

    protected string TenantAdminToken() => GenerateToken(
        CustomWebApplicationFactory.TenantAdminId,
        CustomWebApplicationFactory.TenantAdminEmail,
        "Test Tenant Admin",
        CustomWebApplicationFactory.TestTenantId,
        SystemRole.TenantAdmin);

    protected string TenantUserToken() => GenerateToken(
        CustomWebApplicationFactory.TenantUserId,
        CustomWebApplicationFactory.TenantUserEmail,
        "Test Tenant User",
        CustomWebApplicationFactory.TestTenantId,
        SystemRole.TenantUser);

    protected void UseAdminAuth()
    {
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", AdminToken());
    }

    protected void UseTenantAdminAuth()
    {
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TenantAdminToken());
        Client.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-Tenant-Id", CustomWebApplicationFactory.TestTenantId.ToString());
    }

    protected void UseTenantUserAuth()
    {
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TenantUserToken());
        Client.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-Tenant-Id", CustomWebApplicationFactory.TestTenantId.ToString());
    }

    protected void ClearAuth()
    {
        Client.DefaultRequestHeaders.Authorization = null;
        Client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    // ── Response helpers ───────────────────────────────────────────────────────

    protected async Task<T?> ReadEnvelopeDataAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var data))
            return default;
        return JsonSerializer.Deserialize<T>(data.GetRawText(), JsonOptions);
    }

    protected async Task<string?> ReadEnvelopeMessageAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("message", out var msg))
            return null;
        return msg.GetString();
    }

    protected static StringContent JsonContent<T>(T value) =>
        new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
}
