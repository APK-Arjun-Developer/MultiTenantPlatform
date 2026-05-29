using Application.Common;
using Application.DTOs.ActivityLogs;
using Application.DTOs.Tenant;
using Application.Interfaces.ActivityLogs;
using Application.Interfaces.Tenant;
using Infrastructure.Identity;
using Infrastructure.Identity.Entities;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tenant;

public class TenantService : ITenantService
{
    private readonly ApplicationDbContext _context;

    private readonly UserManager<ApplicationUser> _userManager;

    private readonly ICurrentTenantService _currentTenantService;

    private readonly IActivityLogService _activityLogService;

    public TenantService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ICurrentTenantService currentTenantService,
        IActivityLogService activityLogService)
    {
        _context = context;
        _userManager = userManager;
        _currentTenantService = currentTenantService;
        _activityLogService = activityLogService;
    }

    public async Task<List<TenantResponse>> GetAllAsync()
    {
        if (IsSystemAdmin())
        {
            return await _context.Tenants
                .IgnoreQueryFilters()
                .Where(t => t.DeletedAt == null)
                .OrderBy(t => t.Name)
                .Select(x => new TenantResponse
                {
                    Id = x.Id,
                    Name = x.Name,
                    Slug = x.Slug,
                    IsActive = x.IsActive
                })
                .ToListAsync();
        }

        var tenantId = RequireTenantId();

        return await _context.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Id == tenantId && t.DeletedAt == null)
            .Select(x => new TenantResponse
            {
                Id = x.Id,
                Name = x.Name,
                Slug = x.Slug,
                IsActive = x.IsActive
            })
            .ToListAsync();
    }

    public async Task<TenantResponse> GetCurrentAsync()
    {
        if (IsSystemAdmin())
        {
            throw new InvalidOperationException(
                "Use GET /api/v1/tenants for system admin tenant listing.");
        }

        var tenantId = RequireTenantId();

        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.DeletedAt == null);

        if (tenant == null)
        {
            throw new InvalidOperationException("Tenant not found.");
        }

        return MapToResponse(tenant);
    }

    public async Task<TenantResponse> UpdateAsync(UpdateTenantRequest request)
    {
        Domain.Entities.Tenant tenant;

        if (IsSystemAdmin())
        {
            if (string.IsNullOrWhiteSpace(request.Slug))
            {
                throw new InvalidOperationException(
                    "Slug is required when updating a tenant as system admin.");
            }

            tenant = await _context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Slug == request.Slug && t.DeletedAt == null)
                ?? throw new InvalidOperationException("Tenant not found.");
        }
        else
        {
            var tenantId = RequireTenantId();

            tenant = await _context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == tenantId && t.DeletedAt == null)
                ?? throw new InvalidOperationException("Tenant not found.");
        }

        if (!string.IsNullOrWhiteSpace(request.NewSlug) &&
            request.NewSlug != tenant.Slug)
        {
            var slugTaken = await _context.Tenants
                .IgnoreQueryFilters()
                .AnyAsync(t =>
                    t.Slug == request.NewSlug
                    && t.Id != tenant.Id
                    && t.DeletedAt == null);

            if (slugTaken)
            {
                throw new InvalidOperationException("Tenant slug already exists.");
            }

            tenant.Slug = request.NewSlug;
        }

        tenant.Name = request.Name;
        tenant.IsActive = request.IsActive;
        tenant.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await LogCurrentUserActivityAsync(
            ActivityActions.Tenants.Updated,
            $"Updated tenant '{tenant.Slug}'.");

        return MapToResponse(tenant);
    }

    public async Task DeleteAsync(DeleteTenantRequest request)
    {
        if (!IsSystemAdmin())
        {
            throw new InvalidOperationException(
                "Only system admin can delete tenants.");
        }

        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Slug == request.Slug && t.DeletedAt == null);

        if (tenant == null)
        {
            throw new InvalidOperationException("Tenant not found.");
        }

        var hasUsers = await _userManager.Users
            .AnyAsync(u => u.TenantId == tenant.Id);

        if (hasUsers)
        {
            throw new InvalidOperationException(
                "Cannot delete a tenant that still has users.");
        }

        tenant.MarkDeleted();

        await _context.SaveChangesAsync();

        await LogCurrentUserActivityAsync(
            ActivityActions.Tenants.Deleted,
            $"Deleted tenant '{tenant.Slug}'.");
    }

    public async Task<OnboardTenantResponse> OnboardTenantAsync(
        OnboardTenantRequest request)
    {
        var slugExists = await _context.Tenants
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Slug == request.Tenant.Slug && t.DeletedAt == null);

        if (slugExists)
        {
            throw new InvalidOperationException("Tenant slug already exists.");
        }

        if (request.Roles.Count == 0)
        {
            throw new InvalidOperationException("At least one role is required.");
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            var tenant = new Domain.Entities.Tenant
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.Empty,
                Name = request.Tenant.Name,
                Slug = request.Tenant.Slug,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Tenants.Add(tenant);
            await _context.SaveChangesAsync();

            var createdRoles = new List<CreatedRoleSummary>();

            foreach (var roleDetails in request.Roles)
            {
                if (roleDetails.Name == Application.Common.RoleNames.SuperAdmin)
                {
                    throw new InvalidOperationException(
                        "Cannot create the SuperAdmin role for a tenant.");
                }

                var exists = await IdentityRoleHelper.RoleExistsAsync(
                    _context,
                    tenant.Id,
                    roleDetails.Name);

                if (exists)
                {
                    throw new InvalidOperationException(
                        $"Role '{roleDetails.Name}' already exists for this tenant.");
                }

                var role = await IdentityRoleHelper.CreateRoleAsync(
                    _context,
                    tenant.Id,
                    roleDetails.Name,
                    roleDetails.Description);

                await IdentityRoleHelper.AssignPermissionsToRoleByIdsAsync(
                    _context,
                    role.Id,
                    roleDetails.Permissions);

                createdRoles.Add(new CreatedRoleSummary
                {
                    Id = role.Id,
                    Name = role.Name!
                });
            }

            await _context.SaveChangesAsync();

            var adminUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                FullName = request.User.FullName,
                Email = request.User.Email,
                UserName = request.User.Email,
                NormalizedEmail = request.User.Email.ToUpperInvariant(),
                NormalizedUserName = request.User.Email.ToUpperInvariant(),
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(
                adminUser,
                request.User.Password);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join(", ",
                        createResult.Errors.Select(e => e.Description)));
            }

            await IdentityRoleHelper.AddUserToRoleAsync(
                _context,
                adminUser.Id,
                createdRoles[0].Id);

            await transaction.CommitAsync();

            await LogCurrentUserActivityAsync(
                ActivityActions.Tenants.Onboarded,
                $"Onboarded tenant '{tenant.Slug}' with admin '{adminUser.Email}'.",
                tenantId: tenant.Id);

            return new OnboardTenantResponse
            {
                TenantId = tenant.Id,
                Name = tenant.Name,
                Slug = tenant.Slug,
                AdminUserId = adminUser.Id,
                AdminEmail = adminUser.Email!,
                Roles = createdRoles
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static TenantResponse MapToResponse(Domain.Entities.Tenant tenant) =>
        new()
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            IsActive = tenant.IsActive
        };

    private bool IsSystemAdmin() =>
        (_currentTenantService.TenantId ?? Guid.Empty) == Guid.Empty;

    private async Task LogCurrentUserActivityAsync(
        string action,
        string description,
        Guid? tenantId = null)
    {
        var userId = _currentTenantService.UserId
            ?? throw new InvalidOperationException("User context is required.");

        await _activityLogService.LogAsync(new LogActivityRequest
        {
            UserId = userId,
            TenantId = tenantId,
            Action = action,
            Module = ActivityModules.Tenants,
            Description = description,
        });
    }

    private Guid RequireTenantId()
    {
        var tenantId = _currentTenantService.TenantId ?? Guid.Empty;

        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Tenant context is required. Ensure tenant_id is present in the JWT.");
        }

        return tenantId;
    }
}
