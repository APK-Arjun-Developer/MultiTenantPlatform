using Domain.Entities;

namespace Application.Interfaces.Auth;

public interface IUserService
{
    Task<object?> GetByEmailAsync(string email);

    Task<bool> CheckPasswordAsync(
        object user,
        string password);
}