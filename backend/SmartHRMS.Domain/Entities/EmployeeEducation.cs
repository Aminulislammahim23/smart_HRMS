using smartHRMS.Domain.Common;

namespace smartHRMS.Domain.Entities;

public class EmployeeEducation : EmployeeOwnedEntity
{
    /// <summary>Free text (SSC, HSC, Bachelor, Master, Diploma, ...), deliberately not a fixed list.</summary>
    public string Degree { get; set; } = string.Empty;

    public string Institution { get; set; } = string.Empty;

    public string? Major { get; set; }

    public string? Result { get; set; }

    public int PassingYear { get; set; }
}
