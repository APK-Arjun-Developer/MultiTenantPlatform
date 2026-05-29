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
using Microsoft.AspNetCore.Authorization;

namespace Infrastructure.Identity;

public static class DependencyInjection
{
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
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IUserService, UserService>();

        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));

        var jwtSettings = configuration.GetSection("Jwt").Get<JwtSettings>()!;

        services.AddAuthorization();

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme =
                    JwtBearerDefaults.AuthenticationScheme;

                options.DefaultChallengeScheme =
                    JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters =
                    new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,

                        ValidIssuer = jwtSettings.Issuer,

                        ValidAudience = jwtSettings.Audience,

                        IssuerSigningKey =
                            new SymmetricSecurityKey(
                                Encoding.UTF8.GetBytes(
                                    jwtSettings.Key))
                    };
            });

        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        services.AddScoped<IAuthService, AuthService>();

        services.AddScoped<IRefreshTokenService, RefreshTokenService>();

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

        return services;
    }
}