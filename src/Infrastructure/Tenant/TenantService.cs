using Application.DTOs.Tenant;
using Application.Interfaces.Tenant;
using Domain.Entities;
using Infrastructure.Identity.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tenant;

public class TenantService : ITenantService
{
    private readonly ApplicationDbContext _context;

    private readonly UserManager<ApplicationUser> _userManager;

    private readonly RoleManager<ApplicationRole> _roleManager;

    public TenantService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager)
    {
        _context = context;

        _userManager = userManager;

        _roleManager = roleManager;
    }

    public async Task<TenantResponse> CreateAsync(
        CreateTenantRequest request)
    {
        var existingTenant =
            await _context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x =>
                    x.Slug == request.Slug);

        if (existingTenant != null)
        {
            throw new Exception(
                "Tenant slug already exists.");
        }

        var tenant = new Domain.Entities.Tenant
        {
            Id = Guid.NewGuid(),

            TenantId = Guid.Empty,

            Name = request.Name,

            Slug = request.Slug,

            IsActive = true,

            CreatedAt = DateTime.UtcNow
        };

        _context.Tenants.Add(tenant);

        await _context.SaveChangesAsync();

        return new TenantResponse
        {
            Id = tenant.Id,

            Name = tenant.Name,

            Slug = tenant.Slug,

            IsActive = tenant.IsActive
        };
    }

    public async Task<List<TenantResponse>> GetAllAsync()
    {
        return await _context.Tenants
            .IgnoreQueryFilters()
            .Select(x => new TenantResponse
            {
                Id = x.Id,

                Name = x.Name,

                Slug = x.Slug,

                IsActive = x.IsActive
            })
            .ToListAsync();
    }

    public async Task CreateTenantAdminAsync(
        Guid tenantId,
        CreateTenantAdminRequest request)
    {
        var tenant =
            await _context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x =>
                    x.Id == tenantId);

        if (tenant == null)
        {
            throw new Exception("Tenant not found.");
        }

        const string roleName = "TenantAdmin";

        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            await _roleManager.CreateAsync(
                new ApplicationRole
                {
                    Id = Guid.NewGuid(),

                    Name = roleName,

                    NormalizedName =
                        roleName.ToUpper(),

                    TenantId = tenantId,

                    Description =
                        "Tenant Administrator"
                });
        }

        var existingUser =
            await _userManager.FindByEmailAsync(
                request.Email);

        if (existingUser != null)
        {
            throw new Exception(
                "User already exists.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),

            TenantId = tenantId,

            FullName = request.FullName,

            Email = request.Email,

            UserName = request.Email,

            EmailConfirmed = true,

            CreatedAt = DateTime.UtcNow
        };

        var result =
            await _userManager.CreateAsync(
                user,
                request.Password);

        if (!result.Succeeded)
        {
            throw new Exception(
                string.Join(", ",
                    result.Errors
                        .Select(x => x.Description)));
        }

        await _userManager.AddToRoleAsync(
            user,
            roleName);
    }
}