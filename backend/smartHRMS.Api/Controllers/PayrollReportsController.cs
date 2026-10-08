using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smartHRMS.Api.Auth;
using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Common.Models;
using smartHRMS.Application.Features.Payroll.Dtos;
using smartHRMS.Application.Features.PayrollReports;
using smartHRMS.Application.Features.PayrollReports.Dtos;

namespace smartHRMS.Api.Controllers;

/// <summary>
/// Payroll reporting (Day 19), HR/Admin only: period-level payroll history, monthly summary and salary expense,
/// department, employee, deduction and allowance/bonus reports, and CSV/XLSX export of each. Filtering, grouping and
/// paging happen on the server; the service checks the role again and writes every export to the audit log.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.HrOrAdmin)]
[Route("api/payroll/reports")]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
public class PayrollReportsController : ControllerBase
{
    private readonly IPayrollReportService _reportService;

    public PayrollReportsController(IPayrollReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>Payroll history by period (any status), with totals; filters: search, month, year, status, from, to; paged.</summary>
    [HttpGet("periods")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PayrollPeriodHistoryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<PayrollPeriodHistoryDto>>>> GetPeriods([FromQuery] PayrollPeriodHistoryQueryDto query, CancellationToken cancellationToken)
    {
        var page = await _reportService.GetPeriodHistoryAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<PayrollPeriodHistoryDto>>.Ok(page, "Payroll history retrieved successfully."));
    }

    /// <summary>Monthly payroll summary and salary expense: totals plus one row per payroll period.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<PayrollReportDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<ApiResponse<PayrollReportDto>>> GetSummary([FromQuery] PayrollReportQueryDto query, CancellationToken cancellationToken) =>
        Report(query, PayrollReportGroupBy.Period, forced: true, cancellationToken);

    /// <summary>Department-wise payroll: totals plus one row per department.</summary>
    [HttpGet("departments")]
    [ProducesResponseType(typeof(ApiResponse<PayrollReportDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<ApiResponse<PayrollReportDto>>> GetDepartments([FromQuery] PayrollReportQueryDto query, CancellationToken cancellationToken) =>
        Report(query, PayrollReportGroupBy.Department, forced: true, cancellationToken);

    /// <summary>Deduction summary (tax, provident fund, unpaid leave, advance, loan, other); groupBy=period|department.</summary>
    [HttpGet("deductions")]
    [ProducesResponseType(typeof(ApiResponse<PayrollReportDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<ApiResponse<PayrollReportDto>>> GetDeductions([FromQuery] PayrollReportQueryDto query, CancellationToken cancellationToken) =>
        Report(query, PayrollReportGroupBy.Period, forced: false, cancellationToken);

    /// <summary>Allowance and bonus summary (house rent, medical, transport, other, overtime, bonus); groupBy=period|department.</summary>
    [HttpGet("allowances")]
    [ProducesResponseType(typeof(ApiResponse<PayrollReportDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<ApiResponse<PayrollReportDto>>> GetAllowances([FromQuery] PayrollReportQueryDto query, CancellationToken cancellationToken) =>
        Report(query, PayrollReportGroupBy.Period, forced: false, cancellationToken);

    /// <summary>Employee payroll report: one row per employee and payroll period, paged.</summary>
    [HttpGet("employees")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PayrollRecordDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<PayrollRecordDto>>>> GetEmployees([FromQuery] PayrollReportQueryDto query, CancellationToken cancellationToken)
    {
        var page = await _reportService.GetEmployeeReportAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<PayrollRecordDto>>.Ok(page, "Employee payroll report retrieved successfully."));
    }

    /// <summary>
    /// A report as a file: report = periods, summary, departments, employees, deductions or allowances; format = csv
    /// (default) or xlsx. Takes the same filters as the report itself.
    /// </summary>
    [HttpGet("{report}/export")]
    [Produces("text/csv", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Export(string report, [FromQuery] PayrollReportQueryDto query, [FromQuery] string? format, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PayrollReportKind>(report, ignoreCase: true, out var kind) || !Enum.IsDefined(kind) || int.TryParse(report, out _))
        {
            throw new NotFoundException($"Report '{report}' was not found. Reports: {string.Join(", ", Enum.GetNames<PayrollReportKind>().Select(n => n.ToLowerInvariant()))}.");
        }

        var file = await _reportService.ExportAsync(kind, query, format, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    private async Task<ActionResult<ApiResponse<PayrollReportDto>>> Report(PayrollReportQueryDto query, PayrollReportGroupBy groupBy, bool forced, CancellationToken cancellationToken)
    {
        if (forced)
        {
            // summary is always by period and departments always by department.
            query.GroupBy = groupBy.ToString();
        }

        var report = await _reportService.GetReportAsync(query, groupBy, cancellationToken);
        return Ok(ApiResponse<PayrollReportDto>.Ok(report, "Payroll report retrieved successfully."));
    }
}
