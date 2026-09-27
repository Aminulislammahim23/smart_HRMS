using smartHRMS.Application.Features.EmployeeAddresses.Dtos;
using smartHRMS.Application.Features.EmployeeDocuments.Dtos;
using smartHRMS.Application.Features.EmployeeEducations.Dtos;
using smartHRMS.Application.Features.EmployeeEmergencyContacts.Dtos;
using smartHRMS.Application.Features.EmployeeExperiences.Dtos;

namespace smartHRMS.Application.Features.Employees.Dtos;

/// <summary>Everything the employee profile page shows, grouped into sections, in one response.</summary>
public class EmployeeProfileDto
{
    public Guid EmployeeId { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    /// <summary>Relative URL of the profile photo, or null when no photo has been uploaded.</summary>
    public string? PhotoUrl { get; set; }

    public bool IsActive { get; set; }

    public EmployeePersonalInformationDto PersonalInformation { get; set; } = new();

    public EmployeeJobInformationDto JobInformation { get; set; } = new();

    /// <summary>Present address first, then permanent.</summary>
    public List<EmployeeAddressDto> Addresses { get; set; } = new();

    public List<EmployeeEmergencyContactDto> EmergencyContacts { get; set; } = new();

    /// <summary>Most recent qualification first.</summary>
    public List<EmployeeEducationDto> Educations { get; set; } = new();

    /// <summary>Most recent job first.</summary>
    public List<EmployeeExperienceDto> Experiences { get; set; } = new();

    /// <summary>Active documents only, newest first.</summary>
    public List<EmployeeDocumentDto> Documents { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Contact data from the employee record plus the optional personal-details record. Gender to PassportNo are null
/// when no personal details have been added. National ID and passport number are masked here; the full values are
/// only returned by GET /api/employees/{id}/personal-details.
/// </summary>
public class EmployeePersonalInformationDto
{
    public DateTime DateOfBirth { get; set; }

    public string? Phone { get; set; }

    public string Email { get; set; } = string.Empty;

    public bool HasPersonalDetails { get; set; }

    public string? Gender { get; set; }

    public string? MaritalStatus { get; set; }

    public string? BloodGroup { get; set; }

    public string? Nationality { get; set; }

    /// <summary>Masked, e.g. "*********1234".</summary>
    public string? NationalId { get; set; }

    /// <summary>Masked, e.g. "******567".</summary>
    public string? PassportNo { get; set; }
}

public class EmployeeJobInformationDto
{
    public Guid DepartmentId { get; set; }

    public string? DepartmentName { get; set; }

    public Guid DesignationId { get; set; }

    public string? DesignationName { get; set; }

    public DateTime JoiningDate { get; set; }

    public string EmploymentType { get; set; } = string.Empty;

    /// <summary>The employee's Status: Active, Inactive, Resigned, Terminated or OnLeave.</summary>
    public string EmploymentStatus { get; set; } = string.Empty;

    public decimal? BasicSalary { get; set; }
}
