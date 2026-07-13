using Application.Common;
using Application.DTOs.ActivityLogs;
using Application.DTOs.Common;
using Application.DTOs.Users;
using Application.Exceptions;
using Application.Interfaces.ActivityLogs;
using Application.Interfaces.Authentication;
using Application.Interfaces.Caching;
using Application.Interfaces.Email;
using Application.Interfaces.Files;
using Application.Interfaces.Tenant;
using Application.Interfaces.Users;
using Domain.Entities;
using Domain.Enums;
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

namespace Infrastructure.Users;

public class UserManagementService : TenantScopedService, IUserManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IActivityLogService _activityLogService;
    private readonly IAppCache _cache;
    private readonly IIdentityRoleService _identityRoleService;
    private readonly IEmailService _emailService;
    private readonly IFileService _fileService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _appBaseUrl;

    private static readonly TimeSpan SetupTokenLifetime = TimeSpan.FromDays(7);

    private static readonly HashSet<string> AvatarExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp"
    };

    private const long AvatarMaxBytes = 5 * 1024 * 1024; // 5 MB

    public UserManagementService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ICurrentTenantService currentTenantService,
        IActivityLogService activityLogService,
        IAppCache cache,
        IIdentityRoleService identityRoleService,
        IEmailService emailService,
        IFileService fileService,
        IFileStorageService fileStorageService,
        IRefreshTokenService refreshTokenService,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration)
        : base(currentTenantService)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _activityLogService = activityLogService;
        _cache = cache;
        _identityRoleService = identityRoleService;
        _emailService = emailService;
        _fileService = fileService;
        _fileStorageService = fileStorageService;
        _refreshTokenService = refreshTokenService;
        _httpContextAccessor = httpContextAccessor;
        _appBaseUrl = configuration["AppBaseUrl"] ?? "https://app.example.com";
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

        var roles = await _context.Roles
            .Where(r => request.RoleIds.Contains(r.Id) && r.TenantId == tenantId)
            .ToListAsync();

        var notFound = request.RoleIds.Except(roles.Select(r => r.Id)).ToList();
        if (notFound.Count > 0)
            throw new NotFoundException($"Role(s) not found for this tenant: {string.Join(", ", notFound)}");

        var normalised = request.Email.ToUpperInvariant();

        var activeExists = await _userManager.Users
            .AnyAsync(u => u.NormalizedEmail == normalised && u.TenantId == tenantId);

        if (activeExists)
            throw new ConflictException("User email already exists.");

        // Always create a fresh user record. The unique index on (Email, TenantId) filters
        // out soft-deleted rows, so a new insert is safe even if a deleted record exists.

        // Created inactive — user sets their own password via the account-setup email.
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SystemRole = SystemRole.TenantUser,
            FullName = request.FullName,
            Email = request.Email,
            UserName = request.Email,
            NormalizedEmail = normalised,
            NormalizedUserName = normalised,
            EmailConfirmed = false,
            IsActive = false,
            CreatedVia = CreatedVia.Direct,
            CreatedAt = DateTime.UtcNow
        };

        var placeholder = $"Placeholder!{Guid.NewGuid():N}";
        var result = await _userManager.CreateAsync(user, placeholder);

        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));

        foreach (var role in roles)
            await _identityRoleService.AddUserToRoleAsync(user.Id, role.Id);

        // Invalidate any prior unused tokens, then issue a fresh one.
        var stale = await _context.AccountSetupTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null)
            .ToListAsync();

        _context.AccountSetupTokens.RemoveRange(stale);

        var (rawToken, tokenHash) = TokenHelper.Generate();
        var now = DateTime.UtcNow;

        _context.AccountSetupTokens.Add(new AccountSetupToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = tenantId,
            TokenHash = tokenHash,
            ExpiresAt = now.Add(SetupTokenLifetime),
            CreatedAt = now,
        });

        await _context.SaveChangesAsync();

        try
        {
            var setupUrl = $"{_appBaseUrl}/account-setup?token={rawToken}";
            await _emailService.SendAccountSetupEmailAsync(user.Email!, user.FullName, setupUrl);
        }
        catch
        {
            // Non-fatal - can be resent via the resend endpoint
        }

        await LogActivityAsync(
            ActivityActions.Users.Created,
            $"Created user '{user.Email}'.");

        return await MapToUserResponseAsync(user, includeTenantDetails: false);
    }

    public async Task<PagedResponse<UserResponse>> GetUsersAsync(
        int page, int pageSize,
        string? search = null,
        string? sortBy = null,
        string? sortOrder = null,
        bool? isActive = null,
        CreatedVia? createdVia = null)
    {
        var currentUserId = RequireUserId();

        (page, pageSize) = Pagination.Normalize(page, pageSize);

        var isAdmin = IsSystemAdmin();
        var tenantId = RequireTenantId();

        IQueryable<ApplicationUser> query = _userManager.Users
            .Where(u => u.Id != currentUserId
                     && u.TenantId == tenantId
                     && u.SystemRole == SystemRole.TenantUser);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                u.FullName.Contains(search) || u.Email!.Contains(search));
        }

        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        if (createdVia.HasValue)
        {
            query = query.Where(u => u.CreatedVia == createdVia.Value);
        }

        var totalCount = await query.CountAsync();

        query = (sortBy?.ToLowerInvariant(), sortOrder?.ToLowerInvariant()) switch
        {
            ("fullname", "desc") => query.OrderByDescending(u => u.FullName),
            ("fullname", _) => query.OrderBy(u => u.FullName),
            ("email", "desc") => query.OrderByDescending(u => u.Email),
            ("lastloginat", "desc") => query.OrderByDescending(u => u.LastLoginAt),
            ("lastloginat", _) => query.OrderBy(u => u.LastLoginAt),
            _ => query.OrderBy(u => u.Email),
        };

        var users = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Batch-load roles for all users in one query - eliminates N+1.
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

        var pendingSetupIds = await _context.AccountSetupTokens
            .AsNoTracking()
            .Where(t => userIds.Contains(t.UserId) && t.UsedAt == null)
            .Select(t => t.UserId)
            .ToHashSetAsync();

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

            responses.Add(MapToUserResponse(user, roles, isAdmin, tenantDetails, userAddress,
                hasPendingSetup: pendingSetupIds.Contains(user.Id)));
        }

        return new PagedResponse<UserResponse>
        {
            Items = responses,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<UserResponse> GetByIdAsync(Guid id)
    {
        var isAdmin = IsSystemAdmin();
        var tenantId = RequireTenantId();

        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId)
            ?? throw new NotFoundException("User not found.");

        UserTenantDetails? tenantDetails = null;

        if (isAdmin)
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

        var address = await AddressHelper.GetUserAddressAsync(_context, user.Id, isAdmin);

        return await MapToUserResponseAsync(user, includeTenantDetails: isAdmin, tenantDetails, address);
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

        if (request.RoleId.HasValue)
        {
            var role = await _context.Roles
                .FirstOrDefaultAsync(r => r.Id == request.RoleId.Value && r.TenantId == user.TenantId)
                ?? throw new NotFoundException($"Role '{request.RoleId}' was not found for this tenant.");

            var existingAssignments = await _context.Set<IdentityUserRole<Guid>>()
                .Where(ur => ur.UserId == user.Id)
                .ToListAsync();

            _context.Set<IdentityUserRole<Guid>>().RemoveRange(existingAssignments);

            await _identityRoleService.AddUserToRoleAsync(user.Id, role.Id);
        }

        // UserManager.UpdateAsync internally calls SaveChangesAsync - no second call needed.
        await _userManager.UpdateAsync(user);

        await LogActivityAsync(ActivityActions.Users.Updated, $"Updated user '{user.Email}'.");

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

    public async Task ChangePasswordAsync(ChangePasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmPassword)
        {
            throw new InvalidOperationException("New password and confirmation do not match.");
        }

        var user = await GetCurrentUserEntityAsync();

        var isValid = await _userManager.CheckPasswordAsync(user, request.CurrentPassword);

        if (!isValid)
        {
            throw new InvalidOperationException("Current password is incorrect.");
        }

        var result = await _userManager.ChangePasswordAsync(
            user, request.CurrentPassword, request.NewPassword);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        user.PasswordSetAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await _refreshTokenService.RevokeAllForUserAsync(user.Id, ip);

        await LogActivityAsync(ActivityActions.Users.Updated, "Changed own password.");
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

        if (!IsSystemAdmin() && user.SystemRole == SystemRole.TenantAdmin)
        {
            throw new ForbiddenException("Tenant administrators cannot delete other tenant admin accounts.");
        }

        user.DeletedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        _cache.InvalidateUserStatus(user.Id);

        await LogActivityAsync(ActivityActions.Users.Deleted, $"Deleted user '{user.Email}'.");
    }

    // ── Tenant Admin management (System Admin scope) ──────────────────────────

    public async Task<PagedResponse<UserResponse>> GetTenantAdminsAsync(
        int page, int pageSize,
        string? search = null,
        Guid? tenantId = null,
        bool? isActive = null,
        CreatedVia? createdVia = null,
        string? sortBy = null,
        string? sortOrder = null)
    {
        if (!IsSystemAdmin())
        {
            throw new ForbiddenException("Only system administrators can list tenant admins.");
        }

        (page, pageSize) = Pagination.Normalize(page, pageSize);

        IQueryable<ApplicationUser> query = _userManager.Users
            .Where(u => u.SystemRole == SystemRole.TenantAdmin);

        if (tenantId.HasValue)
        {
            query = query.Where(u => u.TenantId == tenantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                u.FullName.Contains(search) || u.Email!.Contains(search));
        }

        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        if (createdVia.HasValue)
        {
            query = query.Where(u => u.CreatedVia == createdVia.Value);
        }

        var totalCount = await query.CountAsync();

        query = (sortBy?.ToLowerInvariant(), sortOrder?.ToLowerInvariant()) switch
        {
            ("fullname", "desc") => query.OrderByDescending(u => u.FullName),
            ("fullname", _) => query.OrderBy(u => u.FullName),
            _ => query.OrderBy(u => u.Email),
        };

        var users = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var userIds = users.Select(u => u.Id).ToList();
        var tenantIds = users.Select(u => u.TenantId).Distinct().ToList();

        var userRoles = await _context.Set<IdentityUserRole<Guid>>()
            .AsNoTracking()
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(_context.Roles.AsNoTracking(), ur => ur.RoleId, r => r.Id,
                (ur, r) => new { ur.UserId, r.Name })
            .ToListAsync();

        var rolesByUser = userRoles
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name!).ToList());

        var tenantsById = await _context.Tenants
            .IgnoreQueryFilters()
            .Where(t => tenantIds.Contains(t.Id) && t.DeletedAt == null)
            .ToDictionaryAsync(t => t.Id);

        var tenantAddressesById = await AddressHelper.GetTenantAddressesAsync(_context, tenantIds);
        var userAddresses = await AddressHelper.GetUserAddressesAsync(_context, userIds, ignoreTenantFilter: true);

        var pendingSetupIds = await _context.AccountSetupTokens
            .AsNoTracking()
            .Where(t => userIds.Contains(t.UserId) && t.UsedAt == null)
            .Select(t => t.UserId)
            .ToHashSetAsync();

        var responses = users.Select(user =>
        {
            tenantsById.TryGetValue(user.TenantId, out var tenant);
            tenantAddressesById.TryGetValue(user.TenantId, out var tenantAddress);
            userAddresses.TryGetValue(user.Id, out var userAddress);

            var tenantDetails = tenant != null ? MapTenantDetails(tenant, tenantAddress) : null;

            return MapToUserResponse(
                user,
                rolesByUser.GetValueOrDefault(user.Id, []),
                includeTenantDetails: true,
                tenantDetails,
                userAddress,
                hasPendingSetup: pendingSetupIds.Contains(user.Id));
        }).ToList();

        return new PagedResponse<UserResponse>
        {
            Items = responses,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<UserResponse> GetTenantAdminByIdAsync(Guid id)
    {
        if (!IsSystemAdmin())
        {
            throw new ForbiddenException("Only system administrators can view tenant admin details.");
        }

        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId != Guid.Empty)
            ?? throw new NotFoundException("Tenant admin not found.");

        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == user.TenantId && t.DeletedAt == null);

        UserTenantDetails? tenantDetails = null;

        if (tenant != null)
        {
            var tenantAddress = await AddressHelper.GetTenantAddressAsync(_context, tenant.Id);
            tenantDetails = MapTenantDetails(tenant, tenantAddress);
        }

        var address = await AddressHelper.GetUserAddressAsync(_context, user.Id, ignoreTenantFilter: true);

        return await MapToUserResponseAsync(user, includeTenantDetails: true, tenantDetails, address);
    }

    public async Task<UserResponse> UpdateTenantAdminAsync(UpdateTenantAdminRequest request)
    {
        if (!IsSystemAdmin())
        {
            throw new ForbiddenException("Only system administrators can update tenant admins.");
        }

        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.TenantId != Guid.Empty)
            ?? throw new NotFoundException("Tenant admin not found.");

        user.FullName = request.FullName;
        user.UpdatedAt = DateTime.UtcNow;

        await ApplyProfileFileUpdateAsync(user, request.ProfileFileId, request.ClearProfileImage);

        await AddressHelper.ApplyUserAddressUpdateAsync(
            _context, user, request.Address, request.ClearAddress);

        if (request.RoleId.HasValue)
        {
            var role = await _context.Roles
                .FirstOrDefaultAsync(r => r.Id == request.RoleId.Value && r.TenantId == user.TenantId)
                ?? throw new NotFoundException($"Role '{request.RoleId}' was not found for this tenant.");

            var existingAssignments = await _context.Set<IdentityUserRole<Guid>>()
                .Where(ur => ur.UserId == user.Id)
                .ToListAsync();

            _context.Set<IdentityUserRole<Guid>>().RemoveRange(existingAssignments);

            await _identityRoleService.AddUserToRoleAsync(user.Id, role.Id);
        }

        await _userManager.UpdateAsync(user);

        await LogActivityAsync(ActivityActions.Users.Updated, $"Updated tenant admin '{user.Email}'.");

        var address = await AddressHelper.GetUserAddressAsync(_context, user.Id, ignoreTenantFilter: true);

        return await MapToUserResponseAsync(user, includeTenantDetails: true, address: address);
    }

    public async Task DeleteTenantAdminAsync(Guid id)
    {
        if (!IsSystemAdmin())
        {
            throw new ForbiddenException("Only system administrators can delete tenant admins.");
        }

        var currentUserId = RequireUserId();

        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId != Guid.Empty)
            ?? throw new NotFoundException("Tenant admin not found.");

        if (user.Id == currentUserId)
        {
            throw new ConflictException("You cannot delete your own account.");
        }

        // Block deletion if this is the last admin for the tenant
        var remainingAdminCount = await _userManager.Users
            .CountAsync(u => u.TenantId == user.TenantId
                          && u.SystemRole == SystemRole.TenantAdmin
                          && u.Id != id);

        if (remainingAdminCount == 0)
        {
            throw new ConflictException(
                "Cannot delete the last tenant admin. The tenant must have at least one active admin.");
        }

        user.DeletedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        _cache.InvalidateUserStatus(user.Id);

        await LogActivityAsync(ActivityActions.Users.Deleted, $"Deleted tenant admin '{user.Email}'.");
    }

    public async Task<UserResponse> UploadCurrentUserAvatarAsync(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName);
        if (!AvatarExtensions.Contains(extension))
            throw new InvalidOperationException("Profile picture must be a JPEG, PNG, GIF, or WebP image.");

        if (file.Length > AvatarMaxBytes)
            throw new InvalidOperationException("Profile picture must be smaller than 5 MB.");

        using (var dimensionStream = file.OpenReadStream())
        {
            var (width, height) = ReadImageDimensions(dimensionStream);
            if (width <= 0 || height <= 0)
                throw new InvalidOperationException("Could not read image dimensions. Please upload a valid image file.");
            if (width != height)
                throw new InvalidOperationException(
                    $"Profile picture must be square ({width}×{height} is not square). Please crop your image before uploading.");
        }

        var user = await GetCurrentUserEntityAsync();
        var previousFileId = user.ProfileFileId;

        var uploaded = await _fileService.UploadAsync(file);

        user.ProfileFileId = uploaded.Id;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        if (previousFileId.HasValue)
        {
            try { await _fileService.DeleteAsync(previousFileId.Value); }
            catch { /* old file already gone - not fatal */ }
        }

        await LogActivityAsync(ActivityActions.Users.Updated, "Uploaded profile picture.");

        return await GetCurrentUserAsync();
    }

    public async Task<UserResponse> RemoveCurrentUserAvatarAsync()
    {
        var user = await GetCurrentUserEntityAsync();

        if (user.ProfileFileId.HasValue)
            await _fileService.DeleteAsync(user.ProfileFileId.Value);

        await LogActivityAsync(ActivityActions.Users.Updated, "Removed profile picture.");

        return await GetCurrentUserAsync();
    }

    public async Task<UserResponse> UploadUserAvatarByIdAsync(Guid userId, IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName);
        if (!AvatarExtensions.Contains(extension))
            throw new InvalidOperationException("Profile picture must be a JPEG, PNG, GIF, or WebP image.");

        if (file.Length > AvatarMaxBytes)
            throw new InvalidOperationException("Profile picture must be smaller than 5 MB.");

        using (var dimensionStream = file.OpenReadStream())
        {
            var (width, height) = ReadImageDimensions(dimensionStream);
            if (width <= 0 || height <= 0)
                throw new InvalidOperationException("Could not read image dimensions. Please upload a valid image file.");
            if (width != height)
                throw new InvalidOperationException(
                    $"Profile picture must be square ({width}×{height} is not square). Please crop your image before uploading.");
        }

        var user = await _userManager.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null)
            ?? throw new NotFoundException("User not found.");

        var previousFileId = user.ProfileFileId;
        var uploaded = await _fileService.UploadAsync(file);

        user.ProfileFileId = uploaded.Id;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        if (previousFileId.HasValue)
        {
            try { await _fileService.DeleteAsync(previousFileId.Value); }
            catch { /* old file already gone */ }
        }

        await LogActivityAsync(ActivityActions.Users.Updated, $"Uploaded profile picture for user '{user.Email}'.");

        return await MapToUserResponseAsync(user, false);
    }

    public async Task<UserResponse> RemoveUserAvatarByIdAsync(Guid userId)
    {
        var user = await _userManager.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null)
            ?? throw new NotFoundException("User not found.");

        if (user.ProfileFileId.HasValue)
        {
            try { await _fileService.DeleteAsync(user.ProfileFileId.Value); }
            catch { /* file already gone */ }
            user.ProfileFileId = null;
            user.UpdatedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
        }

        await LogActivityAsync(ActivityActions.Users.Updated, $"Removed profile picture for user '{user.Email}'.");

        return await MapToUserResponseAsync(user, false);
    }

    public async Task<(Stream Stream, string ContentType, string FileName)?> GetUserAvatarAsync(Guid userId)
    {
        var user = await _userManager.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user?.ProfileFileId == null)
            return null;

        // IgnoreQueryFilters: SystemAdmin needs to view avatars across tenants.
        var file = await _context.Files
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.Id == user.ProfileFileId.Value && f.DeletedAt == null);

        if (file == null)
            return null;

        var stream = await _fileStorageService.OpenReadAsync(file.RelativePath);
        return (stream, file.ContentType, file.OriginalName);
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

    /// <summary>
    /// Reads image dimensions from the stream header without decoding pixel data.
    /// Supports JPEG, PNG, GIF, and WebP (lossy VP8 and lossless VP8L).
    /// Returns (0, 0) when dimensions cannot be determined.
    /// </summary>
    private static (int Width, int Height) ReadImageDimensions(Stream stream)
    {
        try
        {
            var h = new byte[30];
            var read = stream.Read(h, 0, h.Length);

            if (read < 12) return (0, 0);

            // PNG: magic \x89PNG\r\n\x1a\n, then IHDR chunk at offset 8 (width at 16, height at 20)
            if (read >= 24
                && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47)
            {
                return (ReadInt32BE(h, 16), ReadInt32BE(h, 20));
            }

            // GIF: GIF87a / GIF89a — logical width at offset 6, height at offset 8 (little-endian)
            if (read >= 10 && h[0] == 'G' && h[1] == 'I' && h[2] == 'F')
            {
                return (ReadUInt16LE(h, 6), ReadUInt16LE(h, 8));
            }

            // WebP: "RIFF????WEBP VP8 " (lossy) or "RIFF????WEBP VP8L" (lossless)
            if (read >= 28
                && h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F'
                && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P')
            {
                // Lossy VP8: chunk id "VP8 ", bitstream start code at h[23..25] = 9D 01 2A
                if (h[12] == 'V' && h[13] == 'P' && h[14] == '8' && h[15] == ' '
                    && read >= 30 && h[23] == 0x9D && h[24] == 0x01 && h[25] == 0x2A)
                {
                    return ((ReadUInt16LE(h, 26) & 0x3FFF) + 1,
                            (ReadUInt16LE(h, 28) & 0x3FFF) + 1);
                }

                // Lossless VP8L: chunk id "VP8L", signature byte 0x2F at h[20]
                if (h[12] == 'V' && h[13] == 'P' && h[14] == '8' && h[15] == 'L'
                    && read >= 25 && h[20] == 0x2F)
                {
                    // Dimensions packed in 28 bits: width-1 (14 bits) | height-1 (14 bits)
                    var bits = ReadUInt32LE(h, 21);
                    return ((int)(bits & 0x3FFF) + 1,
                            (int)((bits >> 14) & 0x3FFF) + 1);
                }

                // Extended VP8X: canvas width at byte 24 (3-byte LE, +1), height at 27 (3-byte LE, +1)
                if (h[12] == 'V' && h[13] == 'P' && h[14] == '8' && h[15] == 'X' && read >= 30)
                {
                    var w = ((h[24]) | (h[25] << 8) | (h[26] << 16)) + 1;
                    var ht = ((h[27]) | (h[28] << 8) | (h[29] << 16)) + 1;
                    return (w, ht);
                }
            }

            // JPEG: FF D8 — scan for SOF (Start of Frame) marker
            if (read >= 2 && h[0] == 0xFF && h[1] == 0xD8)
            {
                return ScanJpegDimensions(stream);
            }
        }
        catch { }

        return (0, 0);
    }

    private static (int Width, int Height) ScanJpegDimensions(Stream stream)
    {
        stream.Seek(2, SeekOrigin.Begin); // skip SOI marker
        var buf = new byte[7];
        while (stream.Read(buf, 0, 2) == 2 && buf[0] == 0xFF)
        {
            var marker = buf[1];

            // SOF markers: C0–CF except C4 (DHT), C8 (reserved), CC (DAC)
            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                // Segment layout after FF Cx:
                // [0-1] 2B length  [2] 1B precision  [3-4] 2B height (BE)  [5-6] 2B width (BE)
                if (stream.Read(buf, 0, 7) < 7) break;
                var height = (buf[3] << 8) | buf[4];
                var width = (buf[5] << 8) | buf[6];
                return (width, height);
            }

            // Skip segment body (length includes its own 2 bytes)
            if (stream.Read(buf, 0, 2) < 2) break;
            var segLen = (buf[0] << 8) | buf[1];
            if (segLen < 2) break;
            stream.Seek(segLen - 2, SeekOrigin.Current);
        }

        return (0, 0);
    }

    private static int ReadInt32BE(byte[] b, int offset) =>
        (b[offset] << 24) | (b[offset + 1] << 16) | (b[offset + 2] << 8) | b[offset + 3];

    private static int ReadUInt16LE(byte[] b, int offset) =>
        b[offset] | (b[offset + 1] << 8);

    private static uint ReadUInt32LE(byte[] b, int offset) =>
        (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));

    private async Task<ApplicationUser?> FindManagedUserAsync(string email)
    {
        var normalizedEmail = email.ToUpperInvariant();
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
        var previousFileId = user.ProfileFileId;

        if (clearProfileImage)
        {
            user.ProfileFileId = null;
        }
        else if (!profileFileId.HasValue)
        {
            return;
        }
        else
        {
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

        if (previousFileId.HasValue && previousFileId != user.ProfileFileId)
        {
            var oldFile = await _context.Files
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(f => f.Id == previousFileId.Value && f.DeletedAt == null);

            if (oldFile is not null)
            {
                var relativePath = oldFile.RelativePath;
                oldFile.DeletedAt = DateTime.UtcNow;

                try { await _fileStorageService.DeletePhysicalAsync(relativePath); }
                catch { /* non-fatal: record is soft-deleted; orphaned file requires manual cleanup */ }
            }
        }
    }

    private static UserTenantDetails MapTenantDetails(
        Domain.Entities.Tenant tenant,
        Address? address = null) =>
        new()
        {
            Id = tenant.Id,
            Name = tenant.Name,
            IsActive = tenant.IsActive,
            ProfileFileId = tenant.ProfileFileId,
            Address = AddressFormatter.ToResponse(address),
        };

    private static UserResponse MapToUserResponse(
        ApplicationUser user,
        List<string> roles,
        bool includeTenantDetails,
        UserTenantDetails? tenantDetails = null,
        Address? address = null,
        bool hasPendingSetup = false) =>
        new()
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email!,
            TenantId = user.TenantId,
            SystemRole = user.SystemRole,
            IsActive = user.IsActive,
            Roles = roles,
            ProfileFileId = user.ProfileFileId,
            Address = AddressFormatter.ToResponse(address),
            Tenant = includeTenantDetails ? tenantDetails : null,
            CreatedVia = user.CreatedVia,
            LastLoginAt = user.LastLoginAt,
            HasPendingSetup = hasPendingSetup,
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

