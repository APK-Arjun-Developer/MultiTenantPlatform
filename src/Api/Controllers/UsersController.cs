using Api.Attributes;
using Application.Common;
using Application.DTOs.Users;
using Application.Interfaces.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController : ApiControllerBase
{
    private readonly IUserManagementService _userManagementService;

    public UsersController(IUserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    [HttpGet]
    [HasPermission(PermissionNames.UsersView)]
    public async Task<IActionResult> GetAll()
    {
        var response = await _userManagementService.GetUsersAsync();

        return OkEnvelope(response, "Users retrieved.");
    }

    [HttpGet("current")]
    [HasPermission(PermissionNames.UsersView)]
    public async Task<IActionResult> GetCurrent()
    {
        var response = await _userManagementService.GetCurrentUserAsync();

        return OkEnvelope(response, "Current user retrieved.");
    }

    [HttpPost]
    [HasPermission(PermissionNames.UsersCreate)]
    public async Task<IActionResult> Create(CreateUserRequest request)
    {
        var response = await _userManagementService.CreateUserAsync(request);

        return OkEnvelope(response, "User created.");
    }

    [HttpPut]
    [HasPermission(PermissionNames.UsersEdit)]
    public async Task<IActionResult> Update(UpdateUserRequest request)
    {
        var response = await _userManagementService.UpdateUserAsync(request);

        return OkEnvelope(response, "User updated.");
    }

    [HttpDelete]
    [HasPermission(PermissionNames.UsersDelete)]
    public async Task<IActionResult> Delete(DeleteUserRequest request)
    {
        await _userManagementService.DeleteUserAsync(request);

        return OkEnvelope("User deleted.");
    }
}
