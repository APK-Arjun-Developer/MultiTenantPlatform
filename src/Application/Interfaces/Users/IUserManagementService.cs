using Application.DTOs.Users;

namespace Application.Interfaces.Users;

public interface IUserManagementService
{
    Task<UserResponse> CreateUserAsync(CreateUserRequest request);

    Task<IReadOnlyList<UserResponse>> GetUsersAsync();

    Task<UserResponse> GetCurrentUserAsync();

    Task<UserResponse> UpdateUserAsync(UpdateUserRequest request);

    Task DeleteUserAsync(DeleteUserRequest request);
}
