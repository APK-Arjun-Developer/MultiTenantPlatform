using Api.Attributes;
using Api.Contracts;
using Api.Controllers;
using Api.Tests.Helpers;
using Application.Common;
using Application.DTOs.Common;
using Application.DTOs.Invitations;
using Application.DTOs.Onboarding;
using Application.DTOs.Users;
using Application.Exceptions;
using Application.Interfaces.Invitations;
using Application.Interfaces.Onboarding;
using Application.Interfaces.Users;
using Domain.Enums;
using Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Api.Tests.Controllers;

public class UsersControllerTests : ControllerTestBase
{
    private readonly Mock<IUserManagementService> _userService = new();
    private readonly Mock<IOnboardingService> _onboardingService = new();
    private readonly Mock<IInvitationService> _invitationService = new();

    private UsersController Build(ControllerContext? ctx = null)
    {
        var controller = new UsersController(
            _userService.Object,
            _onboardingService.Object,
            _invitationService.Object);
        controller.ControllerContext = ctx ?? TenantAdminContext();
        return controller;
    }

    // ── Authorization attribute assertions ────────────────────────────────────

    [Theory]
    [InlineData(nameof(UsersController.GetAll), PermissionNames.UsersList)]
    [InlineData(nameof(UsersController.GetById), PermissionNames.UsersView)]
    [InlineData(nameof(UsersController.Create), PermissionNames.UsersCreate)]
    [InlineData(nameof(UsersController.Update), PermissionNames.UsersEdit)]
    [InlineData(nameof(UsersController.Delete), PermissionNames.UsersDelete)]
    [InlineData(nameof(UsersController.DirectCreate), PermissionNames.OnboardingCreate)]
    [InlineData(nameof(UsersController.Invite), PermissionNames.OnboardingInvite)]
    [InlineData(nameof(UsersController.Activate), PermissionNames.OnboardingActivate)]
    [InlineData(nameof(UsersController.Deactivate), PermissionNames.OnboardingDeactivate)]
    public void ActionMethod_HasExpectedPermissionAttribute(string methodName, string permission)
    {
        var method = typeof(UsersController).GetMethod(methodName)!;
        var attrs = method.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .ToList();
        var expectedPolicy = $"{PermissionPolicyProvider.PolicyPrefix}{permission}";
        Assert.Contains(attrs, a => a.Policy == expectedPolicy);
    }

    [Fact]
    public void Controller_HasAuthorizeAttribute()
    {
        var attr = typeof(UsersController).GetCustomAttributes(typeof(AuthorizeAttribute), true);
        Assert.NotEmpty(attr);
    }

    // ── GetAll ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_DefaultParams_ReturnsPagedResponse()
    {
        var paged = MakePagedResponse(MakeUser(), MakeUser());
        _userService.Setup(s => s.GetUsersAsync(1, 20, null, null, null, null, null))
            .ReturnsAsync(paged);

        var result = await Build().GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<PagedResponse<UserResponse>>>(ok.Value);
        Assert.Equal("Users retrieved.", envelope.Message);
        Assert.Equal(2, envelope.Data!.TotalCount);
    }

    [Fact]
    public async Task GetAll_WithSearchAndFilters_PassesParamsToService()
    {
        var paged = MakePagedResponse();
        _userService.Setup(s => s.GetUsersAsync(2, 10, "john", "name", "asc", true, null))
            .ReturnsAsync(paged);

        var result = await Build().GetAll(page: 2, pageSize: 10, search: "john", sortBy: "name", sortOrder: "asc", isActive: true);

        Assert.IsType<OkObjectResult>(result);
        _userService.Verify(s => s.GetUsersAsync(2, 10, "john", "name", "asc", true, null), Times.Once);
    }

    // ── GetById ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ExistingUser_ReturnsOk()
    {
        var user = MakeUser();
        _userService.Setup(s => s.GetByIdAsync(user.Id)).ReturnsAsync(user);

        var result = await Build().GetById(user.Id);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<UserResponse>>(ok.Value);
        Assert.Equal("User retrieved.", envelope.Message);
        Assert.Equal(user.Id, envelope.Data!.Id);
    }

    [Fact]
    public async Task GetById_UserNotFound_PropagatesNotFoundException()
    {
        var id = Guid.NewGuid();
        _userService.Setup(s => s.GetByIdAsync(id))
            .ThrowsAsync(NotFoundException.For("User", id));

        await Assert.ThrowsAsync<NotFoundException>(() => Build().GetById(id));
    }

    // ── GetCurrent ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCurrent_ReturnsCurrentUser()
    {
        var user = MakeUser();
        _userService.Setup(s => s.GetCurrentUserAsync()).ReturnsAsync(user);

        var result = await Build().GetCurrent();

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<UserResponse>>(ok.Value);
        Assert.Equal("Current user retrieved.", envelope.Message);
        Assert.Equal(user.Id, envelope.Data!.Id);
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidRequest_ReturnsOkWithNewUser()
    {
        var user = MakeUser();
        var request = new CreateUserRequest { FullName = "New User", Email = "new@example.com", RoleIds = [Guid.NewGuid()] };
        _userService.Setup(s => s.CreateUserAsync(request)).ReturnsAsync(user);

        var result = await Build().Create(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<UserResponse>>(ok.Value);
        Assert.Equal("User created.", envelope.Message);
    }

    [Fact]
    public async Task Create_ConflictingEmail_PropagatesConflictException()
    {
        _userService.Setup(s => s.CreateUserAsync(It.IsAny<CreateUserRequest>()))
            .ThrowsAsync(new ConflictException("Email already in use."));

        await Assert.ThrowsAsync<ConflictException>(
            () => Build().Create(new CreateUserRequest { Email = "dup@example.com", FullName = "Dup", RoleIds = [Guid.NewGuid()] }));
    }

    // ── ChangePassword ────────────────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_ValidRequest_ReturnsOk()
    {
        _userService.Setup(s => s.ChangePasswordAsync(It.IsAny<ChangePasswordRequest>()))
            .Returns(Task.CompletedTask);

        var result = await Build().ChangePassword(
            new ChangePasswordRequest { CurrentPassword = "Old@123!", NewPassword = "New@1234!", ConfirmPassword = "New@1234!" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<object?>>(ok.Value);
        Assert.Contains("changed", envelope.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_PropagatesException()
    {
        _userService.Setup(s => s.ChangePasswordAsync(It.IsAny<ChangePasswordRequest>()))
            .ThrowsAsync(new InvalidOperationException("Incorrect current password."));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Build().ChangePassword(new ChangePasswordRequest { CurrentPassword = "wrong", NewPassword = "New@1234!", ConfirmPassword = "New@1234!" }));
    }

    // ── UpdateCurrent ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateCurrent_ValidRequest_ReturnsOkWithUpdatedUser()
    {
        var updated = MakeUser(fullName: "Updated Name");
        _userService.Setup(s => s.UpdateCurrentUserAsync(It.IsAny<UpdateCurrentUserRequest>()))
            .ReturnsAsync(updated);

        var result = await Build().UpdateCurrent(new UpdateCurrentUserRequest { FullName = "Updated Name" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<UserResponse>>(ok.Value);
        Assert.Equal("Profile updated.", envelope.Message);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ValidRequest_ReturnsOkWithUpdatedUser()
    {
        var updated = MakeUser();
        _userService.Setup(s => s.UpdateUserAsync(It.IsAny<UpdateUserRequest>())).ReturnsAsync(updated);

        var result = await Build().Update(new UpdateUserRequest { Email = "u@e.com", FullName = "Name" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<UserResponse>>(ok.Value);
        Assert.Equal("User updated.", envelope.Message);
    }

    [Fact]
    public async Task Update_UserNotFound_PropagatesNotFoundException()
    {
        _userService.Setup(s => s.UpdateUserAsync(It.IsAny<UpdateUserRequest>()))
            .ThrowsAsync(new NotFoundException("User not found."));

        await Assert.ThrowsAsync<NotFoundException>(
            () => Build().Update(new UpdateUserRequest { Email = "ghost@example.com", FullName = "Ghost" }));
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ExistingUser_ReturnsOk()
    {
        _userService.Setup(s => s.DeleteUserAsync(It.IsAny<DeleteUserRequest>()))
            .Returns(Task.CompletedTask);

        var result = await Build().Delete(new DeleteUserRequest { Email = "user@example.com" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<object?>>(ok.Value);
        Assert.Contains("deleted", envelope.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_UserNotFound_PropagatesNotFoundException()
    {
        _userService.Setup(s => s.DeleteUserAsync(It.IsAny<DeleteUserRequest>()))
            .ThrowsAsync(new NotFoundException("User not found."));

        await Assert.ThrowsAsync<NotFoundException>(
            () => Build().Delete(new DeleteUserRequest { Email = "nobody@example.com" }));
    }

    // ── GetAvatar ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAvatar_AvatarExists_ReturnsFileResult()
    {
        var id = Guid.NewGuid();
        var stream = new MemoryStream([1, 2, 3]);
        _userService.Setup(s => s.GetUserAvatarAsync(id))
            .ReturnsAsync((stream, "image/jpeg", "avatar.jpg"));

        var result = await Build().GetAvatar(id);

        var fileResult = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("image/jpeg", fileResult.ContentType);
        Assert.Equal("avatar.jpg", fileResult.FileDownloadName);
    }

    [Fact]
    public async Task GetAvatar_NoAvatar_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _userService.Setup(s => s.GetUserAvatarAsync(id))
            .ReturnsAsync(default((Stream, string, string)?));

        var result = await Build().GetAvatar(id);

        Assert.IsType<NotFoundResult>(result);
    }

    // ── DirectCreate (Onboarding) ─────────────────────────────────────────────

    [Fact]
    public async Task DirectCreate_ValidRequest_ReturnsOkWithCreatedUser()
    {
        var response = new CreateTenantUserResponse
        {
            UserId = Guid.NewGuid(),
            FullName = "New User",
            Email = "new@example.com",
            TenantId = Guid.NewGuid(),
            IsActive = false,
        };
        _onboardingService.Setup(s => s.CreateTenantUserAsync(It.IsAny<CreateTenantUserRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await Build().DirectCreate(
            new CreateTenantUserRequest { FullName = "New User", Email = "new@example.com", RoleIds = [Guid.NewGuid()] },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<CreateTenantUserResponse>>(ok.Value);
        Assert.Contains("setup email sent", envelope.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Invite ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Invite_ValidRequest_ReturnsOkWithInviteResponse()
    {
        var inviteResponse = new InviteResponse
        {
            InvitationId = Guid.NewGuid(),
            InvitationType = InvitationType.TenantUser,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        };
        _invitationService.Setup(s => s.InviteTenantUserAsync(It.IsAny<InviteTenantUserRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inviteResponse);

        var result = await Build().Invite(
            new InviteTenantUserRequest { Email = "invited@example.com", RoleIds = [Guid.NewGuid()] },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<InviteResponse>>(ok.Value);
        Assert.Contains("invitation sent", envelope.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── ResendSetupEmail ──────────────────────────────────────────────────────

    [Fact]
    public async Task ResendSetupEmail_ValidUserId_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        _onboardingService.Setup(s => s.ResendTenantUserSetupEmailAsync(userId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await Build().ResendSetupEmail(userId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<object?>>(ok.Value);
        Assert.Contains("resent", envelope.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── GetInvitations ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetInvitations_ReturnsPagedInvitationList()
    {
        var paged = new PagedResponse<InvitationListItemResponse>
        {
            Items = [new InvitationListItemResponse { Id = Guid.NewGuid(), Email = "inv@example.com", Status = "Pending" }],
            Page = 1,
            PageSize = 20,
            TotalCount = 1,
        };
        _invitationService.Setup(s => s.GetUserInvitationsAsync(1, 20, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await Build().GetInvitations();

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<PagedResponse<InvitationListItemResponse>>>(ok.Value);
        Assert.Equal(1, envelope.Data!.TotalCount);
    }

    // ── RevokeInvitation ──────────────────────────────────────────────────────

    [Fact]
    public async Task RevokeInvitation_ValidId_ReturnsOk()
    {
        var invId = Guid.NewGuid();
        _invitationService.Setup(s => s.RevokeInvitationAsync(invId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await Build().RevokeInvitation(invId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<object?>>(ok.Value);
        Assert.Contains("revoked", envelope.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RevokeInvitation_NotFound_PropagatesException()
    {
        _invitationService.Setup(s => s.RevokeInvitationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Invitation not found."));

        await Assert.ThrowsAsync<NotFoundException>(
            () => Build().RevokeInvitation(Guid.NewGuid(), CancellationToken.None));
    }

    // ── ResendInvitation ──────────────────────────────────────────────────────

    [Fact]
    public async Task ResendInvitation_ValidId_ReturnsOk()
    {
        var invId = Guid.NewGuid();
        _invitationService.Setup(s => s.ResendInvitationAsync(invId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await Build().ResendInvitation(invId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<object?>>(ok.Value);
        Assert.Contains("resent", envelope.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Activate / Deactivate ─────────────────────────────────────────────────

    [Fact]
    public async Task Activate_ValidUserId_ReturnsOkWithActiveStatus()
    {
        var userId = Guid.NewGuid();
        var statusResponse = new UserStatusResponse { UserId = userId, IsActive = true, Email = "u@e.com" };
        _onboardingService.Setup(s => s.ActivateUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(statusResponse);

        var result = await Build().Activate(userId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<UserStatusResponse>>(ok.Value);
        Assert.Equal("User activated.", envelope.Message);
        Assert.True(envelope.Data!.IsActive);
    }

    [Fact]
    public async Task Deactivate_ValidUserId_ReturnsOkWithInactiveStatus()
    {
        var userId = Guid.NewGuid();
        var statusResponse = new UserStatusResponse { UserId = userId, IsActive = false, Email = "u@e.com" };
        _onboardingService.Setup(s => s.DeactivateUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(statusResponse);

        var result = await Build().Deactivate(userId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<UserStatusResponse>>(ok.Value);
        Assert.Equal("User deactivated.", envelope.Message);
        Assert.False(envelope.Data!.IsActive);
    }

    [Fact]
    public async Task Activate_UserAlreadyActive_PropagatesException()
    {
        _onboardingService.Setup(s => s.ActivateUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("User is already active."));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Build().Activate(Guid.NewGuid(), CancellationToken.None));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static UserResponse MakeUser(string? fullName = null) => new()
    {
        Id = Guid.NewGuid(),
        FullName = fullName ?? "Test User",
        Email = "user@example.com",
        TenantId = Guid.NewGuid(),
        SystemRole = SystemRole.TenantUser,
        IsActive = true,
    };

    private static PagedResponse<UserResponse> MakePagedResponse(params UserResponse[] users) => new()
    {
        Items = users,
        Page = 1,
        PageSize = 20,
        TotalCount = users.Length,
    };
}
