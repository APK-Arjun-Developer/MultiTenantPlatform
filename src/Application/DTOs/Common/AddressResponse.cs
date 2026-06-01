namespace Application.DTOs.Common;

public class AddressResponse
{
    public Guid Id { get; set; }

    public string Line1 { get; set; } = default!;

    public string? Line2 { get; set; }

    public string City { get; set; } = default!;

    public string? State { get; set; }

    public string PostalCode { get; set; } = default!;

    public string Country { get; set; } = default!;

    public string FullAddress { get; set; } = default!;
}
