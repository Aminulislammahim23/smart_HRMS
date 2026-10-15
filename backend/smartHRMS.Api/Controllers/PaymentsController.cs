using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smartHRMS.Api.Auth;
using smartHRMS.Application.Common.Models;
using smartHRMS.Application.Features.Payments;
using smartHRMS.Application.Features.Payments.Dtos;

namespace smartHRMS.Api.Controllers;

/// <summary>
/// Salary payment for Finalized payroll: payment batches, per-employee payments (transactions) and their status
/// history. Viewing: HR and Admin; every change: Admin. Employees see only their own payments (GET /api/payments/me).
/// Payment states: Pending → Processing → Paid / Failed; Failed → Processing (retry); Pending → Cancelled.
/// Day 21: disbursement dashboard (summary), payment export, evidence-based confirmation and Paid corrections.
/// </summary>
[ApiController]
[Route("api/payments")]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    // ---- batches ----

    /// <summary>Creates a batch for a Finalized payroll with every unpaid employee; totals are calculated by the server.</summary>
    [Authorize(Policy = Policies.Admin)]
    [HttpPost("batches")]
    [ProducesResponseType(typeof(ApiResponse<PaymentBatchDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<PaymentBatchDto>>> CreateBatch(CreatePaymentBatchDto dto, CancellationToken cancellationToken)
    {
        var batch = await _paymentService.CreateBatchAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetBatch), new { id = batch.Id }, ApiResponse<PaymentBatchDto>.Ok(batch, "Payment batch created."));
    }

    /// <summary>Payment batches, newest first. Filters: payrollPeriodId, status; paging: page, pageSize.</summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpGet("batches")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PaymentBatchDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<PaymentBatchDto>>>> GetBatches([FromQuery] PaymentBatchQueryDto query, CancellationToken cancellationToken)
    {
        var page = await _paymentService.GetBatchesAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<PaymentBatchDto>>.Ok(page, "Payment batches retrieved successfully."));
    }

    /// <summary>One batch with totals and per-status counts (its payments: GET /api/payments?batchId=...).</summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpGet("batches/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PaymentBatchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PaymentBatchDto>>> GetBatch(Guid id, CancellationToken cancellationToken)
    {
        var batch = await _paymentService.GetBatchAsync(id, cancellationToken);
        return Ok(ApiResponse<PaymentBatchDto>.Ok(batch, "Payment batch retrieved successfully."));
    }

    /// <summary>Starts processing: every Pending payment of the batch → Processing.</summary>
    [Authorize(Policy = Policies.Admin)]
    [HttpPost("batches/{id:guid}/process")]
    [ProducesResponseType(typeof(ApiResponse<PaymentBatchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<PaymentBatchDto>>> ProcessBatch(Guid id, CancellationToken cancellationToken)
    {
        var batch = await _paymentService.ProcessBatchAsync(id, cancellationToken);
        return Ok(ApiResponse<PaymentBatchDto>.Ok(batch, "Payment processing started."));
    }

    /// <summary>Cancels a batch whose payments are all still Pending (reason required). The payroll can then get a new batch.</summary>
    [Authorize(Policy = Policies.Admin)]
    [HttpPost("batches/{id:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<PaymentBatchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<PaymentBatchDto>>> CancelBatch(Guid id, PaymentReasonDto dto, CancellationToken cancellationToken)
    {
        var batch = await _paymentService.CancelBatchAsync(id, dto, cancellationToken);
        return Ok(ApiResponse<PaymentBatchDto>.Ok(batch, "Payment batch cancelled."));
    }

    // ---- Day 21: disbursement dashboard and export ----

    /// <summary>
    /// Disbursement dashboard (HR/Admin): totals and one row per eligible (Finalized or Paid) payroll: payable, paid,
    /// outstanding, not initiated, pending, processing and failed counts and amounts, all computed by the server.
    /// Filters: payrollPeriodId, year; paging of the payroll rows: page, pageSize.
    /// </summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<DisbursementSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<DisbursementSummaryDto>>> GetSummary([FromQuery] DisbursementSummaryQueryDto query, CancellationToken cancellationToken)
    {
        var summary = await _paymentService.GetSummaryAsync(query, cancellationToken);
        return Ok(ApiResponse<DisbursementSummaryDto>.Ok(summary, "Disbursement summary retrieved successfully."));
    }

    /// <summary>Payments as CSV or XLSX (HR/Admin) with the payment-list filters. <c>format</c>: csv (default) or xlsx.</summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpGet("export")]
    [Produces("text/csv", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Export([FromQuery] PaymentQueryDto query, [FromQuery] string? format, CancellationToken cancellationToken)
    {
        var file = await _paymentService.ExportAsync(query, format, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    // ---- payments ----

    /// <summary>
    /// Payment history (HR/Admin). Filters: batchId, employeeId, payrollPeriodId, status, paymentMethod, from/to
    /// (payment date), search; paging: page, pageSize.
    /// </summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PaymentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<PaymentDto>>>> GetPayments([FromQuery] PaymentQueryDto query, CancellationToken cancellationToken)
    {
        var page = await _paymentService.GetPaymentsAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<PaymentDto>>.Ok(page, "Payments retrieved successfully."));
    }

    /// <summary>The signed-in employee's own payments (the employee comes from the token).</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PaymentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<PaymentDto>>>> GetMyPayments([FromQuery] PaymentQueryDto query, CancellationToken cancellationToken)
    {
        var page = await _paymentService.GetMyPaymentsAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<PaymentDto>>.Ok(page, "Payments retrieved successfully."));
    }

    /// <summary>One payment with its status history: HR/Admin, or the employee it pays (404 for anyone else).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> GetPayment(Guid id, CancellationToken cancellationToken)
    {
        var payment = await _paymentService.GetPaymentAsync(id, cancellationToken);
        return Ok(ApiResponse<PaymentDto>.Ok(payment, "Payment retrieved successfully."));
    }

    /// <summary>
    /// Processing → Paid, with evidence: transactionReference is required for BankTransfer, MobileBanking and Cheque;
    /// Cash/Other need a reference or remarks. paymentDate defaults to today (not future, not before the period).
    /// </summary>
    [Authorize(Policy = Policies.Admin)]
    [HttpPost("{id:guid}/paid")]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> MarkPaid(Guid id, MarkPaymentPaidDto? dto, CancellationToken cancellationToken)
    {
        var payment = await _paymentService.MarkPaidAsync(id, dto ?? new MarkPaymentPaidDto(), cancellationToken);
        return Ok(ApiResponse<PaymentDto>.Ok(payment, "Payment marked as paid."));
    }

    /// <summary>Processing → Failed (reason required; the attempt's transactionReference is optional and kept in history).</summary>
    [Authorize(Policy = Policies.Admin)]
    [HttpPost("{id:guid}/failed")]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> MarkFailed(Guid id, MarkPaymentFailedDto dto, CancellationToken cancellationToken)
    {
        var payment = await _paymentService.MarkFailedAsync(id, dto, cancellationToken);
        return Ok(ApiResponse<PaymentDto>.Ok(payment, "Payment marked as failed."));
    }

    /// <summary>Failed → Processing (optional reason; optional paymentMethod for the new attempt). Earlier attempts stay in history.</summary>
    [Authorize(Policy = Policies.Admin)]
    [HttpPost("{id:guid}/retry")]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> Retry(Guid id, RetryPaymentDto? dto, CancellationToken cancellationToken)
    {
        var payment = await _paymentService.RetryAsync(id, dto ?? new RetryPaymentDto(), cancellationToken);
        return Ok(ApiResponse<PaymentDto>.Ok(payment, "Payment retried."));
    }

    /// <summary>
    /// Day 21: corrects a Paid payment's transactionReference and/or paymentDate (reason required). The payment stays
    /// Paid and its amount can't change; the old values stay in the status history and the audit log.
    /// </summary>
    [Authorize(Policy = Policies.Admin)]
    [HttpPost("{id:guid}/correct")]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> Correct(Guid id, CorrectPaymentDto dto, CancellationToken cancellationToken)
    {
        var payment = await _paymentService.CorrectAsync(id, dto, cancellationToken);
        return Ok(ApiResponse<PaymentDto>.Ok(payment, "Payment corrected."));
    }

    /// <summary>Pending → Cancelled (reason required). The employee can be paid in a later batch.</summary>
    [Authorize(Policy = Policies.Admin)]
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> Cancel(Guid id, PaymentReasonDto dto, CancellationToken cancellationToken)
    {
        var payment = await _paymentService.CancelAsync(id, dto, cancellationToken);
        return Ok(ApiResponse<PaymentDto>.Ok(payment, "Payment cancelled."));
    }
}
