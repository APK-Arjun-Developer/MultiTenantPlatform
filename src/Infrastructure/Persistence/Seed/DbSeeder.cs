using Infrastructure.Persistence.Contexts;

namespace Infrastructure.Persistence.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(
        ApplicationDbContext context)
    {
        if (!context.Tenants.Any())
        {
            context.Tenants.Add(new Domain.Entities.Tenant
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.Empty,
                Name = "Default Tenant",
                Slug = "default",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        }
    }
}