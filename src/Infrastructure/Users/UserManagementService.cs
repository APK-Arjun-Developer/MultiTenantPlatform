using Application.Common;
using Application.DTOs.ActivityLogs;
using Application.DTOs.Common;
using Application.DTOs.Users;
using Application.Exceptions;
using Application.Interfaces.ActivityLogs;
using Application.Interfaces.Caching;
using Application.Interfaces.Tenant;
using Application.Interfaces.Users;
using Infrastructure.Common;
using Infrastructure.Identity;
using Infrastructure.Identity.Entities;
using Domain.Entities;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Users;

public class UserManagementService : TenantScopedService, IUserManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IActivityLogService _activityLogService;
    private readonly IAppCache _cache;
    private readonly IIdentityRoleService _identityRoleService;

    public UserManagementService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ICurrentTenantService currentTenantService,
        IActivityLogService activityLogService,
        IAppCache cache,
        IIdentityRoleService identityRoleService)
        : base(currentTenantService)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _activityLogService = activityLogService;
        _cache = cache;
        _identityRoleService = identityRoleService;
    }

    public async Task<UserResponse> CreateUserAsync(CreateUserRequest request)
    {
        var tenantId = CurrentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Tenant users cannot be created in the platform scope.");
        }

        if (request.RoleName == RoleNames.SuperAdmin)
        {
            throw new ForbiddenException("Cannot assign the SuperAdmin role.");
        }

        var role = await _identityRoleService.FindRoleByNameAsync(tenantId, request.RoleName)
            ?? throw new NotFoundException($"Role '{request.RoleName}' was not found for this tenant.");

        var emailExists = await _userManager.Users
            .AnyAsync(u =>
                u.NormalizedEmail == request.Email.ToUpperInvariant() &&
                u.TenantId == tenantId);

        if (emailExists)
        {
            throw new ConflictException("User email already exists.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FullName = request.FullName,
            Email = request.Email,
            UserName = request.Email,
            NormalizedEmail = request.Email.ToUpperInvariant(),
            NormalizedUserName = request.Email.ToUpperInvariant(),
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        await _identityRoleService.AddUserToRoleAsync(user.Id, role.Id);

        await LogActivityAsync(
            ActivityActions.Users.Created,
            $"Created user '{user.Email}' with role '{request.RoleName}'.");

        _cache.InvalidateTenantDashboard(tenantId);

        return await MapToUserResponseAsync(user, includeTenantDetails: false);
    }

    public async Task<PagedResponse<UserResponse>> GetUsersAsync(int page, int pageSize)
    {
        var currentUserId = RequireUserId();

        (page, pageSize) = Pagination.Normalize(page, pageSize);

        var isAdmin = IsSystemAdmin();

        IQueryable<ApplicationUser> query = _userManager.Users
            .Where(u => u.Id != currentUserId);

        if (isAdmin)
        {
            query = query.Where(u => u.TenantId != Guid.Empty);
        }
        else
        {
            var tenantId = RequireTenantId();
            query = query.Where(u => u.TenantId == tenantId);
        }

        var totalCount = await query.CountAsync();

        var users = await query
            .OrderBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Batch-load roles for all users in one query — eliminates N+1.
        var userIds = users.Select(u => u.Id).ToList();
        var tenantIds = users.Select(u => u.TenantId).Distinct().ToList();

        var userRoles = await _context.Set<IdentityUserRole<Guid>>()
            .AsNoTracking()
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(_context.Roles.AsNoTracking(),
                ur => ur.RoleId,
                r => r.Id,
                (ur, r) => new { ur.UserId, r.Name })
            .ToListAsync();

        var rolesByUser = userRoles
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name!).ToList());

        Dictionary<Guid, Domain.Entities.Tenant> tenantsById = [];
        Dictionary<Guid, Address> tenantAddressesById = [];

        if (isAdmin && users.Count > 0)
        {
            tenantsById = await _context.Tenants
                .IgnoreQueryFilters()
                .Where(t => tenantIds.Contains(t.Id) && t.DeletedAt == null)
                .ToDictionaryAsync(t => t.Id);

            tenantAddressesById = await AddressHelper.GetTenantAddressesAsync(_context, tenantIds);
        }

        var userAddresses = await AddressHelper.GetUserAddressesAsync(_context, userIds, isAdmin);

        var responses = new List<UserResponse>();

        foreach (var user in users)
        {
            UserTenantDetails? tenantDetails = null;

            if (isAdmin && tenantsById.TryGetValue(user.TenantId, out var tenant))
            {
                tenantAddressesById.TryGetValue(tenant.Id, out var tenantAddress);
                tenantDetails = MapTenantDetails(tenant, tenantAddress);
            }

            userAddresses.TryGetValue(user.Id, out var userAddress);
            var roles = rolesByUser.GetValueOrDefault(user.Id, []);

            responses.Add(MapToUserResponse(user, roles, isAdmin, tenantDetails, userAddress));
        }

        return new PagedResponse<UserResponse>
        {
            Items = responses,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<UserResponse> GetCurrentUserAsync()
    {
        var user = await GetCurrentUserEntityAsync();

        UserTenantDetails? tenantDetails = null;

        if (user.TenantId != Guid.Empty)
        {
            var tenant = await _context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == user.TenantId && t.DeletedAt == null);

            if (tenant != null)
            {
                var tenantAddress = await AddressHelper.GetTenantAddressAsync(_context, tenant.Id);
                tenantDetails = MapTenantDetails(tenant, tenantAddress);
            }
        }

        var address = await AddressHelper.GetUserAddressAsync(_context, user.Id, IsSystemAdmin());

        return await MapToUserResponseAsync(
            user,
            includeTenantDetails: tenantDetails != null,
            tenantDetails,
            address);
    }

    public async Task<UserResponse> UpdateUserAsync(UpdateUserRequest request)
    {
        var user = await FindManagedUserAsync(request.Email)
            ?? throw new NotFoundException("User not found.");

        user.FullName = request.FullName;
        user.UpdatedAt = DateTime.UtcNow;

        await ApplyProfileFileUpdateAsync(user, request.ProfileFileId, request.ClearProfileImage);

        await AddressHelper.ApplyUserAddressUpdateAsync(
            _context, user, request.Address, request.ClearAddress);

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var passwordResult = await _userManager.ResetPasswordAsync(user, token, request.Password);

            if (!passwordResult.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join(", ", passwordResult.Errors.Select(e => e.Description)));
            }
        }

        if (!string.IsNullOrWhiteSpace(request.RoleName))
        {
            if (request.RoleName == RoleNames.SuperAdmin)
            {
                throw new ForbiddenException("Cannot assign the SuperAdmin role.");
            }

            var role = await _identityRoleService.FindRoleByNameAsync(user.TenantId, request.RoleName)
                ?? throw new NotFoundException($"Role '{request.RoleName}' was not found for this tenant.");

            var existingAssignments = await _context.Set<IdentityUserRole<Guid>>()
                .Where(ur => ur.UserId == user.Id)
                .ToListAsync();

            _context.Set<IdentityUserRole<Guid>>().RemoveRange(existingAssignments);

            await _identityRoleService.AddUserToRoleAsync(user.Id, role.Id);
        }

        // UserManager.UpdateAsync internally calls SaveChangesAsync — no second call needed.
        await _userManager.UpdateAsync(user);

        await LogActivityAsync(ActivityActions.Users.Updated, $"Updated user '{user.Email}'.");

        _cache.InvalidateTenantDashboard(user.TenantId);

        var address = await AddressHelper.GetUserAddressAsync(_context, user.Id, IsSystemAdmin());

        return await MapToUserResponseAsync(user, includeTenantDetails: IsSystemAdmin(), address: address);
    }

    public async Task<UserResponse> UpdateCurrentUserAsync(UpdateCurrentUserRequest request)
    {
        var user = await GetCurrentUserEntityAsync();

        user.FullName = request.FullName;
        user.UpdatedAt = DateTime.UtcNow;

        await ApplyProfileFileUpdateAsync(user, request.ProfileFileId, request.ClearProfileImage);

        await AddressHelper.ApplyUserAddressUpdateAsync(
            _context, user, request.Address, request.ClearAddress);

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var passwordResult = await _userManager.ResetPasswordAsync(user, token, request.Password);

            if (!passwordResult.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join(", ", passwordResult.Errors.Select(e => e.Description)));
            }
        }

        await _userManager.UpdateAsync(user);

        await LogActivityAsync(ActivityActions.Users.Updated, "Updated own profile.");

        return await GetCurrentUserAsync();
    }

    public async Task DeleteUserAsync(DeleteUserRequest request)
    {
        var currentUserId = RequireUserId();

        var user = await FindManagedUserAsync(request.Email)
            ?? throw new NotFoundException("User not found.");

        if (user.Id == currentUserId)
        {
            throw new ConflictException("You cannot delete your own account.");
        }

        user.DeletedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        await LogActivityAsync(ActivityActions.Users.Deleted, $"Deleted user '{user.Email}'.");

        _cache.InvalidateTenantDashboard(user.TenantId);
    }

    private async Task LogActivityAsync(string action, string description)
    {
        await _activityLogService.LogAsync(new LogActivityRequest
        {
            UserId = RequireUserId(),
            Action = action,
            Module = ActivityModules.Users,
            Description = description,
        });
    }

    private async Task<ApplicationUser> GetCurrentUserEntityAsync()
    {
        return await _userManager.FindByIdAsync(RequireUserId().ToString())
            ?? throw new NotFoundException("User not found.");
    }

    private async Task<ApplicationUser?> FindManagedUserAsync(string email)
    {
        var normalizedEmail = email.ToUpperInvariant();

        if (IsSystemAdmin())
        {
            return await _userManager.Users
                .FirstOrDefaultAsync(u =>
                    u.NormalizedEmail == normalizedEmail &&
                    u.TenantId != Guid.Empty);
        }

        var tenantId = RequireTenantId();

        return await _userManager.Users
            .FirstOrDefaultAsync(u =>
                u.NormalizedEmail == normalizedEmail &&
                u.TenantId == tenantId);
    }

    private async Task ApplyProfileFileUpdateAsync(
        ApplicationUser user,
        Guid? profileFileId,
        bool clearProfileImage)
    {
        if (clearProfileImage)
        {
            user.ProfileFileId = null;
            return;
        }

        if (!profileFileId.HasValue)
        {
            return;
        }

        var fileExists = await _context.Files
            .AsNoTracking()
            .AnyAsync(f => f.Id == profileFileId.Value && f.TenantId == user.TenantId);

        if (!fileExists)
        {
            throw new NotFoundException(
                "Profile file not found or does not belong to the user's tenant.");
        }

        user.ProfileFileId = profileFileId.Value;
    }

    private static string? BuildProfileUrl(Guid? profileFileId) =>
        profileFileId.HasValue ? $"/api/v1/files/{profileFileId.Value}/download" : null;

    private static UserTenantDetails MapTenantDetails(
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

    private static UserResponse MapToUserResponse(
        ApplicationUser user,
        List<string> roles,
        bool includeTenantDetails,
        UserTenantDetails? tenantDetails = null,
        Address? address = null) =>
        new()
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email!,
            TenantId = user.TenantId,
            Roles = roles,
            ProfileFileId = user.ProfileFileId,
            ProfileUrl = BuildProfileUrl(user.ProfileFileId),
            Address = AddressFormatter.ToResponse(address),
            Tenant = includeTenantDetails ? tenantDetails : null
        };

    // Used for single-user mapping where role batch load isn't worth it.
    private async Task<UserResponse> MapToUserResponseAsync(
        ApplicationUser user,
        bool includeTenantDetails,
        UserTenantDetails? tenantDetails = null,
        Address? address = null)
    {
        var roles = await _userManager.GetRolesAsync(user);

        return MapToUserResponse(
            user,
            roles.ToList(),
            includeTenantDetails,
            tenantDetails,
            address);
    }
}
