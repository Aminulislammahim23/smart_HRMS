using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.Payroll;

/// <summary>Already-validated record search criteria; null means "no restriction".</summary>
public sealed record PayrollRecordFilter(
    Guid? PeriodId,
    Guid? EmployeeId,
    Guid? DepartmentId,
    Guid? DesignationId,
    PayrollRecordStatus? Status,
    string? Search,
    IReadOnlyCollection<PayrollPeriodStatus>? PeriodStatuses);

public sealed record PayrollTotals(int EmployeeCount, decimal GrossTotal, decimal DeductionTotal, decimal NetTotal, int NeedsReviewCount);

/// <summary>Already-validated payroll history / payslip list criteria; null means "no restriction".</summary>
public sealed record PayrollHistoryFilter(
    Guid? EmployeeId,
    Guid? DepartmentId,
    int? Year,
    int? Month,
    PayrollPeriodStatus? PeriodStatus,
    PaymentStatus? PaymentStatus,
    string? Search,
    bool OnlyWithPayslip);

public enum PayrollHistorySort
{
    Period,
    Employee,
    Gross,
    Net,
    PaymentDate
}

public sealed record PageRequest(int Page, int PageSize, PayrollHistorySort SortBy, bool Descending);

/// <summary>Payslip counts of one payroll period.</summary>
public sealed record PayslipCounts(int Issued, int Paid);
