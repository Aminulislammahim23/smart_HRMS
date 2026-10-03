namespace smartHRMS.Application.Features.Payroll;

/// <summary>Payroll settings, bound from the "Payroll" section.</summary>
public class PayrollOptions
{
    public const string SectionName = "Payroll";

    /// <summary>Printed on payslips.</summary>
    public string CompanyName { get; set; } = "SmartHRMS";

    public string? CompanyAddress { get; set; }

    /// <summary>Currency code shown on payslips (amounts are plain decimals).</summary>
    public string Currency { get; set; } = "BDT";

    /// <summary>
    /// When true, days with an explicit Absent attendance record (and the missing half of a HalfDay) are deducted like
    /// unpaid leave. Off by default: the attendance module has no rule that an absence is unpaid, and days without
    /// any attendance record are never deducted.
    /// </summary>
    public bool DeductRecordedAbsences { get; set; }

    /// <summary>Longest allowed payroll period in calendar days.</summary>
    public int MaxPeriodDays { get; set; } = 31;

    public static void EnsureValid(PayrollOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.CompanyName))
        {
            throw new InvalidOperationException($"{SectionName}:CompanyName is required.");
        }

        if (options.MaxPeriodDays is < 1 or > 366)
        {
            throw new InvalidOperationException($"{SectionName}:MaxPeriodDays must be between 1 and 366.");
        }
    }
}
