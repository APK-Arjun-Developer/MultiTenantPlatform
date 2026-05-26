using Application.Interfaces.Auth;
using Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity.Services;

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserService(
        UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<object?> GetByEmailAsync(
        string email)
    {
        return await _userManager.FindByEmailAsync(email);
    }

    public async Task<bool> CheckPasswordAsync(
        object user,
        string password)
    {
        return await _userManager.CheckPasswordAsync(
            (ApplicationUser)user,
            password);
    }
}