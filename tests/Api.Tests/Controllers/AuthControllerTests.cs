using Api.Contracts;
using Api.Controllers;
using Api.Tests.Helpers;
using Application.DTOs.Auth;
using Application.Exceptions;
using Application.Interfaces.Authentication;
using Application.Interfaces.Authorization;
using Infrastructure.Authentication.JWT;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;

namespace Api.Tests.Controllers;

public class AuthControllerTests : ControllerTestBase
{
    private readonly Mock<IAuthService> _authService = new();
    private readonly Mock<IPasswordResetService> _passwordResetService = new();
    private readonly Mock<IEmailVerificationService> _emailVerificationService = new();
    private readonly Mock<ICurrentUserPermissionService> _permissionService = new();

    private readonly JwtSettings _jwtSettings = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        Key = "test-signing-key-that-is-long-enough!!",
        ExpiryMinutes = 15,
        RefreshTokenExpiryDays = 7,
    };

    private AuthController Build(ControllerContext? ctx = null)
    {
        var controller = new AuthController(
            _authService.Object,
            _passwordResetService.Object,
            _emailVerificationService.Object,
            _permissionService.Object,
            Options.Create(_jwtSettings));
        controller.ControllerContext = ctx ?? BuildContext();
        return controller;
    }

    // ── Authorization attributes ──────────────────────────────────────────────

    [Fact]
    public void Me_HasAuthorizeAttribute()
    {
        var method = typeof(AuthController).GetMethod(nameof(AuthController.Me))!;
        Assert.NotNull(method.GetCustomAttributes(typeof(AuthorizeAttribute), true).FirstOrDefault());
    }

    [Fact]
    public void Logout_HasAuthorizeAttribute()
    {
        var method = typeof(AuthController).GetMethod(nameof(AuthController.Logout))!;
        Assert.NotNull(method.GetCustomAttributes(typeof(AuthorizeAttribute), true).FirstOrDefault());
    }

    // ── Me ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Me_ReturnsOkWithPermissionsPopulated()
    {
        var meResponse = new MeResponse
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            FullName = "Test User",
            SystemRole = "TenantUser",
        };
        var permissions = new[] { "Users.View", "Files.View" };

        _authService.Setup(s => s.GetMeAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(meResponse);
        _permissionService.Setup(s => s.GetPermissionsAsync()).ReturnsAsync(permissions);

        var result = await Build().Me();

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<MeResponse>>(ok.Value);
        Assert.Equal("Success", envelope.Message);
        Assert.Equal(permissions, envelope.Data!.Permissions);
    }

    [Fact]
    public async Task Me_ServiceThrows_PropagatesException()
    {
        _authService.Setup(s => s.GetMeAsync(It.IsAny<ClaimsPrincipal>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected error."));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Build().Me());
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOkWithAuthResponse()
    {
        var authResponse = MakeAuthResponse();
        _authService.Setup(s => s.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<string>()))
            .ReturnsAsync(authResponse);

        var result = await Build().Login(new LoginRequest { Email = "user@example.com", Password = "P@ssw0rd!" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<AuthResponse>>(ok.Value);
        Assert.Equal("Login successful.", envelope.Message);
        Assert.Equal(authResponse.Email, envelope.Data!.Email);
        Assert.Equal(authResponse.AccessToken, envelope.Data.AccessToken);
    }

    [Fact]
    public async Task Login_InvalidCredentials_PropagatesException()
    {
        _authService.Setup(s => s.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<string>()))
            .ThrowsAsync(new UnauthorizedAccessException("Invalid credentials."));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => Build().Login(new LoginRequest { Email = "bad@example.com", Password = "wrong" }));
    }

    [Fact]
    public async Task Login_SetsAccessAndRefreshTokenCookies()
    {
        var authResponse = MakeAuthResponse();
        _authService.Setup(s => s.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<string>()))
            .ReturnsAsync(authResponse);

        var ctx = BuildContext();
        await Build(ctx).Login(new LoginRequest { Email = "user@example.com", Password = "P@ssw0rd!" });

        var setCookieHeader = string.Join("; ", ctx.HttpContext.Response.Headers["Set-Cookie"].ToArray());
        Assert.Contains("access_token", setCookieHeader);
        Assert.Contains("refresh_token", setCookieHeader);
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_ValidCookie_ReturnsOkWithNewTokens()
    {
        var ctx = BuildContextWithCookie("refresh_token", "old-refresh-token");
        var newResponse = MakeAuthResponse("new-access", "new-refresh");

        _authService.Setup(s => s.RefreshTokenAsync("old-refresh-token", It.IsAny<string>()))
            .ReturnsAsync(newResponse);

        var result = await Build(ctx).Refresh();

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<AuthResponse>>(ok.Value);
        Assert.Equal("Token refreshed.", envelope.Message);
        Assert.Equal("new-access", envelope.Data!.AccessToken);
    }

    [Fact]
    public async Task Refresh_MissingCookie_ReturnsUnauthorized()
    {
        var result = await Build().Refresh();

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Refresh_ServiceThrows_PropagatesException()
    {
        var ctx = BuildContextWithCookie("refresh_token", "expired-token");
        _authService.Setup(s => s.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new UnauthorizedAccessException("Token expired."));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Build(ctx).Refresh());
    }

    // ── Logout ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_WithRefreshToken_CallsServiceAndDeletesCookies()
    {
        var ctx = BuildContextWithCookie("refresh_token", "my-refresh-tok");
        _authService.Setup(s => s.LogoutAsync(It.IsAny<string?>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var result = await Build(ctx).Logout();

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<object?>>(ok.Value);
        Assert.Equal("Logged out.", envelope.Message);

        _authService.Verify(s => s.LogoutAsync("my-refresh-tok", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Logout_WithoutRefreshToken_StillReturnsOk()
    {
        _authService.Setup(s => s.LogoutAsync(It.IsAny<string?>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var result = await Build().Logout();

        Assert.IsType<OkObjectResult>(result);
        _authService.Verify(s => s.LogoutAsync(null, It.IsAny<string>()), Times.Once);
    }

    // ── ForgotPassword ────────────────────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_ValidEmail_ReturnsOk()
    {
        _passwordResetService.Setup(s => s.SendResetEmailAsync(It.IsAny<ForgotPasswordRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await Build().ForgotPassword(
            new ForgotPasswordRequest { Email = "user@example.com" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<object?>>(ok.Value);
        Assert.Contains("password reset link", envelope.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ForgotPassword_UserNotFound_SilentlyReturnsOk()
    {
        _passwordResetService.Setup(s => s.SendResetEmailAsync(It.IsAny<ForgotPasswordRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("User not found."));

        var result = await Build().ForgotPassword(
            new ForgotPasswordRequest { Email = "nobody@example.com" },
            CancellationToken.None);

        // NotFoundException is swallowed — user enumeration protection
        Assert.IsType<OkObjectResult>(result);
    }

    // ── ValidateResetToken ────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateResetToken_ValidToken_ReturnsOkWithValidResult()
    {
        var tokenResponse = new ValidateResetTokenResponse { IsValid = true, Email = "user@example.com" };
        _passwordResetService.Setup(s => s.ValidateTokenAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenResponse);

        var result = await Build().ValidateResetToken("valid-token", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<ValidateResetTokenResponse>>(ok.Value);
        Assert.Equal("Token validated.", envelope.Message);
        Assert.True(envelope.Data!.IsValid);
        Assert.Equal("user@example.com", envelope.Data.Email);
    }

    [Fact]
    public async Task ValidateResetToken_InvalidToken_ReturnsResponseWithIsValidFalse()
    {
        var tokenResponse = new ValidateResetTokenResponse { IsValid = false, ErrorMessage = "Token expired." };
        _passwordResetService.Setup(s => s.ValidateTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenResponse);

        var result = await Build().ValidateResetToken("expired-token", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<ValidateResetTokenResponse>>(ok.Value);
        Assert.False(envelope.Data!.IsValid);
        Assert.Equal("Token expired.", envelope.Data.ErrorMessage);
    }

    // ── ResetPassword ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ResetPassword_ValidRequest_ReturnsOk()
    {
        _passwordResetService.Setup(s => s.ResetPasswordAsync(It.IsAny<ResetPasswordRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await Build().ResetPassword(
            new ResetPasswordRequest { Token = "tok", NewPassword = "NewP@ss1!", ConfirmPassword = "NewP@ss1!" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<object?>>(ok.Value);
        Assert.Contains("reset successfully", envelope.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_PropagatesException()
    {
        _passwordResetService.Setup(s => s.ResetPasswordAsync(It.IsAny<ResetPasswordRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Invalid or expired token."));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Build().ResetPassword(
                new ResetPasswordRequest { Token = "bad", NewPassword = "P@ss1!", ConfirmPassword = "P@ss1!" },
                CancellationToken.None));
    }

    // ── VerifyEmail ───────────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyEmail_ValidOtp_ReturnsOk()
    {
        _emailVerificationService.Setup(s => s.VerifyOtpAsync(It.IsAny<VerifyEmailOtpRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await Build().VerifyEmail(
            new VerifyEmailOtpRequest { Email = "user@example.com", Otp = "123456" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<object?>>(ok.Value);
        Assert.Contains("verified", envelope.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyEmail_InvalidOtp_PropagatesException()
    {
        _emailVerificationService.Setup(s => s.VerifyOtpAsync(It.IsAny<VerifyEmailOtpRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Invalid OTP."));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Build().VerifyEmail(
                new VerifyEmailOtpRequest { Email = "user@example.com", Otp = "000000" },
                CancellationToken.None));
    }

    // ── ResendVerification ────────────────────────────────────────────────────

    [Fact]
    public async Task ResendVerification_ValidEmail_ReturnsOk()
    {
        _emailVerificationService.Setup(s => s.SendOtpAsync(It.IsAny<ResendEmailOtpRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await Build().ResendVerification(
            new ResendEmailOtpRequest { Email = "user@example.com" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<object?>>(ok.Value);
        Assert.NotNull(envelope.Message);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AuthResponse MakeAuthResponse(
        string access = "access-token",
        string refresh = "refresh-token") => new()
    {
        AccessToken = access,
        RefreshToken = refresh,
        ExpiresAt = DateTime.UtcNow.AddMinutes(15),
        Email = "user@example.com",
        FullName = "Test User",
        SystemRole = "TenantUser",
    };
}
