using smartHRMS.Application.Features.PayrollReports;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Application.Interfaces;

/// <summary>Read-only payroll aggregation for reports; every query runs in the database (no full-table loads).</summary>
public interface IPayrollReportRepository
{
    /// <summary>Totals per payroll period, including paid net salary.</summary>
    Task<List<PayrollAmountsRow<Guid>>> SumByPeriodAsync(PayrollReportFilter filter, CancellationToken cancellationToken);

    /// <summary>Totals per department name as stored on the payroll record (null: no department).</summary>
    Task<List<PayrollAmountsRow<string?>>> SumByDepartmentAsync(PayrollReportFilter filter, CancellationToken cancellationToken);

    /// <summary>Distinct employees in the filtered records.</summary>
    Task<int> CountEmployeesAsync(PayrollReportFilter filter, CancellationToken cancellationToken);

    /// <summary>Filtered records with period and payslip, newest period first, then employee code.</summary>
    Task<(List<PayrollRecord> Items, int TotalCount)> SearchRecordsAsync(PayrollReportFilter filter, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Filtered payroll periods, newest first.</summary>
    Task<(List<PayrollPeriod> Items, int TotalCount)> SearchPeriodsAsync(PayrollPeriodHistoryFilter filter, int page, int pageSize, CancellationToken cancellationToken);

    Task<List<PayrollPeriod>> GetPeriodsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
}
