using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Common.Text;
using smartHRMS.Application.Features.EmployeeEducations.Dtos;
using smartHRMS.Application.Features.Employees;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Application.Features.EmployeeEducations;

public class EmployeeEducationService
    : EmployeeOwnedRecordService<EmployeeEducation, CreateEmployeeEducationDto, EmployeeEducationDto>, IEmployeeEducationService
{
    /// <summary>How far ahead a passing year may be (degree still in progress, expected result).</summary>
    private const int MaxYearsAhead = 5;

    public EmployeeEducationService(IEmployeeOwnedRepository<EmployeeEducation> repository, IEmployeeRepository employeeRepository)
        : base(repository, employeeRepository)
    {
    }

    protected override string RecordName => "Education record";

    protected override async Task ApplyAsync(EmployeeEducation record, CreateEmployeeEducationDto dto, CancellationToken cancellationToken)
    {
        var employee = await EmployeeRepository.GetByIdAsync(record.EmployeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee with id '{record.EmployeeId}' was not found.");

        var passingYear = dto.PassingYear!.Value;
        var latestYear = DateTime.UtcNow.Year + MaxYearsAhead;
        if (passingYear < employee.DateOfBirth.Year || passingYear > latestYear)
        {
            throw new BadRequestException(
                $"The passing year must be between {employee.DateOfBirth.Year} (the employee's birth year) and {latestYear}.");
        }

        var degree = InputText.Required(dto.Degree);
        var institution = InputText.Required(dto.Institution);

        if (await Repository.AnyAsync(
                e => e.EmployeeId == record.EmployeeId && e.Degree == degree && e.Institution == institution
                     && e.PassingYear == passingYear && e.Id != record.Id,
                cancellationToken))
        {
            throw new ConflictException($"This employee already has {degree} from {institution} ({passingYear}).");
        }

        record.Degree = degree;
        record.Institution = institution;
        record.Major = InputText.Optional(dto.Major);
        record.Result = InputText.Optional(dto.Result);
        record.PassingYear = passingYear;
    }

    /// <summary>Most recent qualification first.</summary>
    protected override IEnumerable<EmployeeEducation> Order(IEnumerable<EmployeeEducation> records) =>
        records.OrderByDescending(e => e.PassingYear);

    protected override EmployeeEducationDto MapToDto(EmployeeEducation record) => ToDto(record);

    internal static EmployeeEducationDto ToDto(EmployeeEducation record)
    {
        return new EmployeeEducationDto
        {
            Id = record.Id,
            EmployeeId = record.EmployeeId,
            Degree = record.Degree,
            Institution = record.Institution,
            Major = record.Major,
            Result = record.Result,
            PassingYear = record.PassingYear,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt,
        };
    }
}
