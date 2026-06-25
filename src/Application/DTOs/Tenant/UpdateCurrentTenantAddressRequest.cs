using Application.DTOs.Common;

namespace Application.DTOs.Tenant;

public class UpdateCurrentTenantAddressRequest
{
    public AddressRequest? Address { get; set; }

    public bool ClearAddress { get; set; }
}
