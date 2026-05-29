using Application.Common;
using Application.DTOs.Users;
using Application.Interfaces.Tenant;
using Application.Interfaces.Users;
using Infrastructure.Identity;
using Infrastructure.Identity.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Users;

public class UserManagementService : IUserManagementService
{
    private readonly ApplicationDbContext _context;

    private readonly UserManager<ApplicationUser> _userManager;

    private readonly RoleManager<ApplicationRole> _roleManager;

    private readonly ICurrentTenantService _currentTenantService;

    public UserManagementService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ICurrentTenantService currentTenantService)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _currentTenantService = currentTenantService;
    }

    public async Task<UserResponse> CreateUserAsync(CreateUserRequest request)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Tenant users cannot be created in the platform scope.");
        }

        if (request.RoleName == RoleNames.SuperAdmin)
        {
            throw new InvalidOperationException(
                "Cannot assign the SuperAdmin role.");
        }

        var role = await IdentityRoleHelper.FindRoleByNameAsync(
            _roleManager,
            tenantId,
            request.RoleName);

        if (role == null)
        {
            throw new InvalidOperationException(
                $"Role '{request.RoleName}' was not found for this tenant.");
        }

        var emailExists = await _userManager.Users
            .AnyAsync(u =>
                u.NormalizedEmail == request.Email.ToUpperInvariant() &&
                u.TenantId == tenantId);

        if (emailExists)
        {
            throw new InvalidOperationException("User email already exists.");
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
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        await IdentityRoleHelper.AddUserToRoleAsync(
            _context,
            user.Id,
            role.Id);

        return await MapToUserResponseAsync(user, includeTenantDetails: false);
    }

    public async Task<IReadOnlyList<UserResponse>> GetUsersAsync()
    {
        var currentUserId = _currentTenantService.UserId
            ?? throw new InvalidOperationException(
                "User context is required. Ensure user_id is present in the JWT.");

        var isSystemAdmin = IsSystemAdmin();

        IQueryable<ApplicationUser> query = _userManager.Users
            .Where(u => u.Id != currentUserId);

        if (isSystemAdmin)
        {
            query = query.Where(u => u.TenantId != Guid.Empty);
        }
        else
        {
            var tenantId = RequireTenantId();
            query = query.Where(u => u.TenantId == tenantId);
        }

        var users = await query
            .OrderBy(u => u.Email)
            .ToListAsync();

        Dictionary<Guid, Domain.Entities.Tenant> tenantsById = [];

        if (isSystemAdmin && users.Count > 0)
        {
            var tenantIds = users.Select(u => u.TenantId).Distinct().ToList();

            tenantsById = await _context.Tenants
                .IgnoreQueryFilters()
                .Where(t => tenantIds.Contains(t.Id) && t.DeletedAt == null)
                .ToDictionaryAsync(t => t.Id);
        }

        var responses = new List<UserResponse>();

        foreach (var user in users)
        {
            UserTenantDetails? tenantDetails = null;

            if (isSystemAdmin && tenantsById.TryGetValue(user.TenantId, out var tenant))
            {
                tenantDetails = new UserTenantDetails
                {
                    Id = tenant.Id,
                    Name = tenant.Name,
                    Slug = tenant.Slug,
                    IsActive = tenant.IsActive
                };
            }

            responses.Add(await MapToUserResponseAsync(
                user,
                includeTenantDetails: isSystemAdmin,
                tenantDetails));
        }

        return responses;
    }

    public async Task<UserResponse> GetCurrentUserAsync()
    {
        var user = await GetCurrentUserEntityAsync();

        UserTenantDetails? tenantDetails = null;

        if (IsSystemAdmin() && user.TenantId != Guid.Empty)
        {
            var tenant = await _context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == user.TenantId && t.DeletedAt == null);

            if (tenant != null)
            {
                tenantDetails = new UserTenantDetails
                {
                    Id = tenant.Id,
                    Name = tenant.Name,
                    Slug = tenant.Slug,
                    IsActive = tenant.IsActive
                };
            }
        }
        else if (!IsSystemAdmin())
        {
            var tenant = await _context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == user.TenantId && t.DeletedAt == null);

            if (tenant != null)
            {
                tenantDetails = new UserTenantDetails
                {
                    Id = tenant.Id,
                    Name = tenant.Name,
                    Slug = tenant.Slug,
                    IsActive = tenant.IsActive
                };
            }
        }

        return await MapToUserResponseAsync(
            user,
            includeTenantDetails: tenantDetails != null,
            tenantDetails);
    }

    public async Task<UserResponse> UpdateUserAsync(UpdateUserRequest request)
    {
        var user = await FindManagedUserAsync(request.Email);

        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }

        user.FullName = request.FullName;
        user.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var passwordResult = await _userManager.ResetPasswordAsync(
                user,
                token,
                request.Password);

            if (!passwordResult.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join(", ",
                        passwordResult.Errors.Select(e => e.Description)));
            }
        }

        if (!string.IsNullOrWhiteSpace(request.RoleName))
        {
            if (request.RoleName == RoleNames.SuperAdmin)
            {
                throw new InvalidOperationException(
                    "Cannot assign the SuperAdmin role.");
            }

            var tenantId = user.TenantId;
            var role = await IdentityRoleHelper.FindRoleByNameAsync(
                _roleManager,
                tenantId,
                request.RoleName);

            if (role == null)
            {
                throw new InvalidOperationException(
                    $"Role '{request.RoleName}' was not found for this tenant.");
            }

            var existingAssignments = await _context.Set<IdentityUserRole<Guid>>()
                .Where(ur => ur.UserId == user.Id)
                .ToListAsync();

            _context.Set<IdentityUserRole<Guid>>().RemoveRange(existingAssignments);

            await IdentityRoleHelper.AddUserToRoleAsync(
                _context,
                user.Id,
                role.Id);
        }

        await _userManager.UpdateAsync(user);

        return await MapToUserResponseAsync(user, includeTenantDetails: IsSystemAdmin());
    }

    public async Task DeleteUserAsync(DeleteUserRequest request)
    {
        var currentUserId = _currentTenantService.UserId
            ?? throw new InvalidOperationException("User context is required.");

        var user = await FindManagedUserAsync(request.Email);

        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }

        if (user.Id == currentUserId)
        {
            throw new InvalidOperationException("You cannot delete your own account.");
        }

        user.DeletedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    private async Task<ApplicationUser> GetCurrentUserEntityAsync()
    {
        var userId = _currentTenantService.UserId
            ?? throw new InvalidOperationException(
                "User context is required. Ensure user_id is present in the JWT.");

        return await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("User not found.");
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

    private async Task<UserResponse> MapToUserResponseAsync(
        ApplicationUser user,
        bool includeTenantDetails,
        UserTenantDetails? tenantDetails = null)
    {
        var roles = await _userManager.GetRolesAsync(user);

        return new UserResponse
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email!,
            TenantId = user.TenantId,
            Roles = roles.ToList(),
            Tenant = includeTenantDetails ? tenantDetails : null
        };
    }

    private bool IsSystemAdmin() =>
        (_currentTenantService.TenantId ?? Guid.Empty) == Guid.Empty;

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
