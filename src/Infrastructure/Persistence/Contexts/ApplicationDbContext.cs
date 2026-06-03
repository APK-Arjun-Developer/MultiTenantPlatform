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

    public DbSet<Address> Addresses => Set<Address>();

    public DbSet<SeedHistory> SeedHistory => Set<SeedHistory>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationRole>()
            .HasIndex(r => r.NormalizedName)
            .HasDatabaseName("RoleNameIndex")
            .IsUnique(false);

        ApplyGlobalQueryFilters(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTenantStamps();
        ApplyAuditStamps();

        return await base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyTenantStamps()
    {
        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State == EntityState.Added
                && _currentTenantService.TenantId.HasValue)
            {
                entry.Entity.TenantId = _currentTenantService.TenantId.Value;
            }
        }
    }

    private void ApplyAuditStamps()
    {
        var userId = _currentTenantService.UserId;
        var utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.CreatedAt == default)
                    {
                        entry.Entity.CreatedAt = utcNow;
                    }

                    if (userId.HasValue)
                    {
                        entry.Entity.CreatedBy = userId;
                    }

                    break;

                case EntityState.Modified:
                    if (entry.Entity.DeletedAt.HasValue)
                    {
                        if (!entry.Entity.DeletedBy.HasValue && userId.HasValue)
                        {
                            entry.Entity.DeletedBy = userId;
                        }
                    }
                    else
                    {
                        entry.Entity.UpdatedAt = utcNow;

                        if (userId.HasValue)
                        {
                            entry.Entity.UpdatedBy = userId;
                        }
                    }

                    break;
            }
        }

        foreach (var entry in ChangeTracker.Entries<ApplicationUser>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.CreatedAt == default)
                    {
                        entry.Entity.CreatedAt = utcNow;
                    }

                    break;

                case EntityState.Modified:
                    if (!entry.Entity.DeletedAt.HasValue)
                    {
                        entry.Entity.UpdatedAt = utcNow;
                    }

                    break;
            }
        }
    }
    private void ApplyGlobalQueryFilters(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            if (typeof(ITenantEntity).IsAssignableFrom(clrType)
                && typeof(IAuditableEntity).IsAssignableFrom(clrType))
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(
                        nameof(GetTenantAndSoftDeleteFilter),
                        BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(clrType);

                var filter = method.Invoke(this, Array.Empty<object>());

                builder.Entity(clrType)
                    .HasQueryFilter((LambdaExpression)filter!);
            }
        }

        builder.Entity<ApplicationUser>()
            .HasQueryFilter(u => u.DeletedAt == null);

        builder.Entity<ApplicationRole>()
            .HasQueryFilter(r => r.DeletedAt == null);
    }

    private LambdaExpression GetTenantAndSoftDeleteFilter<TEntity>()
        where TEntity : class, ITenantEntity, IAuditableEntity
    {
        return (Expression<Func<TEntity, bool>>)(e =>
            (!_currentTenantService.TenantId.HasValue
             || e.TenantId == _currentTenantService.TenantId)
            && e.DeletedAt == null);
    }
}