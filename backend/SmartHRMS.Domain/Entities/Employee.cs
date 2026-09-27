using smartHRMS.Domain.Common;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Domain.Entities;

public class Employee : BaseEntity
{
    public string EmployeeCode { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public DateTime DateOfBirth { get; set; }

    public DateTime JoiningDate { get; set; }

    public Guid DepartmentId { get; set; }

    public Department? Department { get; set; }

    public Guid DesignationId { get; set; }

    public Designation? Designation { get; set; }

    public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;

    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

    /// <summary>Monthly basic salary. Optional; never negative.</summary>
    public decimal? BasicSalary { get; set; }

    public string? PhotoUrl { get; set; }

    public ApplicationUser? ApplicationUser { get; set; }

    public ICollection<EmployeeDocument> Documents { get; set; } = new List<EmployeeDocument>();

    // Profile records (Day 11). Gender, blood group, addresses and emergency contacts live only in these.
    public EmployeePersonalDetails? PersonalDetails { get; set; }

    public ICollection<EmployeeAddress> Addresses { get; set; } = new List<EmployeeAddress>();

    public ICollection<EmployeeEmergencyContact> EmergencyContacts { get; set; } = new List<EmployeeEmergencyContact>();

    public ICollection<EmployeeEducation> Educations { get; set; } = new List<EmployeeEducation>();

    public ICollection<EmployeeExperience> Experiences { get; set; } = new List<EmployeeExperience>();
}
