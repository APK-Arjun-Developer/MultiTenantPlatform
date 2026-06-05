using Infrastructure.MultiTenancy;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Persistence;

/// <summary>
/// Used exclusively by EF Core design-time tools (dotnet ef migrations add/update).
/// Not used at runtime.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../Api"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? "Server=localhost\\SQLEXPRESS;Database=MultiTenantPlatformDb;Trusted_Connection=True;TrustServerCertificate=True;";

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        var tenantService = new NullTenantService();

        return new ApplicationDbContext(optionsBuilder.Options, tenantService);
    }
}

/// <summary>
/// No-op tenant service for design-time context creation.
/// </summary>
file sealed class NullTenantService : Application.Interfaces.Tenant.ICurrentTenantService
{
    public Guid? TenantId => null;
    public Guid? UserId => null;
    public Guid? RoleId => null;
    public IReadOnlyList<Guid> RoleIds => [];
    public string? Email => null;
}
