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

    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

    /// <summary>
    /// Relative URL of the employee's profile photo (e.g. "/uploads/employees/{id}.jpg"), or null
    /// if none has been uploaded yet. Only a reference is stored here — never image bytes, and never
    /// a physical file-system path — so the storage backend (local disk, S3, Blob Storage, ...) can be
    /// swapped without changing this entity.
    /// </summary>
    public string? PhotoUrl { get; set; }

    public ApplicationUser? ApplicationUser { get; set; }
}
