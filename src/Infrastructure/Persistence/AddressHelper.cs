using Application.DTOs.Common;
using Domain.Entities;
using Infrastructure.Identity.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public static class AddressHelper
{
    public static async Task ApplyUserAddressUpdateAsync(
        ApplicationDbContext context,
        ApplicationUser user,
        AddressRequest? address,
        bool clearAddress)
    {
        if (clearAddress)
        {
            var toClear = await context.Addresses
                .FirstOrDefaultAsync(a => a.UserId == user.Id && a.DeletedAt == null);

            if (toClear != null)
            {
                toClear.UserId = null;
                toClear.MarkDeleted();
            }

            return;
        }

        if (address == null)
        {
            return;
        }

        var existing = await context.Addresses
            .FirstOrDefaultAsync(a => a.UserId == user.Id && a.DeletedAt == null);

        if (existing == null)
        {
            context.Addresses.Add(new Address
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TenantId = user.TenantId,
                Line1 = address.Line1,
                Line2 = address.Line2,
                City = address.City,
                State = address.State,
                PostalCode = address.PostalCode,
                Country = address.Country,
                CreatedAt = DateTime.UtcNow,
            });

            return;
        }

        existing.Line1 = address.Line1;
        existing.Line2 = address.Line2;
        existing.City = address.City;
        existing.State = address.State;
        existing.PostalCode = address.PostalCode;
        existing.Country = address.Country;
        existing.UpdatedAt = DateTime.UtcNow;
    }

    public static async Task ApplyTenantAddressUpdateAsync(
        ApplicationDbContext context,
        Domain.Entities.Tenant tenant,
        AddressRequest? address,
        bool clearAddress)
    {
        if (clearAddress)
        {
            var toClear = await context.Addresses
                .FirstOrDefaultAsync(a =>
                    a.OwnerTenantId == tenant.Id && a.DeletedAt == null);

            if (toClear != null)
            {
                toClear.OwnerTenantId = null;
                toClear.MarkDeleted();
            }

            return;
        }

        if (address == null)
        {
            return;
        }

        var existing = await context.Addresses
            .FirstOrDefaultAsync(a =>
                a.OwnerTenantId == tenant.Id && a.DeletedAt == null);

        if (existing == null)
        {
            context.Addresses.Add(new Address
            {
                Id = Guid.NewGuid(),
                OwnerTenantId = tenant.Id,
                TenantId = tenant.Id,
                Line1 = address.Line1,
                Line2 = address.Line2,
                City = address.City,
                State = address.State,
                PostalCode = address.PostalCode,
                Country = address.Country,
                CreatedAt = DateTime.UtcNow,
            });

            return;
        }

        existing.Line1 = address.Line1;
        existing.Line2 = address.Line2;
        existing.City = address.City;
        existing.State = address.State;
        existing.PostalCode = address.PostalCode;
        existing.Country = address.Country;
        existing.UpdatedAt = DateTime.UtcNow;
    }

    public static async Task<Dictionary<Guid, Address>> GetUserAddressesAsync(
        ApplicationDbContext context,
        IReadOnlyList<Guid> userIds,
        bool ignoreTenantFilter)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        var query = context.Addresses.AsQueryable();

        if (ignoreTenantFilter)
        {
            query = query.IgnoreQueryFilters();
        }

        return await query
            .AsNoTracking()
            .Where(a =>
                a.UserId != null
                && userIds.Contains(a.UserId.Value)
                && a.DeletedAt == null)
            .ToDictionaryAsync(a => a.UserId!.Value);
    }

    public static async Task<Dictionary<Guid, Address>> GetTenantAddressesAsync(
        ApplicationDbContext context,
        IReadOnlyList<Guid> tenantIds)
    {
        if (tenantIds.Count == 0)
        {
            return [];
        }

        return await context.Addresses
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a =>
                a.OwnerTenantId != null
                && tenantIds.Contains(a.OwnerTenantId.Value)
                && a.DeletedAt == null)
            .ToDictionaryAsync(a => a.OwnerTenantId!.Value);
    }

    public static async Task<Address?> GetUserAddressAsync(
        ApplicationDbContext context,
        Guid userId,
        bool ignoreTenantFilter)
    {
        var query = context.Addresses.AsQueryable();

        if (ignoreTenantFilter)
        {
            query = query.IgnoreQueryFilters();
        }

        return await query
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId && a.DeletedAt == null);
    }

    public static async Task<Address?> GetTenantAddressAsync(
        ApplicationDbContext context,
        Guid tenantId)
    {
        return await context.Addresses
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(a =>
                a.OwnerTenantId == tenantId && a.DeletedAt == null);
    }
}
