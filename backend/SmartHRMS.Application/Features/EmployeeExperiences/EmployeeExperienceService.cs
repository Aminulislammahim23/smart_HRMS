using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Common.Text;
using smartHRMS.Application.Features.EmployeeExperiences.Dtos;
using smartHRMS.Application.Features.Employees;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Application.Features.EmployeeExperiences;

public class EmployeeExperienceService
    : EmployeeOwnedRecordService<EmployeeExperience, CreateEmployeeExperienceDto, EmployeeExperienceDto>, IEmployeeExperienceService
{
    public EmployeeExperienceService(IEmployeeOwnedRepository<EmployeeExperience> repository, IEmployeeRepository employeeRepository)
        : base(repository, employeeRepository)
    {
    }

    protected override string RecordName => "Experience record";

    protected override async Task ApplyAsync(EmployeeExperience record, CreateEmployeeExperienceDto dto, CancellationToken cancellationToken)
    {
        var employee = await EmployeeRepository.GetByIdAsync(record.EmployeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee with id '{record.EmployeeId}' was not found.");

        var today = DateTime.UtcNow.Date;
        var startDate = dto.StartDate.Date;
        var endDate = dto.EndDate?.Date;

        if (startDate > today)
        {
            throw new BadRequestException("The start date cannot be in the future.");
        }

        if (startDate <= employee.DateOfBirth.Date)
        {
            throw new BadRequestException("The start date must be after the employee's date of birth.");
        }

        if (endDate is not null && endDate < startDate)
        {
            throw new BadRequestException("The end date cannot be earlier than the start date.");
        }

        if (endDate is not null && endDate > today)
        {
            throw new BadRequestException("The end date cannot be in the future. Leave it empty for a current job.");
        }

        record.CompanyName = InputText.Required(dto.CompanyName);
        record.Designation = InputText.Required(dto.Designation);
        record.StartDate = startDate;
        record.EndDate = endDate;
        record.Responsibilities = InputText.Optional(dto.Responsibilities);
    }

    /// <summary>Most recent job first.</summary>
    protected override IEnumerable<EmployeeExperience> Order(IEnumerable<EmployeeExperience> records) =>
        records.OrderByDescending(x => x.StartDate);

    protected override EmployeeExperienceDto MapToDto(EmployeeExperience record) => ToDto(record);

    internal static EmployeeExperienceDto ToDto(EmployeeExperience record)
    {
        return new EmployeeExperienceDto
        {
            Id = record.Id,
            EmployeeId = record.EmployeeId,
            CompanyName = record.CompanyName,
            Designation = record.Designation,
            StartDate = record.StartDate,
            EndDate = record.EndDate,
            IsCurrent = record.EndDate is null,
            Responsibilities = record.Responsibilities,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt,
        };
    }
}
