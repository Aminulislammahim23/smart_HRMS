using System.ComponentModel.DataAnnotations;

namespace smartHRMS.Application.Features.PayrollReports.Dtos;

/// <summary>Report filters (query string, all optional). Every report is filtered and aggregated on the server.</summary>
public class PayrollReportQueryDto
{
    /// <summary>Calendar month (1–12) of the payroll period's start date.</summary>
    public int? Month { get; set; }

    /// <summary>2000–2100, year of the payroll period's start date.</summary>
    public int? Year { get; set; }

    public Guid? PayrollPeriodId { get; set; }

    public Guid? EmployeeId { get; set; }

    /// <summary>Employee's current department (as in the Day 17 payroll history).</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>Employee's current designation.</summary>
    public Guid? DesignationId { get; set; }

    /// <summary>
    /// Payroll status. When omitted, reports include only issued payroll (Approved, Finalized, Paid): draft amounts
    /// are not final and cancelled payroll is never paid.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>Payroll periods overlapping this date range.</summary>
    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    /// <summary>Employee code or name contains (employee report).</summary>
    [MaxLength(100)]
    public string? Search { get; set; }

    /// <summary>Deduction and allowance reports: period (default) or department.</summary>
    public string? GroupBy { get; set; }

    /// <summary>Employee report paging: page ≥ 1 (default 1), pageSize 1–100 (default 25).</summary>
    public int? Page { get; set; }

    public int? PageSize { get; set; }
}

/// <summary>Filters for the payroll period history (all statuses unless one is given).</summary>
public class PayrollPeriodHistoryQueryDto
{
    /// <summary>Period name contains.</summary>
    [MaxLength(100)]
    public string? Search { get; set; }

    public int? Month { get; set; }

    public int? Year { get; set; }

    /// <summary>Draft, Calculated, PendingApproval, Approved, Finalized, Paid or Cancelled.</summary>
    public string? Status { get; set; }

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public int? Page { get; set; }

    public int? PageSize { get; set; }
}

/// <summary>Summed payroll amounts of a group of payroll records, as stored at calculation (never recalculated).</summary>
public class PayrollAmountsDto
{
    /// <summary>Distinct employees.</summary>
    public int EmployeeCount { get; set; }

    /// <summary>Payroll records (one per employee and period).</summary>
    public int RecordCount { get; set; }

    public decimal BasicSalary { get; set; }

    public decimal HouseRent { get; set; }

    public decimal MedicalAllowance { get; set; }

    public decimal TransportAllowance { get; set; }

    public decimal OtherAllowance { get; set; }

    /// <summary>House rent + medical + transport + other allowance.</summary>
    public decimal TotalAllowances { get; set; }

    public decimal Overtime { get; set; }

    public decimal Bonus { get; set; }

    public decimal GrossSalary { get; set; }

    public decimal Tax { get; set; }

    public decimal ProvidentFund { get; set; }

    public decimal LeaveDeduction { get; set; }

    public decimal AdvanceDeduction { get; set; }

    public decimal LoanDeduction { get; set; }

    public decimal OtherDeduction { get; set; }

    /// <summary>Every deduction except tax.</summary>
    public decimal OtherDeductions { get; set; }

    public decimal TotalDeduction { get; set; }

    public decimal NetSalary { get; set; }

    /// <summary>Net salary of payslips already paid, and what is still to pay.</summary>
    public decimal PaidNetSalary { get; set; }

    public decimal UnpaidNetSalary { get; set; }
}

/// <summary>One row of a grouped report: a payroll period or a department.</summary>
public class PayrollReportRowDto : PayrollAmountsDto
{
    /// <summary>Period name, or department name ("Unassigned" for none).</summary>
    public string Label { get; set; } = string.Empty;

    public Guid? PayrollPeriodId { get; set; }

    public DateOnly? PeriodStartDate { get; set; }

    public DateOnly? PeriodEndDate { get; set; }

    public string? PeriodStatus { get; set; }
}

public class PayrollReportDto
{
    /// <summary>period or department.</summary>
    public string GroupBy { get; set; } = string.Empty;

    /// <summary>Payroll statuses included.</summary>
    public List<string> Statuses { get; set; } = new();

    public PayrollAmountsDto Totals { get; set; } = new();

    public List<PayrollReportRowDto> Rows { get; set; } = new();
}

/// <summary>One payroll period with its totals (payroll history, period level).</summary>
public class PayrollPeriodHistoryDto : PayrollAmountsDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public int Month { get; set; }

    public int Year { get; set; }

    public string Status { get; set; } = string.Empty;

    public bool IsLocked { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>When the payroll was (last) calculated: the "processed" date.</summary>
    public DateTime? CalculatedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? FinalizedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime? CancelledAt { get; set; }
}
