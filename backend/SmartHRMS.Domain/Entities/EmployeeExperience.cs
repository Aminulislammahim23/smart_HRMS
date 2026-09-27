using smartHRMS.Domain.Common;

namespace smartHRMS.Domain.Entities;

/// <summary>Previous (or ongoing, when <see cref="EndDate"/> is null) employment elsewhere.</summary>
public class EmployeeExperience : EmployeeOwnedEntity
{
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>Job title held at that company (free text; not the SmartHRMS Designation entity).</summary>
    public string Designation { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? Responsibilities { get; set; }
}
