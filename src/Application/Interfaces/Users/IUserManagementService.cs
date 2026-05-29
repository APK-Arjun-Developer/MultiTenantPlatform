using Application.DTOs.Common;
using Application.DTOs.Users;

namespace Application.Interfaces.Users;

public interface IUserManagementService
{
    Task<UserResponse> CreateUserAsync(CreateUserRequest request);

    Task<PagedResponse<UserResponse>> GetUsersAsync(int page, int pageSize);

    Task<UserResponse> GetCurrentUserAsync();

    Task<UserResponse> UpdateUserAsync(UpdateUserRequest request);

    Task<UserResponse> UpdateCurrentUserAsync(UpdateCurrentUserRequest request);

    Task DeleteUserAsync(DeleteUserRequest request);
}
