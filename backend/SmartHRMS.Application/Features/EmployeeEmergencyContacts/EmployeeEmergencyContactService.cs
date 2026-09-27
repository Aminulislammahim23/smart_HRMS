using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Common.Text;
using smartHRMS.Application.Features.EmployeeEmergencyContacts.Dtos;
using smartHRMS.Application.Features.Employees;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Application.Features.EmployeeEmergencyContacts;

public class EmployeeEmergencyContactService
    : EmployeeOwnedRecordService<EmployeeEmergencyContact, CreateEmployeeEmergencyContactDto, EmployeeEmergencyContactDto>,
      IEmployeeEmergencyContactService
{
    public EmployeeEmergencyContactService(
        IEmployeeOwnedRepository<EmployeeEmergencyContact> repository, IEmployeeRepository employeeRepository)
        : base(repository, employeeRepository)
    {
    }

    protected override string RecordName => "Emergency contact";

    protected override async Task ApplyAsync(
        EmployeeEmergencyContact record, CreateEmployeeEmergencyContactDto dto, CancellationToken cancellationToken)
    {
        var phone = InputText.Required(dto.Phone);

        // The same person (phone number) listed twice for one employee is a duplicate entry.
        if (await Repository.AnyAsync(
                c => c.EmployeeId == record.EmployeeId && c.Phone == phone && c.Id != record.Id, cancellationToken))
        {
            throw new ConflictException($"An emergency contact with phone '{phone}' already exists for this employee.");
        }

        record.Name = InputText.Required(dto.Name);
        record.Relationship = InputText.Required(dto.Relationship);
        record.Phone = phone;
        record.Email = dto.Email;
        record.Address = InputText.Optional(dto.Address);
    }

    protected override EmployeeEmergencyContactDto MapToDto(EmployeeEmergencyContact record) => ToDto(record);

    internal static EmployeeEmergencyContactDto ToDto(EmployeeEmergencyContact record)
    {
        return new EmployeeEmergencyContactDto
        {
            Id = record.Id,
            EmployeeId = record.EmployeeId,
            Name = record.Name,
            Relationship = record.Relationship,
            Phone = record.Phone,
            Email = record.Email,
            Address = record.Address,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt,
        };
    }
}
