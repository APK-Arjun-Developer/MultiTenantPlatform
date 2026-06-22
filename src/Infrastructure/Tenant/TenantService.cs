using Application.Common;
using Application.DTOs.ActivityLogs;
using Application.DTOs.Common;
using Application.DTOs.Tenant;
using Application.Exceptions;
using Application.Interfaces.Authentication;
using Application.Interfaces.Email;
using Application.Options;
using Domain.Entities;
using Domain.Enums;
using Application.Interfaces.ActivityLogs;
using Application.Interfaces.Caching;
using Application.Interfaces.Tenant;
using Infrastructure.Caching;
using Infrastructure.Common;
using Infrastructure.Identity;
using Infrastructure.Identity.Entities;
using Infrastructure.Onboarding;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.Tenant;

public class TenantService : TenantScopedService, ITenantService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IActivityLogService _activityLogService;
    private readonly IAppCache _cache;
    private readonly CacheOptions _cacheOptions;
    private readonly IIdentityRoleService _identityRoleService;
    private readonly IEmailService _emailService;
    private readonly string _appBaseUrl;

    private static readonly TimeSpan SetupTokenLifetime = TimeSpan.FromDays(7);

    public TenantService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ICurrentTenantService currentTenantService,
        IActivityLogService activityLogService,
        IAppCache cache,
        IOptions<CacheOptions> cacheOptions,
        IIdentityRoleService identityRoleService,
        IEmailService emailService,
        IConfiguration configuration)
        : base(currentTenantService)
    {
        _context = context;
        _userManager = userManager;
        _activityLogService = activityLogService;
        _cache = cache;
        _cacheOptions = cacheOptions.Value;
        _identityRoleService = identityRoleService;
        _emailService = emailService;
        _appBaseUrl = configuration["AppBaseUrl"] ?? "https://app.example.com";
    }

    public async Task<PagedResponse<TenantResponse>> GetTenantsAsync(
        int page, int pageSize,
        string? search = null,
        string? sortBy = null,
        string? sortOrder = null)
    {
        (page, pageSize) = Pagination.Normalize(page, pageSize);

        if (IsSystemAdmin())
        {
            var query = _context.Tenants
                .IgnoreQueryFilters()
                .Where(t => t.DeletedAt == null);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(t =>
                    t.Name.Contains(search) || t.Slug.Contains(search));
            }

            var totalCount = await query.CountAsync();

            query = (sortBy?.ToLowerInvariant(), sortOrder?.ToLowerInvariant()) switch
            {
                ("slug", "desc") => query.OrderByDescending(t => t.Slug),
                ("slug", _)      => query.OrderBy(t => t.Slug),
                ("name", "desc") => query.OrderByDescending(t => t.Name),
                _                => query.OrderBy(t => t.Name),
            };

            var tenants = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var tenantIds = tenants.Select(t => t.Id).ToList();

            var addressesByTenantId = await AddressHelper.GetTenantAddressesAsync(
                _context, tenantIds);

            var items = tenants
                .Select(t => MapToResponse(t, addressesByTenantId.GetValueOrDefault(t.Id)))
                .ToList();

            return new PagedResponse<TenantResponse>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
            };
        }

        var tenantId = RequireTenantId();
        var current = await GetTenantByIdCachedAsync(tenantId);

        return new PagedResponse<TenantResponse>
        {
            Items = [current],
            Page = 1,
            PageSize = pageSize,
            TotalCount = 1,
        };
    }

    public async Task<TenantResponse> GetByIdAsync(Guid id)
    {
        if (!IsSystemAdmin())
        {
            var tenantId = RequireTenantId();

            if (tenantId != id)
            {
                throw new ForbiddenException("You can only access your own tenant.");
            }
        }

        return await GetTenantByIdCachedAsync(id);
    }

    public async Task<TenantResponse> GetCurrentAsync()
    {
        if (IsSystemAdmin())
        {
            throw new InvalidOperationException(
                "Use GET /api/v1/tenants for system admin tenant listing.");
        }

        return await GetTenantByIdCachedAsync(RequireTenantId());
    }

    private async Task<TenantResponse> GetTenantByIdCachedAsync(Guid tenantId)
    {
        return await _cache.GetOrCreateAsync(
            CacheKeys.TenantDetail(tenantId),
            async _ =>
            {
                var tenant = await _context.Tenants
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(t => t.Id == tenantId && t.DeletedAt == null)
                    ?? throw new NotFoundException("Tenant not found.");

                var address = await AddressHelper.GetTenantAddressAsync(_context, tenantId);

                return MapToResponse(tenant, address);
            },
            TimeSpan.FromMinutes(_cacheOptions.TenantDetailMinutes));
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
                ?? throw new NotFoundException("Tenant not found.");
        }
        else
        {
            tenant = await _context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == RequireTenantId() && t.DeletedAt == null)
                ?? throw new NotFoundException("Tenant not found.");
        }

        if (!string.IsNullOrWhiteSpace(request.NewSlug) && request.NewSlug != tenant.Slug)
        {
            var slugTaken = await _context.Tenants
                .IgnoreQueryFilters()
                .AnyAsync(t => t.Slug == request.NewSlug && t.Id != tenant.Id && t.DeletedAt == null);

            if (slugTaken)
            {
                throw new ConflictException("Tenant slug already exists.");
            }

            tenant.Slug = request.NewSlug;
        }

        tenant.Name = request.Name;
        tenant.IsActive = request.IsActive;
        tenant.UpdatedAt = DateTime.UtcNow;

        await ApplyProfileFileUpdateAsync(tenant, request.ProfileFileId, request.ClearProfileImage);

        await AddressHelper.ApplyTenantAddressUpdateAsync(
            _context, tenant, request.Address, request.ClearAddress);

        await _context.SaveChangesAsync();

        _cache.InvalidateTenantCatalog();
        _cache.InvalidateTenant(tenant.Id);

        await LogActivityAsync(ActivityActions.Tenants.Updated, $"Updated tenant '{tenant.Slug}'.");

        var address = await AddressHelper.GetTenantAddressAsync(_context, tenant.Id);

        return MapToResponse(tenant, address);
    }

    public async Task DeleteAsync(DeleteTenantRequest request)
    {
        if (!IsSystemAdmin())
        {
            throw new ForbiddenException("Only system admin can delete tenants.");
        }

        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Slug == request.Slug && t.DeletedAt == null)
            ?? throw new NotFoundException("Tenant not found.");

        var hasUsers = await _userManager.Users.AnyAsync(u => u.TenantId == tenant.Id);

        if (hasUsers)
        {
            throw new ConflictException("Cannot delete a tenant that still has users.");
        }

        tenant.MarkDeleted();

        await _context.SaveChangesAsync();

        _cache.InvalidateTenantCatalog();
        _cache.InvalidateTenant(tenant.Id);

        await LogActivityAsync(ActivityActions.Tenants.Deleted, $"Deleted tenant '{tenant.Slug}'.");
    }

    public async Task<OnboardTenantResponse> OnboardTenantAsync(OnboardTenantRequest request)
    {
        var slugExists = await _context.Tenants
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Slug == request.Tenant.Slug && t.DeletedAt == null);

        if (slugExists)
        {
            throw new ConflictException("Tenant slug already exists.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var tenant = new Domain.Entities.Tenant
            {
                Id = Guid.NewGuid(),
                Name = request.Tenant.Name,
                Slug = request.Tenant.Slug,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Tenants.Add(tenant);
            await _context.SaveChangesAsync();

            // Create any caller-supplied custom roles (optional).
            var createdRoles = new List<CreatedRoleSummary>();

            foreach (var roleDetails in request.Roles)
            {
                if (roleDetails.Name is RoleNames.SystemAdmin or RoleNames.TenantAdmin or RoleNames.TenantUser)
                {
                    throw new ForbiddenException($"Cannot create built-in system role '{roleDetails.Name}' for a tenant.");
                }

                if (await _identityRoleService.RoleExistsAsync(tenant.Id, roleDetails.Name))
                {
                    throw new ConflictException(
                        $"Role '{roleDetails.Name}' already exists for this tenant.");
                }

                var role = await _identityRoleService.CreateRoleAsync(
                    tenant.Id, roleDetails.Name, roleDetails.Description);

                await _identityRoleService.AssignPermissionsToRoleByIdsAsync(
                    role.Id, roleDetails.Permissions);

                createdRoles.Add(new CreatedRoleSummary { Id = role.Id, Name = role.Name! });
            }

            await _context.SaveChangesAsync();

            // TenantAdmin permissions come from SystemRole — no role table entry or assignment needed.
            // Created inactive; admin sets their own password via the account-setup email link.
            var adminUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                SystemRole = SystemRole.TenantAdmin,
                FullName = request.User.FullName,
                Email = request.User.Email,
                UserName = request.User.Email,
                NormalizedEmail = request.User.Email.ToUpperInvariant(),
                NormalizedUserName = request.User.Email.ToUpperInvariant(),
                EmailConfirmed = false,
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            };

            var placeholder = $"Placeholder!{Guid.NewGuid():N}";
            var createResult = await _userManager.CreateAsync(adminUser, placeholder);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
            }

            // Issue account-setup token so the admin can set their own password.
            var (rawToken, tokenHash) = TokenHelper.Generate();
            var now = DateTime.UtcNow;

            _context.AccountSetupTokens.Add(new AccountSetupToken
            {
                Id = Guid.NewGuid(),
                UserId = adminUser.Id,
                TenantId = tenant.Id,
                TokenHash = tokenHash,
                ExpiresAt = now.Add(SetupTokenLifetime),
                CreatedAt = now,
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var setupUrl = $"{_appBaseUrl}/account-setup?token={rawToken}";

            try
            {
                await _emailService.SendAccountSetupEmailAsync(
                    adminUser.Email!, adminUser.FullName, setupUrl);
            }
            catch
            {
                // Non-fatal — admin can be resent via POST /tenant-admins/{id}/resend
            }

            _cache.InvalidateTenantCatalog();
            _cache.InvalidateTenant(tenant.Id);

            await LogActivityAsync(
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

    private async Task ApplyProfileFileUpdateAsync(
        Domain.Entities.Tenant tenant,
        Guid? profileFileId,
        bool clearProfileImage)
    {
        if (clearProfileImage)
        {
            tenant.ProfileFileId = null;
            return;
        }

        if (!profileFileId.HasValue)
        {
            return;
        }

        var fileExists = await _context.Files
            .AsNoTracking()
            .AnyAsync(f => f.Id == profileFileId.Value && f.TenantId == tenant.Id);

        if (!fileExists)
        {
            throw new NotFoundException("Profile file not found or does not belong to the tenant.");
        }

        tenant.ProfileFileId = profileFileId.Value;
    }

    private static string? BuildProfileUrl(Guid? profileFileId) =>
        profileFileId.HasValue ? $"/api/v1/files/{profileFileId.Value}/download" : null;

    private static TenantResponse MapToResponse(
        Domain.Entities.Tenant tenant,
        Address? address = null) =>
        new()
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            IsActive = tenant.IsActive,
            ProfileFileId = tenant.ProfileFileId,
            ProfileUrl = BuildProfileUrl(tenant.ProfileFileId),
            Address = AddressFormatter.ToResponse(address),
        };

    private async Task LogActivityAsync(string action, string description, Guid? tenantId = null)
    {
        await _activityLogService.LogAsync(new LogActivityRequest
        {
            UserId = RequireUserId(),
            TenantId = tenantId,
            Action = action,
            Module = ActivityModules.Tenants,
            Description = description,
        });
    }
}
