using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Features.EmployeeAddresses;
using smartHRMS.Application.Features.EmployeeDocuments;
using smartHRMS.Application.Features.EmployeeEducations;
using smartHRMS.Application.Features.EmployeeEmergencyContacts;
using smartHRMS.Application.Features.EmployeeExperiences;
using smartHRMS.Application.Features.Employees.Dtos;
using smartHRMS.Application.Interfaces;

namespace smartHRMS.Application.Features.Employees;

/// <summary>Read-only view that combines the employee with all of their profile records for the profile page.</summary>
public class EmployeeProfileService : IEmployeeProfileService
{
    private readonly IEmployeeRepository _employeeRepository;

    public EmployeeProfileService(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public async Task<EmployeeProfileDto> GetProfileAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetProfileAsync(employeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee with id '{employeeId}' was not found.");

        var details = employee.PersonalDetails;

        return new EmployeeProfileDto
        {
            EmployeeId = employee.Id,
            EmployeeCode = employee.EmployeeCode,
            FullName = $"{employee.FirstName} {employee.LastName}".Trim(),
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            PhotoUrl = employee.PhotoUrl,
            IsActive = EmployeeStatusRules.CurrentStatuses.Contains(employee.Status),
            PersonalInformation = new EmployeePersonalInformationDto
            {
                DateOfBirth = employee.DateOfBirth,
                Phone = employee.Phone,
                Email = employee.Email,
                HasPersonalDetails = details is not null,
                Gender = details?.Gender?.ToString(),
                MaritalStatus = details?.MaritalStatus?.ToString(),
                BloodGroup = details?.BloodGroup?.ToString(),
                Nationality = details?.Nationality,
                NationalId = Mask(details?.NationalId, visibleChars: 4),
                PassportNo = Mask(details?.PassportNo, visibleChars: 3),
            },
            JobInformation = new EmployeeJobInformationDto
            {
                DepartmentId = employee.DepartmentId,
                DepartmentName = employee.Department?.Name,
                DesignationId = employee.DesignationId,
                DesignationName = employee.Designation?.Name,
                JoiningDate = employee.JoiningDate,
                EmploymentType = employee.EmploymentType.ToString(),
                EmploymentStatus = employee.Status.ToString(),
                BasicSalary = employee.BasicSalary,
            },
            Addresses = employee.Addresses.OrderBy(a => a.AddressType).Select(EmployeeAddressService.ToDto).ToList(),
            EmergencyContacts = employee.EmergencyContacts.OrderBy(c => c.CreatedAt).Select(EmployeeEmergencyContactService.ToDto).ToList(),
            Educations = employee.Educations.OrderByDescending(e => e.PassingYear).Select(EmployeeEducationService.ToDto).ToList(),
            Experiences = employee.Experiences.OrderByDescending(x => x.StartDate).Select(EmployeeExperienceService.ToDto).ToList(),
            Documents = employee.Documents
                .Where(d => d.IsActive)
                .OrderByDescending(d => d.CreatedAt)
                .Select(EmployeeDocumentService.MapToDto)
                .ToList(),
            CreatedAt = employee.CreatedAt,
            UpdatedAt = employee.UpdatedAt,
        };
    }

    /// <summary>Shows only the last <paramref name="visibleChars"/> characters, e.g. "*********1234".</summary>
    internal static string? Mask(string? value, int visibleChars)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= visibleChars
            ? new string('*', value.Length)
            : new string('*', value.Length - visibleChars) + value[^visibleChars..];
    }
}
