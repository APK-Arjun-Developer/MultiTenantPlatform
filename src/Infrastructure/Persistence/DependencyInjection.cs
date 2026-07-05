using Infrastructure.Persistence.Contexts;
using Infrastructure.Persistence.Seed;
using Infrastructure.Persistence.Seed.Seeds;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
        });

        services.AddScoped<SeedRunner>();
        services.AddScoped<IDataSeed, PermissionsSeed>();
        services.AddScoped<IDataSeed, SuperAdminSeed>();
        services.AddScoped<IDataSeed, AddListPermissionsSeed>();
        services.AddScoped<IDataSeed, SubscriptionAndAuditPermissionsSeed>();

        return services;
    }
}
