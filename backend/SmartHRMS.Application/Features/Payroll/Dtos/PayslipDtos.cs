using System.ComponentModel.DataAnnotations;

namespace smartHRMS.Application.Features.Payroll.Dtos;

/// <summary>
/// Query-string filters and paging for payroll history and payslip lists. All filters are optional and combine with
/// AND. Employees calling the "my" endpoints can't widen them: the employee always comes from the token.
/// </summary>
public class PayrollHistoryQueryDto
{
    /// <summary>HR/Admin lists only; ignored on the employee's own lists.</summary>
    public Guid? EmployeeId { get; set; }

    public Guid? DepartmentId { get; set; }

    /// <summary>Calendar month (1–12) of the payroll period's start date.</summary>
    public int? Month { get; set; }

    public int? Year { get; set; }

    /// <summary>Payroll status: Draft, Calculated, PendingApproval, Approved, Paid or Cancelled.</summary>
    public string? Status { get; set; }

    /// <summary>Payslip payment status: Unpaid or Paid.</summary>
    public string? PaymentStatus { get; set; }

    /// <summary>Employee code or name contains.</summary>
    [MaxLength(100)]
    public string? Search { get; set; }

    /// <summary>1-based page; default 1.</summary>
    public int? Page { get; set; }

    /// <summary>1–100; default 25.</summary>
    public int? PageSize { get; set; }

    /// <summary>period (default), employee, gross, net or paymentDate.</summary>
    public string? SortBy { get; set; }

    /// <summary>asc or desc (default desc for period/amounts/dates, asc for employee).</summary>
    public string? SortDirection { get; set; }
}

/// <summary>Records a salary payment. The payment date defaults to today's office date and can't be in the future.</summary>
public class RecordPaymentDto
{
    public DateOnly? PaymentDate { get; set; }
}

public class PayslipGenerationResultDto
{
    public Guid PayrollPeriodId { get; set; }

    /// <summary>Payslips created by this call.</summary>
    public int Generated { get; set; }

    /// <summary>Payslips that already existed and were left unchanged.</summary>
    public int AlreadyGenerated { get; set; }
}
