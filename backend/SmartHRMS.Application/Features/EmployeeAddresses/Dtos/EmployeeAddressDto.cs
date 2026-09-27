namespace smartHRMS.Application.Features.EmployeeAddresses.Dtos;

public class EmployeeAddressDto
{
    public Guid Id { get; set; }

    public Guid EmployeeId { get; set; }

    /// <summary>Present or Permanent.</summary>
    public string AddressType { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string District { get; set; } = string.Empty;

    public string? PostalCode { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
