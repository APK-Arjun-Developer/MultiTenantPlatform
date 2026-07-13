using Application.DTOs.Common;
using Application.DTOs.Users;
using Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Application.Interfaces.Users;

public interface IUserManagementService
{
    Task<UserResponse> CreateUserAsync(CreateUserRequest request);

    Task<PagedResponse<UserResponse>> GetUsersAsync(int page, int pageSize, string? search = null, string? sortBy = null, string? sortOrder = null, bool? isActive = null, CreatedVia? createdVia = null);

    Task<UserResponse> GetByIdAsync(Guid id);

    Task<UserResponse> GetCurrentUserAsync();

    Task<UserResponse> UpdateUserAsync(UpdateUserRequest request);

    Task<UserResponse> UpdateCurrentUserAsync(UpdateCurrentUserRequest request);

    Task DeleteUserAsync(DeleteUserRequest request);

    Task ChangePasswordAsync(ChangePasswordRequest request);

    Task<UserResponse> UploadCurrentUserAvatarAsync(IFormFile file);

    Task<UserResponse> RemoveCurrentUserAvatarAsync();

    Task<(Stream Stream, string ContentType, string FileName)?> GetUserAvatarAsync(Guid userId);

    Task<UserResponse> UploadUserAvatarByIdAsync(Guid userId, IFormFile file);

    Task<UserResponse> RemoveUserAvatarByIdAsync(Guid userId);

    // ── Tenant Admin management (System Admin scope) ──────────────────────────

    Task<PagedResponse<UserResponse>> GetTenantAdminsAsync(int page, int pageSize, string? search = null, Guid? tenantId = null, bool? isActive = null, CreatedVia? createdVia = null, string? sortBy = null, string? sortOrder = null);

    Task<UserResponse> GetTenantAdminByIdAsync(Guid id);

    Task<UserResponse> UpdateTenantAdminAsync(UpdateTenantAdminRequest request);

    Task DeleteTenantAdminAsync(Guid id);
}
