using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smartHRMS.Api.Auth;
using smartHRMS.Application.Common.Models;
using smartHRMS.Application.Features.Payroll;
using smartHRMS.Application.Features.Payroll.Dtos;

namespace smartHRMS.Api.Controllers;

/// <summary>
/// Payroll: periods, calculation, review, approval, payment and payslips. Management endpoints are HR/Admin (approve
/// and mark-paid: Admin); employees reach only their own history and payslips. The service re-checks every rule.
/// Workflow: Draft → Calculated → PendingApproval → Approved → Paid (or Cancelled before approval).
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

    public PayrollController(IPayrollService payrollService, ISalaryStructureService salaryService)
    {
        _payrollService = payrollService;
        _salaryService = salaryService;
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

    /// <summary>Calculated → PendingApproval (HR/Admin). Refused while any record needs review.</summary>
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

    /// <summary>PendingApproval → Approved (Admin). Never by someone whose own salary is in the payroll.</summary>
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

    /// <summary>Approved → Paid (Admin).</summary>
    [Authorize(Policy = Policies.Admin)]
    [HttpPost("{periodId:guid}/mark-paid")]
    [ProducesResponseType(typeof(ApiResponse<PayrollPeriodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<PayrollPeriodDto>>> MarkPaid(Guid periodId, CancellationToken cancellationToken)
    {
        var period = await _payrollService.MarkPaidAsync(periodId, cancellationToken);
        return Ok(ApiResponse<PayrollPeriodDto>.Ok(period, "Payroll marked as paid."));
    }

    /// <summary>Draft, Calculated or PendingApproval → Cancelled (HR/Admin). Records are kept for history.</summary>
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

    /// <summary>Payslip for one record: HR/Admin (also previews), or the employee once Approved or Paid.</summary>
    [HttpGet("payslip/{recordId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PayslipDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PayslipDto>>> GetPayslip(Guid recordId, CancellationToken cancellationToken)
    {
        var payslip = await _payrollService.GetPayslipAsync(recordId, cancellationToken);
        return Ok(ApiResponse<PayslipDto>.Ok(payslip, "Payslip retrieved successfully."));
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
