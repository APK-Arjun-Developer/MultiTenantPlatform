using Application.DTOs.Common;
using Domain.Entities;

namespace Application.Common;

public static class AddressFormatter
{
    public static string BuildFullAddress(
        string line1,
        string? line2,
        string city,
        string? state,
        string postalCode,
        string country)
    {
        return string.Join(
            ", ",
            new[] { line1, line2, city, state, postalCode, country }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    public static AddressResponse? ToResponse(Address? address)
    {
        if (address == null)
        {
            return null;
        }

        return new AddressResponse
        {
            Id = address.Id,
            Line1 = address.Line1,
            Line2 = address.Line2,
            City = address.City,
            State = address.State,
            PostalCode = address.PostalCode,
            Country = address.Country,
            FullAddress = BuildFullAddress(
                address.Line1,
                address.Line2,
                address.City,
                address.State,
                address.PostalCode,
                address.Country),
        };
    }
}
