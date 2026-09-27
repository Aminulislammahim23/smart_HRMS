using smartHRMS.Domain.Common;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Domain.Entities;

/// <summary>A structured address. An employee has at most one of each <see cref="AddressType"/>.</summary>
public class EmployeeAddress : EmployeeOwnedEntity
{
    public AddressType AddressType { get; set; }

    public string Address { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string District { get; set; } = string.Empty;

    public string? PostalCode { get; set; }
}
