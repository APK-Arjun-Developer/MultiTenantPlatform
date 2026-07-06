using Application.Common;
using Application.DTOs.ActivityLogs;
using Application.DTOs.Common;
using Application.DTOs.Subscription;
using Application.DTOs.Tenant;
using Application.Exceptions;
using Application.Interfaces.Authentication;
using Application.Interfaces.Email;
using Application.Interfaces.Files;
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
using Microsoft.AspNetCore.Http;
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
    private readonly IFileService _fileService;
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
        IFileService fileService,
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
        _fileService = fileService;
        _appBaseUrl = configuration["AppBaseUrl"] ?? "https://app.example.com";
    }

    public async Task<PagedResponse<TenantResponse>> GetTenantsAsync(
        int page, int pageSize,
        string? search = null,
        string? sortBy = null,
        string? sortOrder = null,
        bool? isActive = null,
        CreatedVia? createdVia = null)
    {
        (page, pageSize) = Pagination.Normalize(page, pageSize);

        if (IsSystemAdmin())
        {
            var query = _context.Tenants
                .IgnoreQueryFilters()
                .Where(t => t.DeletedAt == null);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(t => t.Name.Contains(search));
            }

            if (isActive.HasValue)
            {
                query = query.Where(t => t.IsActive == isActive.Value);
            }

            if (createdVia.HasValue)
            {
                query = query.Where(t => t.CreatedVia == createdVia.Value);
            }

            var totalCount = await query.CountAsync();

            query = (sortBy?.ToLowerInvariant(), sortOrder?.ToLowerInvariant()) switch
            {
                ("name", "desc") => query.OrderByDescending(t => t.Name),
                _ => query.OrderBy(t => t.Name),
            };

            var tenants = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var tenantIds = tenants.Select(t => t.Id).ToList();

            var addressesByTenantId = await AddressHelper.GetTenantAddressesAsync(
                _context, tenantIds);

            var adminEmailsByTenantId = await _context.Users
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(u => tenantIds.Contains(u.TenantId)
                            && u.SystemRole == SystemRole.TenantAdmin
                            && u.DeletedAt == null)
                .GroupBy(u => u.TenantId)
                .Select(g => new
                {
                    TenantId = g.Key,
                    Email = g.OrderBy(u => u.CreatedAt).Select(u => u.Email).FirstOrDefault(),
                })
                .ToDictionaryAsync(x => x.TenantId, x => x.Email);

            var items = tenants
                .Select(t => MapToResponse(
                    t,
                    addressesByTenantId.GetValueOrDefault(t.Id),
                    adminEmailsByTenantId.GetValueOrDefault(t.Id)))
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

                var adminEmail = await _context.Users
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(u => u.TenantId == tenantId
                                && u.SystemRole == SystemRole.TenantAdmin
                                && u.DeletedAt == null)
                    .OrderBy(u => u.CreatedAt)
                    .Select(u => u.Email)
                    .FirstOrDefaultAsync();

                return MapToResponse(tenant, address, adminEmail);
            },
            TimeSpan.FromMinutes(_cacheOptions.TenantDetailMinutes));
    }

    public async Task<TenantResponse> UpdateAsync(UpdateTenantRequest request)
    {
        Guid tenantId = IsSystemAdmin() ? request.Id : RequireTenantId();

        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("Tenant id is required.");

        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.DeletedAt == null)
            ?? throw new NotFoundException("Tenant not found.");

        tenant.Name = request.Name;
        tenant.IsActive = request.IsActive;
        tenant.UpdatedAt = DateTime.UtcNow;

        await ApplyProfileFileUpdateAsync(tenant, request.ProfileFileId, request.ClearProfileImage);

        await AddressHelper.ApplyTenantAddressUpdateAsync(
            _context, tenant, request.Address, request.ClearAddress);

        await _context.SaveChangesAsync();

        _cache.InvalidateTenantCatalog();
        _cache.InvalidateTenant(tenant.Id);
        _cache.InvalidateTenantStatus(tenant.Id);

        await LogActivityAsync(ActivityActions.Tenants.Updated, $"Updated tenant '{tenant.Name}'.");

        var address = await AddressHelper.GetTenantAddressAsync(_context, tenant.Id);

        var adminEmail = await _context.Users
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(u => u.TenantId == tenant.Id
                        && u.SystemRole == SystemRole.TenantAdmin
                        && u.DeletedAt == null)
            .OrderBy(u => u.CreatedAt)
            .Select(u => u.Email)
            .FirstOrDefaultAsync();

        return MapToResponse(tenant, address, adminEmail);
    }

    public async Task<TenantResponse> UpdateCurrentTenantAddressAsync(UpdateCurrentTenantAddressRequest request)
    {
        var tenantId = RequireTenantId();

        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.DeletedAt == null)
            ?? throw new NotFoundException("Tenant not found.");

        await AddressHelper.ApplyTenantAddressUpdateAsync(
            _context, tenant, request.Address, request.ClearAddress);

        await _context.SaveChangesAsync();

        _cache.InvalidateTenant(tenant.Id);

        await LogActivityAsync(ActivityActions.Tenants.Updated, $"Updated address for tenant '{tenant.Name}'.");

        var address = await AddressHelper.GetTenantAddressAsync(_context, tenant.Id);

        return MapToResponse(tenant, address);
    }

    public async Task<TenantResponse> UpdateTenantSettingsAsync(UpdateTenantSettingsRequest request)
    {
        var tenantId = RequireTenantId();

        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.DeletedAt == null)
            ?? throw new NotFoundException("Tenant not found.");

        tenant.Name = request.Name;
        tenant.UpdatedAt = DateTime.UtcNow;

        await ApplyProfileFileUpdateAsync(tenant, request.ProfileFileId, request.ClearProfileImage);

        await AddressHelper.ApplyTenantAddressUpdateAsync(
            _context, tenant, request.Address, request.ClearAddress);

        await _context.SaveChangesAsync();

        _cache.InvalidateTenantCatalog();
        _cache.InvalidateTenant(tenant.Id);
        _cache.InvalidateTenantStatus(tenant.Id);

        await LogActivityAsync(ActivityActions.Tenants.Updated, $"Updated tenant settings for '{tenant.Name}'.");

        var address = await AddressHelper.GetTenantAddressAsync(_context, tenant.Id);

        var adminEmail = await _context.Users
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(u => u.TenantId == tenant.Id
                        && u.SystemRole == SystemRole.TenantAdmin
                        && u.DeletedAt == null)
            .OrderBy(u => u.CreatedAt)
            .Select(u => u.Email)
            .FirstOrDefaultAsync();

        return MapToResponse(tenant, address, adminEmail);
    }

    public async Task DeleteAsync(DeleteTenantRequest request)
    {
        if (!IsSystemAdmin())
        {
            throw new ForbiddenException("Only system admin can delete tenants.");
        }

        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.DeletedAt == null)
            ?? throw new NotFoundException("Tenant not found.");

        tenant.MarkDeleted();

        await _context.SaveChangesAsync();

        _cache.InvalidateTenantCatalog();
        _cache.InvalidateTenant(tenant.Id);
        _cache.InvalidateTenantStatus(tenant.Id);

        await LogActivityAsync(ActivityActions.Tenants.Deleted, $"Deleted tenant '{tenant.Name}'.");
    }

    public async Task<OnboardTenantResponse> OnboardTenantAsync(OnboardTenantRequest request)
    {
        var softDeletedTenant = (Domain.Entities.Tenant?)null;

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            Domain.Entities.Tenant tenant;

            if (softDeletedTenant != null)
            {
                // Restore the soft-deleted tenant record.
                softDeletedTenant.Name = request.Tenant.Name;
                softDeletedTenant.IsActive = true;
                softDeletedTenant.DeletedAt = null;
                softDeletedTenant.DeletedBy = null;
                softDeletedTenant.CreatedVia = CreatedVia.Direct;
                softDeletedTenant.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                tenant = softDeletedTenant;
            }
            else
            {
                tenant = new Domain.Entities.Tenant
                {
                    Id = Guid.NewGuid(),
                    Name = request.Tenant.Name,
                    IsActive = true,
                    CreatedVia = CreatedVia.Direct,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Tenants.Add(tenant);
                await _context.SaveChangesAsync();
            }

            // Create any caller-supplied custom roles (optional).
            // IdentityRoleService.CreateRoleAsync already handles soft-deleted roles.
            var createdRoles = new List<CreatedRoleSummary>();

            foreach (var roleDetails in request.Roles)
            {
                if (roleDetails.Name is RoleNames.SystemAdmin or RoleNames.TenantAdmin or RoleNames.TenantUser)
                    throw new ForbiddenException($"Cannot create built-in system role '{roleDetails.Name}' for a tenant.");

                var role = await _identityRoleService.CreateRoleAsync(
                    tenant.Id, roleDetails.Name, roleDetails.Description);

                await _identityRoleService.AssignPermissionsToRoleByIdsAsync(
                    role.Id, roleDetails.Permissions);

                createdRoles.Add(new CreatedRoleSummary { Id = role.Id, Name = role.Name! });
            }

            await _context.SaveChangesAsync();

            // Create or restore the admin user for this tenant.
            var normalised = request.User.Email.ToUpperInvariant();

            var activeAdminExists = await _userManager.Users
                .AnyAsync(u => u.NormalizedEmail == normalised && u.TenantId == tenant.Id);
            if (activeAdminExists)
                throw new ConflictException($"A user with email '{request.User.Email}' already exists in this tenant.");

            var softDeletedAdmin = await _context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.NormalizedEmail == normalised && u.TenantId == tenant.Id && u.DeletedAt != null);

            ApplicationUser adminUser;

            if (softDeletedAdmin != null)
            {
                softDeletedAdmin.FullName = request.User.FullName;
                softDeletedAdmin.SystemRole = SystemRole.TenantAdmin;
                softDeletedAdmin.IsActive = false;
                softDeletedAdmin.EmailConfirmed = false;
                softDeletedAdmin.DeletedAt = null;
                softDeletedAdmin.CreatedVia = CreatedVia.Direct;
                softDeletedAdmin.SecurityStamp = Guid.NewGuid().ToString();
                softDeletedAdmin.ConcurrencyStamp = Guid.NewGuid().ToString();
                softDeletedAdmin.PasswordHash = _userManager.PasswordHasher.HashPassword(softDeletedAdmin, $"Placeholder!{Guid.NewGuid():N}");
                softDeletedAdmin.UpdatedAt = DateTime.UtcNow;

                var updateResult = await _userManager.UpdateAsync(softDeletedAdmin);
                if (!updateResult.Succeeded)
                    throw new InvalidOperationException(string.Join(", ", updateResult.Errors.Select(e => e.Description)));

                adminUser = softDeletedAdmin;
            }
            else
            {
                adminUser = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    SystemRole = SystemRole.TenantAdmin,
                    FullName = request.User.FullName,
                    Email = request.User.Email,
                    UserName = request.User.Email,
                    NormalizedEmail = normalised,
                    NormalizedUserName = normalised,
                    EmailConfirmed = false,
                    IsActive = false,
                    CreatedVia = CreatedVia.Direct,
                    CreatedAt = DateTime.UtcNow
                };

                var placeholder = $"Placeholder!{Guid.NewGuid():N}";
                var createResult = await _userManager.CreateAsync(adminUser, placeholder);

                if (!createResult.Succeeded)
                    throw new InvalidOperationException(string.Join(", ", createResult.Errors.Select(e => e.Description)));
            }

            // Issue account-setup token so the admin can set their own password.
            var stale = await _context.AccountSetupTokens
                .IgnoreQueryFilters()
                .Where(t => t.UserId == adminUser.Id && t.UsedAt == null)
                .ToListAsync();
            _context.AccountSetupTokens.RemoveRange(stale);

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

            if (request.Tenant.Address != null)
            {
                await AddressHelper.ApplyTenantAddressUpdateAsync(
                    _context, tenant, request.Tenant.Address, false);
                await _context.SaveChangesAsync();
            }

            if (request.User.Address != null)
            {
                await AddressHelper.ApplyUserAddressUpdateAsync(
                    _context, adminUser, request.User.Address, false);
                await _context.SaveChangesAsync();
            }

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
            _cache.InvalidateUserStatus(adminUser.Id);

            await LogActivityAsync(
                ActivityActions.Tenants.Onboarded,
                $"Onboarded tenant '{tenant.Name}' with admin '{adminUser.Email}'.",
                tenantId: tenant.Id);

            return new OnboardTenantResponse
            {
                TenantId = tenant.Id,
                Name = tenant.Name,
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

    public async Task<TenantResponse> UploadTenantLogoAsync(IFormFile file)
    {
        var tenantId = RequireTenantId();

        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.DeletedAt == null)
            ?? throw new NotFoundException("Tenant not found.");

        var previousFileId = tenant.ProfileFileId;
        var uploaded = await _fileService.UploadAsync(file);

        tenant.ProfileFileId = uploaded.Id;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _cache.InvalidateTenant(tenant.Id);

        if (previousFileId.HasValue)
        {
            try { await _fileService.DeleteAsync(previousFileId.Value); }
            catch { /* old file already gone */ }
        }

        await LogActivityAsync(ActivityActions.Tenants.Updated, $"Updated logo for tenant '{tenant.Name}'.");

        var address = await AddressHelper.GetTenantAddressAsync(_context, tenant.Id);
        return MapToResponse(tenant, address);
    }

    public async Task<TenantResponse> RemoveTenantLogoAsync()
    {
        var tenantId = RequireTenantId();

        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.DeletedAt == null)
            ?? throw new NotFoundException("Tenant not found.");

        if (tenant.ProfileFileId.HasValue)
        {
            try { await _fileService.DeleteAsync(tenant.ProfileFileId.Value); }
            catch { /* file already gone */ }
            tenant.ProfileFileId = null;
            tenant.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _cache.InvalidateTenant(tenant.Id);
        }

        await LogActivityAsync(ActivityActions.Tenants.Updated, $"Removed logo for tenant '{tenant.Name}'.");

        var address = await AddressHelper.GetTenantAddressAsync(_context, tenant.Id);
        return MapToResponse(tenant, address);
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
        Address? address = null,
        string? adminEmail = null)
    {
        var features = PlanFeatures.Get(tenant.PlanType);
        return new TenantResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            IsActive = tenant.IsActive,
            CreatedVia = tenant.CreatedVia,
            ProfileFileId = tenant.ProfileFileId,
            ProfileUrl = BuildProfileUrl(tenant.ProfileFileId),
            Address = AddressFormatter.ToResponse(address),
            AdminEmail = adminEmail,
            PlanType = tenant.PlanType.ToString(),
            PlanName = PlanFeatures.GetName(tenant.PlanType),
            PlanFeatures = new PlanFeatureSummary
            {
                MaxUsers = features.MaxUsers,
                MaxStorageMb = features.MaxStorageMb,
                CanAccessReports = features.CanAccessReports,
                CanAccessAdvancedRoles = features.CanAccessAdvancedRoles,
            },
        };
    }

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
