using Domain.Entities;
using Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Application.Interfaces.Tenant;
using Domain.Contracts;
using System.Linq.Expressions;
using System.Reflection;

namespace Infrastructure.Persistence.Contexts;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    private readonly ICurrentTenantService _currentTenantService;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentTenantService currentTenantService)
        : base(options)
    {
        _currentTenantService = currentTenantService;
    }

    public DbSet<Domain.Entities.Tenant> Tenants => Set<Domain.Entities.Tenant>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    public DbSet<FileEntity> Files => Set<FileEntity>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationRole>()
            .HasIndex(r => r.NormalizedName)
            .HasDatabaseName("RoleNameIndex")
            .IsUnique(false);

        ApplyTenantQueryFilters(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries<ITenantEntity>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                if (_currentTenantService.TenantId.HasValue)
                {
                    entry.Entity.TenantId = _currentTenantService.TenantId.Value;
                }
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
    private void ApplyTenantQueryFilters(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity)
                .IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(
                        nameof(GetTenantFilter),
                        BindingFlags.NonPublic |
                        BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType);

                var filter = method.Invoke(this, Array.Empty<object>());

                builder.Entity(entityType.ClrType)
                    .HasQueryFilter((LambdaExpression)filter!);
            }
        }
    }

    private LambdaExpression GetTenantFilter<TEntity>() where TEntity : class, ITenantEntity
    {
        return (Expression<Func<TEntity, bool>>)
            (e =>
                !_currentTenantService.TenantId.HasValue ||
                e.TenantId ==
                _currentTenantService.TenantId);
    }
}