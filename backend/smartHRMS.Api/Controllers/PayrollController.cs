using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smartHRMS.Api.Auth;
using smartHRMS.Application.Common.Models;
using smartHRMS.Application.Features.Payroll;
using smartHRMS.Application.Features.Payroll.Dtos;

namespace smartHRMS.Api.Controllers;

/// <summary>
/// Payroll: periods, calculation, review, approval, payment and payslips. Management endpoints are HR/Admin (approve
/// and finalize: Admin; payment is made through /api/payments); employees reach only their own history and payslips. The service re-checks every rule.
/// Workflow: Draft → Calculated → PendingApproval → Approved → Finalized → Paid (or Cancelled before approval).
/// Reports and report exports are in <see cref="PayrollReportsController"/>.
/// </summary>
[ApiController]
[Route("api/payroll")]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
public class PayrollController : ControllerBase
{
    private readonly IPayrollService _payrollService;
    private readonly ISalaryStructureService _salaryService;
    private readonly IPayslipService _payslipService;

    public PayrollController(IPayrollService payrollService, ISalaryStructureService salaryService, IPayslipService payslipService)
    {
        _payrollService = payrollService;
        _salaryService = salaryService;
        _payslipService = payslipService;
    }

    // ---- periods ----

    /// <summary>Payroll periods with totals, newest first. Filters: year, status.</summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpGet("periods")]
    [ProducesResponseType(typeof(ApiResponse<List<PayrollPeriodDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<List<PayrollPeriodDto>>>> GetPeriods([FromQuery] PayrollPeriodQueryDto query, CancellationToken cancellationToken)
    {
        var periods = await _payrollService.GetPeriodsAsync(query, cancellationToken);
        return Ok(ApiResponse<List<PayrollPeriodDto>>.Ok(periods, "Payroll periods retrieved successfully."));
    }

    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpGet("periods/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PayrollPeriodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PayrollPeriodDto>>> GetPeriod(Guid id, CancellationToken cancellationToken)
    {
        var period = await _payrollService.GetPeriodAsync(id, cancellationToken);
        return Ok(ApiResponse<PayrollPeriodDto>.Ok(period, "Payroll period retrieved successfully."));
    }

    /// <summary>Creates a Draft period. Start before end, at most 31 days, no overlap with another period.</summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpPost("periods")]
    [ProducesResponseType(typeof(ApiResponse<PayrollPeriodDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<PayrollPeriodDto>>> CreatePeriod(CreatePayrollPeriodDto dto, CancellationToken cancellationToken)
    {
        var period = await _payrollService.CreatePeriodAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetPeriod), new { id = period.Id }, ApiResponse<PayrollPeriodDto>.Ok(period, "Payroll period created successfully."));
    }

    /// <summary>Edits a Draft period's name, dates and notes.</summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpPut("periods/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PayrollPeriodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<PayrollPeriodDto>>> UpdatePeriod(Guid id, UpdatePayrollPeriodDto dto, CancellationToken cancellationToken)
    {
        var period = await _payrollService.UpdatePeriodAsync(id, dto, cancellationToken);
        return Ok(ApiResponse<PayrollPeriodDto>.Ok(period, "Payroll period updated successfully."));
    }

    /// <summary>Deletes a Draft period (anything later is cancelled instead, to keep the history).</summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpDelete("periods/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<object>>> DeletePeriod(Guid id, CancellationToken cancellationToken)
    {
        await _payrollService.DeletePeriodAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok("Payroll period deleted successfully."));
    }

    // ---- calculation and records ----

    /// <summary>
    /// Calculates (or recalculates) one record per current employee from salary structure, attendance and approved
    /// leave, in one transaction. Allowed while Draft or Calculated; manual amounts are kept.
    /// </summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpPost("calculate/{periodId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PayrollCalculationResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<PayrollCalculationResultDto>>> Calculate(Guid periodId, CancellationToken cancellationToken)
    {
        var result = await _payrollService.CalculateAsync(periodId, cancellationToken);
        return Ok(ApiResponse<PayrollCalculationResultDto>.Ok(result, "Payroll calculated successfully."));
    }

    /// <summary>The period's records. Filters: search (code/name), departmentId, designationId, status.</summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpGet("periods/{periodId:guid}/records")]
    [ProducesResponseType(typeof(ApiResponse<List<PayrollRecordDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<PayrollRecordDto>>>> GetRecords(Guid periodId, [FromQuery] PayrollRecordQueryDto query, CancellationToken cancellationToken)
    {
        var records = await _payrollService.GetRecordsAsync(periodId, query, cancellationToken);
        return Ok(ApiResponse<List<PayrollRecordDto>>.Ok(records, "Payroll records retrieved successfully."));
    }

    /// <summary>One record: HR/Admin, or the employee once the payroll is Approved or Paid.</summary>
    [HttpGet("records/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PayrollRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PayrollRecordDto>>> GetRecord(Guid id, CancellationToken cancellationToken)
    {
        var record = await _payrollService.GetRecordAsync(id, cancellationToken);
        return Ok(ApiResponse<PayrollRecordDto>.Ok(record, "Payroll record retrieved successfully."));
    }

    /// <summary>Edits overtime, bonus, advance, loan and other deductions while the payroll is Draft or Calculated.</summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpPut("records/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PayrollRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<PayrollRecordDto>>> UpdateRecord(Guid id, UpdatePayrollRecordDto dto, CancellationToken cancellationToken)
    {
        var record = await _payrollService.UpdateRecordAsync(id, dto, cancellationToken);
        return Ok(ApiResponse<PayrollRecordDto>.Ok(record, "Payroll record updated successfully."));
    }

    // ---- workflow ----

    /// <summary>Calculated â†’ PendingApproval (HR/Admin). Refused while any record needs review.</summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpPost("{periodId:guid}/submit")]
    [ProducesResponseType(typeof(ApiResponse<PayrollPeriodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<PayrollPeriodDto>>> Submit(Guid periodId, CancellationToken cancellationToken)
    {
        var period = await _payrollService.SubmitAsync(periodId, cancellationToken);
        return Ok(ApiResponse<PayrollPeriodDto>.Ok(period, "Payroll submitted for approval."));
    }

    /// <summary>PendingApproval â†’ Approved (Admin). Never by someone whose own salary is in the payroll.</summary>
    [Authorize(Policy = Policies.Admin)]
    [HttpPost("{periodId:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse<PayrollPeriodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<PayrollPeriodDto>>> Approve(Guid periodId, CancellationToken cancellationToken)
    {
        var period = await _payrollService.ApproveAsync(periodId, cancellationToken);
        return Ok(ApiResponse<PayrollPeriodDto>.Ok(period, "Payroll approved."));
    }

    /// <summary>
    /// Approved â†’ Finalized (Admin): validates the payroll, locks it and makes it payable through payment batches
    /// (/api/payments). Salary is paid only through payment batches.
    /// </summary>
    [Authorize(Policy = Policies.Admin)]
    [HttpPost("{periodId:guid}/finalize")]
    [ProducesResponseType(typeof(ApiResponse<PayrollPeriodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<PayrollPeriodDto>>> Finalize(Guid periodId, CancellationToken cancellationToken)
    {
        var period = await _payrollService.FinalizeAsync(periodId, cancellationToken);
        return Ok(ApiResponse<PayrollPeriodDto>.Ok(period, "Payroll finalized and locked."));
    }

    /// <summary>Draft, Calculated or PendingApproval â†’ Cancelled (HR/Admin). Records are kept for history.</summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpPost("{periodId:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<PayrollPeriodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<PayrollPeriodDto>>> Cancel(Guid periodId, CancelPayrollDto? dto, CancellationToken cancellationToken)
    {
        var period = await _payrollService.CancelAsync(periodId, dto ?? new CancelPayrollDto(), cancellationToken);
        return Ok(ApiResponse<PayrollPeriodDto>.Ok(period, "Payroll cancelled."));
    }

    // ---- employee self-service ----

    /// <summary>An employee's payroll history: their own (Approved/Paid only), or anyone's for HR/Admin.</summary>
    [HttpGet("employee/{employeeId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<List<PayrollRecordDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<PayrollRecordDto>>>> GetEmployeeHistory(Guid employeeId, CancellationToken cancellationToken)
    {
        var records = await _payrollService.GetEmployeeHistoryAsync(employeeId, cancellationToken);
        return Ok(ApiResponse<List<PayrollRecordDto>>.Ok(records, "Payroll history retrieved successfully."));
    }

    /// <summary>Payslip for one payroll record: HR/Admin (also previews), or the employee once the payslip is issued.</summary>
    [HttpGet("payslip/{recordId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PayslipDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PayslipDto>>> GetPayslip(Guid recordId, CancellationToken cancellationToken)
    {
        var payslip = await _payslipService.GetByRecordAsync(recordId, cancellationToken);
        return Ok(ApiResponse<PayslipDto>.Ok(payslip, "Payslip retrieved successfully."));
    }

    // ---- payroll history and payslips (Day 17) ----

    /// <summary>
    /// Payroll history: every record with its payslip and payment (HR/Admin). Filters: employeeId, departmentId, month,
    /// year, status, paymentStatus, search; paging: page, pageSize (â‰¤ 100); sortBy (period, employee, gross, net,
    /// paymentDate), sortDirection (asc, desc).
    /// </summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpGet("history")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PayrollRecordDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<PayrollRecordDto>>>> GetHistory([FromQuery] PayrollHistoryQueryDto query, CancellationToken cancellationToken)
    {
        var page = await _payslipService.GetHistoryAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<PayrollRecordDto>>.Ok(page, "Payroll history retrieved successfully."));
    }

    /// <summary>Issued payslips (HR/Admin), with the same filters, paging and sorting as the history.</summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpGet("payslips")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PayrollRecordDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<PayrollRecordDto>>>> GetPayslips([FromQuery] PayrollHistoryQueryDto query, CancellationToken cancellationToken)
    {
        var page = await _payslipService.GetPayslipsAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<PayrollRecordDto>>.Ok(page, "Payslips retrieved successfully."));
    }

    /// <summary>Issued payslips as CSV or XLSX (HR/Admin), with the list filters. <c>format</c>: csv (default) or xlsx.</summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpGet("payslips/export")]
    [Produces("text/csv", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportPayslips([FromQuery] PayrollHistoryQueryDto query, [FromQuery] string? format, CancellationToken cancellationToken)
    {
        var file = await _payslipService.ExportAsync(query, mine: false, format, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>The signed-in employee's own payslips as CSV or XLSX (employee from the token).</summary>
    [HttpGet("me/payslips/export")]
    [Produces("text/csv", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportMyPayslips([FromQuery] PayrollHistoryQueryDto query, [FromQuery] string? format, CancellationToken cancellationToken)
    {
        var file = await _payslipService.ExportAsync(query, mine: true, format, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>One payslip by its id: HR/Admin, or the employee it belongs to (404 for anyone else).</summary>
    [HttpGet("payslips/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PayslipDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PayslipDto>>> GetPayslipById(Guid id, CancellationToken cancellationToken)
    {
        var payslip = await _payslipService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<PayslipDto>.Ok(payslip, "Payslip retrieved successfully."));
    }

    /// <summary>Issues the missing payslips of an Approved or Paid payroll (HR/Admin); refused for unapproved payroll.</summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpPost("{periodId:guid}/payslips")]
    [ProducesResponseType(typeof(ApiResponse<PayslipGenerationResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<PayslipGenerationResultDto>>> GeneratePayslips(Guid periodId, CancellationToken cancellationToken)
    {
        var result = await _payslipService.GenerateAsync(periodId, cancellationToken);
        return Ok(ApiResponse<PayslipGenerationResultDto>.Ok(result, $"{result.Generated} payslips generated."));
    }


    /// <summary>One employee's payslips: HR/Admin, or the employee themself (403 for anyone else).</summary>
    [HttpGet("employee/{employeeId:guid}/payslips")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PayrollRecordDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PagedResult<PayrollRecordDto>>>> GetEmployeePayslips(Guid employeeId, [FromQuery] PayrollHistoryQueryDto query, CancellationToken cancellationToken)
    {
        var page = await _payslipService.GetEmployeePayslipsAsync(employeeId, query, cancellationToken);
        return Ok(ApiResponse<PagedResult<PayrollRecordDto>>.Ok(page, "Payslips retrieved successfully."));
    }

    /// <summary>The signed-in employee's payslips. The employee comes from the token; employeeId in the query is ignored.</summary>
    [HttpGet("me/payslips")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PayrollRecordDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<PayrollRecordDto>>>> GetMyPayslips([FromQuery] PayrollHistoryQueryDto query, CancellationToken cancellationToken)
    {
        var page = await _payslipService.GetMyPayslipsAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<PayrollRecordDto>>.Ok(page, "Payslips retrieved successfully."));
    }

    /// <summary>The signed-in employee's latest payslip (404 while none has been issued).</summary>
    [HttpGet("me/payslips/current")]
    [ProducesResponseType(typeof(ApiResponse<PayslipDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PayslipDto>>> GetMyCurrentPayslip(CancellationToken cancellationToken)
    {
        var payslip = await _payslipService.GetMyCurrentPayslipAsync(cancellationToken);
        return Ok(ApiResponse<PayslipDto>.Ok(payslip, "Payslip retrieved successfully."));
    }

    /// <summary>The signed-in employee's salary payments (paid payslips), newest payment first.</summary>
    [HttpGet("me/payments")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PayrollRecordDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<PayrollRecordDto>>>> GetMyPayments([FromQuery] PayrollHistoryQueryDto query, CancellationToken cancellationToken)
    {
        var page = await _payslipService.GetMyPaymentsAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<PayrollRecordDto>>.Ok(page, "Payment history retrieved successfully."));
    }

    // ---- salary structures ----

    /// <summary>Basic salary, allowances and tax of every employee (HR/Admin).</summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpGet("salary-structures")]
    [ProducesResponseType(typeof(ApiResponse<List<SalaryStructureDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SalaryStructureDto>>>> GetSalaryStructures(CancellationToken cancellationToken)
    {
        var structures = await _salaryService.GetAllAsync(cancellationToken);
        return Ok(ApiResponse<List<SalaryStructureDto>>.Ok(structures, "Salary structures retrieved successfully."));
    }

    /// <summary>One employee's salary structure (HR/Admin, or the employee).</summary>
    [HttpGet("/api/employees/{employeeId:guid}/salary-structure")]
    [ProducesResponseType(typeof(ApiResponse<SalaryStructureDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SalaryStructureDto>>> GetSalaryStructure(Guid employeeId, CancellationToken cancellationToken)
    {
        var structure = await _salaryService.GetAsync(employeeId, cancellationToken);
        return Ok(ApiResponse<SalaryStructureDto>.Ok(structure, "Salary structure retrieved successfully."));
    }

    /// <summary>Replaces basic salary, allowances and monthly tax (HR/Admin). Applies from the next calculation.</summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpPut("/api/employees/{employeeId:guid}/salary-structure")]
    [ProducesResponseType(typeof(ApiResponse<SalaryStructureDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SalaryStructureDto>>> UpdateSalaryStructure(Guid employeeId, UpdateSalaryStructureDto dto, CancellationToken cancellationToken)
    {
        var structure = await _salaryService.UpdateAsync(employeeId, dto, cancellationToken);
        return Ok(ApiResponse<SalaryStructureDto>.Ok(structure, "Salary structure updated successfully."));
    }
}
