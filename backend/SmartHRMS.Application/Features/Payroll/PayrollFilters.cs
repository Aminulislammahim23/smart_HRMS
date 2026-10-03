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
