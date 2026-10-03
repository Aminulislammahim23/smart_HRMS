using smartHRMS.Domain.Common;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Domain.Entities;

/// <summary>
/// The issued payslip of one payroll record. It exists only once the payroll is Approved: it is generated in the same
/// transaction as the approval, and its amounts are the record's, which can no longer change (approved payroll is
/// locked). Payment is tracked here, per employee, separately from the approval.
/// </summary>
public class Payslip : BaseEntity
{
    /// <summary>Human-readable, unique: "PS-{period start yyyyMMdd}-{employee code}".</summary>
    public string PayslipNumber { get; set; } = string.Empty;

    public Guid PayrollRecordId { get; set; }

    public PayrollRecord? PayrollRecord { get; set; }

    public Guid PayrollPeriodId { get; set; }

    public Guid EmployeeId { get; set; }

    public DateTime GeneratedAt { get; set; }

    public Guid? GeneratedByUserId { get; set; }

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;

    /// <summary>Calendar date the salary was paid (office date), set when payment is recorded.</summary>
    public DateOnly? PaymentDate { get; set; }

    public DateTime? PaidAt { get; set; }

    public Guid? PaidByUserId { get; set; }
}
