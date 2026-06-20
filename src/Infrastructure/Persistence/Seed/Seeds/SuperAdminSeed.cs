using Application.Common;
using Domain.Enums;
using Infrastructure.Identity;
using Infrastructure.Identity.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Persistence.Seed.Seeds;

public sealed class SuperAdminSeed : IDataSeed
{
    public const string Id = "20260603000003_SuperAdmin";

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IIdentityRoleService _identityRoleService;
    private readonly IConfiguration _configuration;

    public SuperAdminSeed(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IIdentityRoleService identityRoleService,
        IConfiguration configuration)
    {
        _context = context;
        _userManager = userManager;
        _identityRoleService = identityRoleService;
        _configuration = configuration;
    }

    public string SeedId => Id;

    public string Description => "SuperAdmin role, permissions, and admin@system.com user.";

    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        var adminPassword = _configuration["Seeding:AdminPassword"];

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            throw new InvalidOperationException(
                "Seeding:AdminPassword must be configured before applying SuperAdmin seed. " +
                "Set it via environment variable, user secrets, or a secrets manager.");
        }

        const string adminRole = RoleNames.SuperAdmin;
        var platformTenantId = Guid.Empty;

        var superAdminRole = await _identityRoleService.FindRoleByNameAsync(
            platformTenantId,
            adminRole);

        if (superAdminRole == null)
        {
            superAdminRole = await _identityRoleService.CreateRoleAsync(
                platformTenantId,
                adminRole,
                "System Super Administrator");
        }
        else if (superAdminRole.Scope != RoleScope.System)
        {
            superAdminRole.Scope = RoleScope.System;
        }

        await _identityRoleService.AssignPermissionsToRoleAsync(
            superAdminRole.Id,
            PermissionNames.All);

        await _context.SaveChangesAsync(cancellationToken);

        const string adminEmail = "admin@system.com";

        var existingUser = await _userManager.Users
            .FirstOrDefaultAsync(u =>
                u.NormalizedEmail == adminEmail.ToUpperInvariant() &&
                u.TenantId == platformTenantId,
                cancellationToken);

        if (existingUser != null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = platformTenantId,
            SystemRole = SystemRole.SystemAdmin,
            FullName = "System Administrator",
            UserName = adminEmail,
            Email = adminEmail,
            NormalizedEmail = adminEmail.ToUpperInvariant(),
            NormalizedUserName = adminEmail.ToUpperInvariant(),
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        var result = await _userManager.CreateAsync(user, adminPassword);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Failed to create system admin user: " +
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        await _identityRoleService.AddUserToRoleAsync(user.Id, superAdminRole.Id);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
