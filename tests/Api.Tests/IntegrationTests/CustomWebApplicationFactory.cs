using Application.Common;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Identity.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;

namespace Api.Tests.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestJwtKey = "super-secret-test-jwt-key-at-least-32-chars!!";
    public const string TestJwtIssuer = "TestIssuer";
    public const string TestJwtAudience = "TestAudience";

    public const string AdminEmail = "admin@test.com";
    public const string AdminPassword = "Admin123!";
    public const string TenantAdminEmail = "tenantadmin@test.com";
    public const string TenantUserEmail = "tenantuser@test.com";
    public const string TestInactiveUserEmail = "inactive-setup@test.com";
    public const string TestValidateTokenEmail = "inactive-validate@test.com";
    public const string UserPassword = "User123!";

    // Raw tokens for invitation / account-setup tests — each consumed by exactly one test.
    public const string TestTenantAdminInviteToken = "ta-invite-token-001";
    public const string TestTenantUserInviteToken = "tu-invite-token-002";
    public const string TestNewTenantInviteToken = "nt-invite-token-003";
    public const string TestAccountSetupToken = "account-setup-token-004";
    public const string TestRevokeInviteToken = "revoke-invite-token-005";
    public const string TestResendInviteToken = "resend-invite-token-006";
    public const string TestValidateInviteToken = "validate-invite-token-007";
    public const string TestResendSetupToken = "resend-setup-token-008";

    public static readonly Guid AdminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid TestTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid TenantAdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid TenantUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid TestRoleId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    public static readonly Guid TestInactiveUserId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    public static readonly Guid TestValidateTokenUserId = Guid.Parse("66666666-6666-6666-6666-666666666677");
    public static readonly Guid ProfileViewPermId = Guid.Parse("AA000001-0000-0000-0000-000000000000");
    public static readonly Guid FilesViewPermId = Guid.Parse("AA000002-0000-0000-0000-000000000000");
    public static readonly Guid TestTenantAdminInviteId = Guid.Parse("BB000001-0000-0000-0000-000000000000");
    public static readonly Guid TestTenantUserInviteId = Guid.Parse("BB000002-0000-0000-0000-000000000000");
    public static readonly Guid TestNewTenantInviteId = Guid.Parse("BB000003-0000-0000-0000-000000000000");
    public static readonly Guid TestRevokeInviteId = Guid.Parse("BB000004-0000-0000-0000-000000000000");
    public static readonly Guid TestResendInviteId = Guid.Parse("BB000005-0000-0000-0000-000000000000");
    public static readonly Guid TestValidateInviteId = Guid.Parse("BB000006-0000-0000-0000-000000000000");
    public static readonly Guid TestAccountSetupTokenId = Guid.Parse("CC000001-0000-0000-0000-000000000000");
    public static readonly Guid TestResendSetupTokenId = Guid.Parse("CC000002-0000-0000-0000-000000000000");

    // Dedicated EF Core internal provider for Sqlite — bypasses the app's IDatabaseProvider
    // conflict that arises when both SqlServer and Sqlite providers are in the same container.
    private static readonly IServiceProvider _efInternalProvider =
        new ServiceCollection()
            .AddEntityFrameworkSqlite()
            .BuildServiceProvider();

    // Shared SQLite in-memory connection — must stay open for the lifetime of the factory.
    private readonly SqliteConnection _sqliteConnection =
        new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "",
                ["Jwt:Key"] = TestJwtKey,
                ["Jwt:Issuer"] = TestJwtIssuer,
                ["Jwt:Audience"] = TestJwtAudience,
                ["Jwt:ExpiryMinutes"] = "60",
                ["Jwt:RefreshTokenExpiryDays"] = "7",
                ["AppBaseUrl"] = "https://localhost:5001",
                ["ApplyMigrationsOnStartup"] = "false",
                ["ApplySeedsOnStartup"] = "false",
                ["Features:RequireEmailVerification"] = "false",
                ["Seeding:AdminPassword"] = AdminPassword,
                ["FileStorage:BasePath"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
                ["Cache:PermissionCatalogMinutes"] = "5",
                ["Cache:UserProfileMinutes"] = "5",
                ["Cache:RolePermissionMinutes"] = "5",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove the cached DbContextOptions and the ApplicationDbContext registration.
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();

            // AddDbContext stores each options-action as IDbContextOptionsConfiguration<TContext>.
            // The app's AddPersistence already registered one with the SqlServer extension.
            // If we just call AddDbContext again with UseSqlite, both actions are applied and
            // EF Core sees two relational providers → "Multiple relational database provider
            // configurations found." Strip those descriptors before re-registering.
            var toRemove = services
                .Where(d => d.ServiceType.IsGenericType
                            && d.ServiceType.GetGenericArguments().Length == 1
                            && d.ServiceType.GetGenericArguments()[0] == typeof(ApplicationDbContext)
                            && d.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration"))
                .ToList();
            foreach (var d in toRemove)
                services.Remove(d);

            // Register the open SQLite connection so the same in-memory DB is shared
            // across scopes for the life of the factory.
            services.AddSingleton(_sqliteConnection);

            // Re-register DbContext using SQLite with a dedicated internal service
            // provider. UseInternalServiceProvider tells EF Core NOT to look at the
            // app's IDatabaseProvider registrations (bypassing the SqlServer vs Sqlite
            // conflict that arises when both providers are in the app's DI container).
            services.AddDbContext<ApplicationDbContext>((sp, options) =>
            {
                var conn = sp.GetRequiredService<SqliteConnection>();
                options.UseSqlite(conn)
                       .UseInternalServiceProvider(_efInternalProvider);
            });

            // IdentityModel 8.x requires kid matching. Update only the IssuerSigningKey
            // (with matching KeyId) on the existing TokenValidationParameters to avoid
            // resetting other settings that affect claim extraction in GetMeAsync.
            // Explicitly disable MapInboundClaims to ensure JWT claim names ("email",
            // "user_id", etc.) are preserved as-is in the ClaimsPrincipal.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters.IssuerSigningKey =
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtKey))
                    {
                        KeyId = "test-key",
                    };
                options.TokenValidationParameters.ValidIssuer = TestJwtIssuer;
                options.TokenValidationParameters.ValidAudience = TestJwtAudience;
            });

            // Replace the rate-limiter configuration with a very permissive policy so
            // tests that hit auth endpoints in quick succession don't get 429 responses.
            // Remove ALL IConfigureOptions<RateLimiterOptions> registered by the app
            // then re-add a single permissive policy.
            services.RemoveAll(typeof(IConfigureOptions<RateLimiterOptions>));
            services.RemoveAll(typeof(IPostConfigureOptions<RateLimiterOptions>));
            services.Configure<RateLimiterOptions>(options =>
            {
                options.RejectionStatusCode = 429;
                options.AddFixedWindowLimiter("auth", policy =>
                {
                    policy.PermitLimit = 10_000;
                    policy.Window = TimeSpan.FromMinutes(1);
                    policy.QueueLimit = 0;
                    policy.AutoReplenishment = true;
                });
            });
        });
    }

    public async Task InitializeAsync()
    {
        // Open the SQLite connection before any scope uses it
        _sqliteConnection.Open();

        using var scope = Services.CreateScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<ApplicationDbContext>();

        // Create schema via EnsureCreated (bypasses SQL Server migrations)
        await db.Database.EnsureCreatedAsync();

        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

        // Known IDs for permissions used in test payloads
        var knownPermIds = new Dictionary<string, Guid>
        {
            [PermissionNames.ProfileView] = ProfileViewPermId,
            [PermissionNames.FilesView] = FilesViewPermId,
        };

        // Seed permissions catalog
        foreach (var name in PermissionNames.All)
        {
            var permId = knownPermIds.TryGetValue(name, out var known) ? known : Guid.NewGuid();
            db.Permissions.Add(new Permission
            {
                Id = permId,
                Name = name,
                Module = name.Split('.')[0],
                Description = name,
                RequiredSystemRole = PermissionNames.Scopes.TryGetValue(name, out var role)
                    ? role
                    : SystemRole.SystemAdmin,
                CreatedAt = DateTime.UtcNow,
            });
        }

        // Seed test tenant
        db.Tenants.Add(new Domain.Entities.Tenant
        {
            Id = TestTenantId,
            Name = "Test Corp",
            IsActive = true,
            CreatedVia = CreatedVia.Direct,
            CreatedAt = DateTime.UtcNow,
        });

        // Seed a reusable test role with known ID (used in user/invite payloads)
        db.Roles.Add(new ApplicationRole
        {
            Id = TestRoleId,
            TenantId = TestTenantId,
            Name = "TestRole",
            NormalizedName = "TESTROLE",
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            Scope = Domain.Enums.RoleScope.Tenant,
        });

        // Assign ProfileView permission to TestRole
        db.Set<Domain.Entities.RolePermission>().Add(new Domain.Entities.RolePermission
        {
            RoleId = TestRoleId,
            PermissionId = ProfileViewPermId,
        });

        await db.SaveChangesAsync();

        // Seed system admin user
        await CreateUserAsync(userManager, new ApplicationUser
        {
            Id = AdminUserId,
            TenantId = Guid.Empty,
            SystemRole = SystemRole.SystemAdmin,
            FullName = "Test Admin",
            UserName = AdminEmail,
            Email = AdminEmail,
            EmailConfirmed = true,
            IsActive = true,
            CreatedVia = CreatedVia.Direct,
            CreatedAt = DateTime.UtcNow,
        }, AdminPassword);

        // Seed tenant admin
        await CreateUserAsync(userManager, new ApplicationUser
        {
            Id = TenantAdminId,
            TenantId = TestTenantId,
            SystemRole = SystemRole.TenantAdmin,
            FullName = "Test Tenant Admin",
            UserName = TenantAdminEmail,
            Email = TenantAdminEmail,
            EmailConfirmed = true,
            IsActive = true,
            CreatedVia = CreatedVia.Direct,
            CreatedAt = DateTime.UtcNow,
        }, UserPassword);

        // Seed tenant user
        await CreateUserAsync(userManager, new ApplicationUser
        {
            Id = TenantUserId,
            TenantId = TestTenantId,
            SystemRole = SystemRole.TenantUser,
            FullName = "Test Tenant User",
            UserName = TenantUserEmail,
            Email = TenantUserEmail,
            EmailConfirmed = true,
            IsActive = true,
            CreatedVia = CreatedVia.Direct,
            CreatedAt = DateTime.UtcNow,
        }, UserPassword);

        // Seed an inactive user whose account-setup token is known — used by SetPassword tests.
        await CreateUserAsync(userManager, new ApplicationUser
        {
            Id = TestInactiveUserId,
            TenantId = TestTenantId,
            SystemRole = SystemRole.TenantUser,
            FullName = "Inactive Setup User",
            UserName = TestInactiveUserEmail,
            Email = TestInactiveUserEmail,
            EmailConfirmed = false,
            IsActive = false,
            CreatedVia = CreatedVia.Direct,
            CreatedAt = DateTime.UtcNow,
        }, UserPassword);

        // Separate inactive user exclusively for ValidateSetupToken test —
        // never activated by SetPassword, so ValidateTokenAsync always returns isValid=true.
        await CreateUserAsync(userManager, new ApplicationUser
        {
            Id = TestValidateTokenUserId,
            TenantId = TestTenantId,
            SystemRole = SystemRole.TenantUser,
            FullName = "Validate Token User",
            UserName = TestValidateTokenEmail,
            Email = TestValidateTokenEmail,
            EmailConfirmed = false,
            IsActive = false,
            CreatedVia = CreatedVia.Direct,
            CreatedAt = DateTime.UtcNow,
        }, UserPassword);

        var now = DateTime.UtcNow;

        // Account-setup tokens for the inactive user
        db.AccountSetupTokens.Add(new AccountSetupToken
        {
            Id = TestAccountSetupTokenId,
            UserId = TestInactiveUserId,
            TenantId = TestTenantId,
            TokenHash = HashToken(TestAccountSetupToken),
            ExpiresAt = now.AddDays(7),
            CreatedAt = now,
        });
        // Second token — belongs to a SEPARATE inactive user so SetPassword on TestInactiveUserId
        // never makes this token's user appear active, keeping the validate test independent.
        db.AccountSetupTokens.Add(new AccountSetupToken
        {
            Id = TestResendSetupTokenId,
            UserId = TestValidateTokenUserId,
            TenantId = TestTenantId,
            TokenHash = HashToken(TestResendSetupToken),
            ExpiresAt = now.AddDays(7),
            CreatedAt = now,
        });

        // Invitation records for full-flow tests — each token consumed by exactly one test
        var invitations = new[]
        {
            new Invitation
            {
                Id = TestTenantAdminInviteId,
                TenantId = TestTenantId,
                Email = "ta-invite-accept@test.com",
                InvitationType = InvitationType.TenantAdmin,
                RoleIdsJson = "[]",
                TokenHash = HashToken(TestTenantAdminInviteToken),
                ExpiresAt = now.AddDays(7),
                InvitedByUserId = AdminUserId,
                CreatedAt = now,
            },
            new Invitation
            {
                Id = TestTenantUserInviteId,
                TenantId = TestTenantId,
                Email = "tu-invite-accept@test.com",
                InvitationType = InvitationType.TenantUser,
                RoleIdsJson = $"[\"{TestRoleId}\"]",
                TokenHash = HashToken(TestTenantUserInviteToken),
                ExpiresAt = now.AddDays(7),
                InvitedByUserId = TenantAdminId,
                CreatedAt = now,
            },
            new Invitation
            {
                Id = TestNewTenantInviteId,
                TenantId = Guid.Empty,
                Email = "nt-invite-accept@test.com",
                InvitationType = InvitationType.NewTenant,
                RoleIdsJson = "[]",
                TokenHash = HashToken(TestNewTenantInviteToken),
                ExpiresAt = now.AddDays(7),
                InvitedByUserId = AdminUserId,
                CreatedAt = now,
            },
            new Invitation
            {
                Id = TestRevokeInviteId,
                TenantId = TestTenantId,
                Email = "revoke-invite@test.com",
                InvitationType = InvitationType.TenantUser,
                RoleIdsJson = "[]",
                TokenHash = HashToken(TestRevokeInviteToken),
                ExpiresAt = now.AddDays(7),
                InvitedByUserId = TenantAdminId,
                CreatedAt = now,
            },
            new Invitation
            {
                Id = TestResendInviteId,
                TenantId = TestTenantId,
                Email = "resend-invite@test.com",
                InvitationType = InvitationType.TenantAdmin,
                RoleIdsJson = "[]",
                TokenHash = HashToken(TestResendInviteToken),
                ExpiresAt = now.AddDays(7),
                InvitedByUserId = AdminUserId,
                CreatedAt = now,
            },
            new Invitation
            {
                Id = TestValidateInviteId,
                TenantId = TestTenantId,
                Email = "validate-invite@test.com",
                InvitationType = InvitationType.TenantUser,
                RoleIdsJson = "[]",
                TokenHash = HashToken(TestValidateInviteToken),
                ExpiresAt = now.AddDays(7),
                InvitedByUserId = TenantAdminId,
                CreatedAt = now,
            },
        };

        db.Invitations.AddRange(invitations);
        await db.SaveChangesAsync();
    }

    private static string HashToken(string rawToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public new async Task DisposeAsync()
    {
        await _sqliteConnection.DisposeAsync();
        await base.DisposeAsync();
    }

    private static async Task CreateUserAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        string password)
    {
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create test user {user.Email}: " +
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
