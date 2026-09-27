using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Common.Text;
using smartHRMS.Application.Features.EmployeeAddresses.Dtos;
using smartHRMS.Application.Features.Employees;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Application.Features.EmployeeAddresses;

public class EmployeeAddressService
    : EmployeeOwnedRecordService<EmployeeAddress, CreateEmployeeAddressDto, EmployeeAddressDto>, IEmployeeAddressService
{
    public EmployeeAddressService(IEmployeeOwnedRepository<EmployeeAddress> repository, IEmployeeRepository employeeRepository)
        : base(repository, employeeRepository)
    {
    }

    protected override string RecordName => "Address";

    protected override async Task ApplyAsync(EmployeeAddress record, CreateEmployeeAddressDto dto, CancellationToken cancellationToken)
    {
        var addressType = dto.AddressType!.Value;

        // One Present and one Permanent address per employee (a unique index enforces the same rule in the database).
        if (await Repository.AnyAsync(
                a => a.EmployeeId == record.EmployeeId && a.AddressType == addressType && a.Id != record.Id, cancellationToken))
        {
            throw new ConflictException($"This employee already has a {addressType} address. Update it instead of adding another.");
        }

        record.AddressType = addressType;
        record.Address = InputText.Required(dto.Address);
        record.City = InputText.Required(dto.City);
        record.District = InputText.Required(dto.District);
        record.PostalCode = InputText.Optional(dto.PostalCode);
    }

    protected override IEnumerable<EmployeeAddress> Order(IEnumerable<EmployeeAddress> records) =>
        records.OrderBy(a => a.AddressType);

    protected override EmployeeAddressDto MapToDto(EmployeeAddress record) => ToDto(record);

    internal static EmployeeAddressDto ToDto(EmployeeAddress record)
    {
        return new EmployeeAddressDto
        {
            Id = record.Id,
            EmployeeId = record.EmployeeId,
            AddressType = record.AddressType.ToString(),
            Address = record.Address,
            City = record.City,
            District = record.District,
            PostalCode = record.PostalCode,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt,
        };
    }
}
