using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Application.Interfaces.Auth;
using Infrastructure.Identity.Services;
using Infrastructure.Identity.Entities;
using Application.Interfaces.Authentication;
using Infrastructure.Authentication.JWT;
using Infrastructure.Authentication.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Application.Interfaces.ActivityLogs;
using Application.Interfaces.Authorization;
using Application.Interfaces.Caching;
using Application.Interfaces.Files;
using Application.Interfaces.Permissions;
using Application.Interfaces.Reports;
using Application.Interfaces.Products;
using Application.Interfaces.Roles;
using Application.Interfaces.Tenant;
using Application.Interfaces.Users;
using Infrastructure.ActivityLogs;
using Infrastructure.Caching;
using Infrastructure.Files;
using Infrastructure.Permissions;
using Infrastructure.Reports;
using Infrastructure.Products;
using Infrastructure.Authorization;
using Infrastructure.MultiTenancy;
using Infrastructure.Roles;
using Infrastructure.Tenant;
using Infrastructure.Users;
using Application.Interfaces.AccountSetup;
using Application.Interfaces.Email;
using Application.Interfaces.Invitations;
using Application.Interfaces.Onboarding;
using Infrastructure.AccountSetup;
using Infrastructure.Email;
using Infrastructure.Invitations;
using Infrastructure.Jobs;
using Infrastructure.Onboarding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Identity;

public static class DependencyInjection
{
    public static IServiceCollection AddEmailInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.Configure<SmtpSettings>(configuration.GetSection(SmtpSettings.SectionName));

        if (environment.IsDevelopment())
        {
            services.AddScoped<IEmailService, StubEmailService>();
        }
        else
        {
            var host = configuration[$"{SmtpSettings.SectionName}:Host"];

            if (string.IsNullOrWhiteSpace(host))
            {
                throw new InvalidOperationException(
                    $"Email:Host is required in non-Development environments. " +
                    $"Set it via environment variable Email__Host or a secrets manager.");
            }

            services.AddScoped<IEmailService, SmtpEmailService>();
        }

        services.AddSingleton<SmtpHealthCheck>();

        return services;
    }

    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;

                options.User.RequireUniqueEmail = true;

                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IIdentityRoleService, IdentityRoleService>();

        var jwtSettings = configuration.GetSection("Jwt").Get<JwtSettings>()
            ?? throw new InvalidOperationException("Jwt configuration section is missing.");

        if (string.IsNullOrWhiteSpace(jwtSettings.Key) || jwtSettings.Key.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Key must be configured and at least 32 characters long. " +
                "Set it via environment variable, user secrets, or a secrets manager.");
        }

        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));

        services.AddAuthorization();

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSettings.Key)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                };

                // Cookie-first token extraction: read HttpOnly cookie, fall back to
                // Authorization: Bearer header if cookie is absent.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        if (ctx.Request.Cookies.TryGetValue("access_token", out var cookieToken))
                            ctx.Token = cookieToken;

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<IEmailVerificationService, EmailVerificationService>();

        services.AddHttpContextAccessor();
        services.AddMemoryCache();

        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));
        services.AddSingleton<IAppCache, AppCache>();
        services.AddScoped<IRolePermissionLookup, RolePermissionLookup>();

        services.AddScoped<ICurrentTenantService, CurrentTenantService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IActivityLogService, ActivityLogService>();

        services.Configure<FileStorageSettings>(
            configuration.GetSection(FileStorageSettings.SectionName));
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IFileService, FileService>();

        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IProductService, ProductService>();

        services.AddScoped<ICurrentUserPermissionService, CurrentUserPermissionService>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddHostedService<ExpiredTokenCleanupJob>();

        services.AddScoped<IOnboardingService, OnboardingService>();
        services.AddScoped<IAccountSetupService, AccountSetupService>();
        services.AddScoped<IInvitationService, InvitationService>();

        return services;
    }
}
