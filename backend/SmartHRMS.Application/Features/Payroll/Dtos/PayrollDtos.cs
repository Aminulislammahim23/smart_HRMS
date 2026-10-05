using System.ComponentModel.DataAnnotations;

namespace smartHRMS.Application.Features.Payroll.Dtos;

public class PayrollPeriodDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    /// <summary>Draft, Calculated, PendingApproval, Approved, Paid or Cancelled.</summary>
    public string Status { get; set; } = string.Empty;

    public string? Notes { get; set; }

    /// <summary>Working days in the period (weekends excluded).</summary>
    public int WorkingDays { get; set; }

    public int EmployeeCount { get; set; }

    public decimal GrossTotal { get; set; }

    public decimal DeductionTotal { get; set; }

    public decimal NetTotal { get; set; }

    /// <summary>Records that block submission (e.g. negative net salary).</summary>
    public int NeedsReviewCount { get; set; }

    /// <summary>Payslips issued for this period (all records once approved).</summary>
    public int PayslipCount { get; set; }

    public int PaidPayslipCount { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? CalculatedAt { get; set; }

    public string? CalculatedBy { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public string? SubmittedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public string? ApprovedBy { get; set; }

    /// <summary>When the payroll was finalized (and locked).</summary>
    public DateTime? FinalizedAt { get; set; }

    public string? FinalizedBy { get; set; }

    /// <summary>True once finalized: no payroll value can change any more.</summary>
    public bool IsLocked { get; set; }

    public DateTime? PaidAt { get; set; }

    public string? PaidBy { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancelledBy { get; set; }

    /// <summary>What the signed-in user may do next (computed by the server, which checks again on every action).</summary>
    public PayrollActionsDto Actions { get; set; } = new();
}

public class PayrollActionsDto
{
    public bool CanEdit { get; set; }

    public bool CanDelete { get; set; }

    public bool CanCalculate { get; set; }

    public bool CanEditRecords { get; set; }

    public bool CanSubmit { get; set; }

    public bool CanApprove { get; set; }

    /// <summary>Admin, Approved payroll: finalize and lock it.</summary>
    public bool CanFinalize { get; set; }

    /// <summary>Admin, Finalized payroll with unpaid payslips: create a payment batch (the server checks for an open batch).</summary>
    public bool CanCreatePaymentBatch { get; set; }

    public bool CanCancel { get; set; }

    /// <summary>Approved/Paid payroll that still has records without a payslip (e.g. approved before Day 17).</summary>
    public bool CanGeneratePayslips { get; set; }
}

public class PayrollRecordDto
{
    public Guid Id { get; set; }

    public Guid PayrollPeriodId { get; set; }

    public string PeriodName { get; set; } = string.Empty;

    public DateOnly PeriodStartDate { get; set; }

    public DateOnly PeriodEndDate { get; set; }

    public string PeriodStatus { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;

    public string EmployeeName { get; set; } = string.Empty;

    public string? DepartmentName { get; set; }

    public string? DesignationName { get; set; }

    public decimal BasicSalary { get; set; }

    public decimal HouseRent { get; set; }

    public decimal MedicalAllowance { get; set; }

    public decimal TransportAllowance { get; set; }

    public decimal OtherAllowance { get; set; }

    public decimal OvertimeAmount { get; set; }

    public decimal Bonus { get; set; }

    public decimal GrossSalary { get; set; }

    public decimal Tax { get; set; }

    public decimal LeaveDeduction { get; set; }

    public decimal ProvidentFund { get; set; }

    public decimal AdvanceDeduction { get; set; }

    public decimal LoanDeduction { get; set; }

    public decimal OtherDeduction { get; set; }

    public decimal TotalDeduction { get; set; }

    public decimal NetSalary { get; set; }

    public decimal WorkingDays { get; set; }

    public decimal PresentDays { get; set; }

    public decimal PaidLeaveDays { get; set; }

    public decimal UnpaidLeaveDays { get; set; }

    public decimal AbsentDays { get; set; }

    /// <summary>Calculated, NeedsReview, Approved, Paid or Cancelled.</summary>
    public string Status { get; set; } = string.Empty;

    public string? Remarks { get; set; }

    /// <summary>The issued payslip, once the payroll is approved; null before.</summary>
    public Guid? PayslipId { get; set; }

    public string? PayslipNumber { get; set; }

    public DateTime? PayslipGeneratedAt { get; set; }

    /// <summary>Unpaid or Paid; null while no payslip has been generated.</summary>
    public string? PaymentStatus { get; set; }

    public DateOnly? PaymentDate { get; set; }

    /// <summary>BankTransfer, Cash, MobileBanking, Cheque or Other, once paid.</summary>
    public string? PaymentMethod { get; set; }

    public string? PaymentReference { get; set; }

    /// <summary>True while the period is Draft or Calculated and the user is HR/Admin.</summary>
    public bool CanEdit { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public class CreatePayrollPeriodDto
{
    /// <summary>Optional; defaults to the month name ("September 2026") for a full calendar month, else the date range.</summary>
    [MaxLength(100)]
    public string? Name { get; set; }

    [Required]
    public DateOnly? StartDate { get; set; }

    [Required]
    public DateOnly? EndDate { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class UpdatePayrollPeriodDto : CreatePayrollPeriodDto
{
}

public class PayrollPeriodQueryDto
{
    public int? Year { get; set; }

    public string? Status { get; set; }
}

public class PayrollRecordQueryDto
{
    /// <summary>Matches employee code or name.</summary>
    [MaxLength(100)]
    public string? Search { get; set; }

    public Guid? DepartmentId { get; set; }

    public Guid? DesignationId { get; set; }

    public string? Status { get; set; }
}

/// <summary>
/// The manually entered parts of a payroll record. Basic, allowances, tax and leave deduction come from the salary
/// structure and attendance and are not editable here (change the salary structure and recalculate instead).
/// </summary>
public class UpdatePayrollRecordDto
{
    private const string Max = "9999999999999999.99";

    [Range(typeof(decimal), "0", Max, ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "The OvertimeAmount field must be 0 or more.")]
    public decimal OvertimeAmount { get; set; }

    [Range(typeof(decimal), "0", Max, ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "The Bonus field must be 0 or more.")]
    public decimal Bonus { get; set; }

    [Range(typeof(decimal), "0", Max, ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "The AdvanceDeduction field must be 0 or more.")]
    public decimal AdvanceDeduction { get; set; }

    [Range(typeof(decimal), "0", Max, ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "The LoanDeduction field must be 0 or more.")]
    public decimal LoanDeduction { get; set; }

    [Range(typeof(decimal), "0", Max, ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "The OtherDeduction field must be 0 or more.")]
    public decimal OtherDeduction { get; set; }

    [MaxLength(500)]
    public string? Remarks { get; set; }
}

public class CancelPayrollDto
{
    [MaxLength(500)]
    public string? Reason { get; set; }
}

public class SkippedEmployeeDto
{
    public Guid EmployeeId { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;

    public string EmployeeName { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}

public class PayrollCalculationResultDto
{
    public PayrollPeriodDto Period { get; set; } = new();

    public int Created { get; set; }

    public int Updated { get; set; }

    public int Removed { get; set; }

    /// <summary>Current employees without payroll because data is missing (e.g. no basic salary).</summary>
    public List<SkippedEmployeeDto> Skipped { get; set; } = new();
}

public class PayslipLineDto
{
    public string Label { get; set; } = string.Empty;

    public decimal Amount { get; set; }
}

public class PayslipDto
{
    /// <summary>Null for an HR/Admin preview of payroll that is not approved yet.</summary>
    public Guid? PayslipId { get; set; }

    public string? PayslipNumber { get; set; }

    public Guid RecordId { get; set; }

    public Guid PayrollPeriodId { get; set; }

    public string CompanyName { get; set; } = string.Empty;

    public string? CompanyAddress { get; set; }

    public string Currency { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;

    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>Relative URL of the employee's current photo, or null.</summary>
    public string? EmployeePhotoUrl { get; set; }

    public string? DepartmentName { get; set; }

    public string? DesignationName { get; set; }

    public string PeriodName { get; set; } = string.Empty;

    public DateOnly PeriodStartDate { get; set; }

    public DateOnly PeriodEndDate { get; set; }

    public List<PayslipLineDto> Earnings { get; set; } = new();

    public List<PayslipLineDto> Deductions { get; set; } = new();

    public decimal GrossSalary { get; set; }

    public decimal TotalDeduction { get; set; }

    public decimal NetSalary { get; set; }

    public decimal WorkingDays { get; set; }

    public decimal PresentDays { get; set; }

    public decimal PaidLeaveDays { get; set; }

    public decimal UnpaidLeaveDays { get; set; }

    public decimal AbsentDays { get; set; }

    /// <summary>The payroll period status: Draft, Calculated, PendingApproval, Approved, Paid or Cancelled.</summary>
    public string PayrollStatus { get; set; } = string.Empty;

    /// <summary>Unpaid or Paid (a preview is always Unpaid).</summary>
    public string PaymentStatus { get; set; } = string.Empty;

    /// <summary>Calendar date the salary was paid, once paid.</summary>
    public DateOnly? PaymentDate { get; set; }

    public string? PaymentMethod { get; set; }

    public string? PaymentReference { get; set; }

    /// <summary>When the payslip was issued (at approval); null for a preview.</summary>
    public DateTime? GeneratedAt { get; set; }

    /// <summary>False for a preview of payroll that isn't approved yet (HR/Admin only).</summary>
    public bool IsFinal { get; set; }

    public string? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public string? Remarks { get; set; }
}
