using Microsoft.AspNetCore.Mvc;
using smartHRMS.Application.Common.Models;
using smartHRMS.Application.Features.Leaves;
using smartHRMS.Application.Features.Leaves.Dtos;

namespace smartHRMS.Api.Controllers;

/// <summary>
/// Leave requests. Every signed-in user can apply for their own leave; who may see, approve or cancel a request is
/// decided by the server (employee, direct manager, HR, Admin). Dates are "yyyy-MM-dd".
/// </summary>
[ApiController]
[Route("api/leave")]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
public class LeaveController : ControllerBase
{
    private readonly ILeaveService _leaveService;

    public LeaveController(ILeaveService leaveService)
    {
        _leaveService = leaveService;
    }

    /// <summary>Leave types and whether each is paid.</summary>
    [HttpGet("types")]
    [ProducesResponseType(typeof(ApiResponse<List<LeaveTypeDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<LeaveTypeDto>>> GetTypes()
    {
        return Ok(ApiResponse<List<LeaveTypeDto>>.Ok(_leaveService.GetLeaveTypes(), "Leave types retrieved successfully."));
    }

    /// <summary>
    /// Leave requests the caller may see. scope: mine, team (direct reports) or all (HR/Admin). Filters: employeeId,
    /// status, leaveType, startDate/endDate (overlap).
    /// </summary>
    [HttpGet("requests")]
    [ProducesResponseType(typeof(ApiResponse<List<LeaveRequestDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<List<LeaveRequestDto>>>> GetAll([FromQuery] LeaveQueryDto query, CancellationToken cancellationToken)
    {
        var requests = await _leaveService.GetAllAsync(query, cancellationToken);
        return Ok(ApiResponse<List<LeaveRequestDto>>.Ok(requests, "Leave requests retrieved successfully."));
    }

    [HttpGet("requests/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<LeaveRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<LeaveRequestDto>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var request = await _leaveService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<LeaveRequestDto>.Ok(request, "Leave request retrieved successfully."));
    }

    /// <summary>Applies for leave (Pending). employeeId is only honoured for HR/Admin.</summary>
    [HttpPost("requests")]
    [ProducesResponseType(typeof(ApiResponse<LeaveRequestDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<LeaveRequestDto>>> Create(CreateLeaveRequestDto dto, CancellationToken cancellationToken)
    {
        var request = await _leaveService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = request.Id }, ApiResponse<LeaveRequestDto>.Ok(request, "Leave request submitted successfully."));
    }

    /// <summary>Approves a Pending request (direct manager, HR or Admin; never your own).</summary>
    [HttpPost("requests/{id:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse<LeaveRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<LeaveRequestDto>>> Approve(Guid id, ReviewLeaveRequestDto? dto, CancellationToken cancellationToken)
    {
        var request = await _leaveService.ApproveAsync(id, dto ?? new ReviewLeaveRequestDto(), cancellationToken);
        return Ok(ApiResponse<LeaveRequestDto>.Ok(request, "Leave request approved."));
    }

    /// <summary>Rejects a Pending request (direct manager, HR or Admin; never your own).</summary>
    [HttpPost("requests/{id:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse<LeaveRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<LeaveRequestDto>>> Reject(Guid id, ReviewLeaveRequestDto? dto, CancellationToken cancellationToken)
    {
        var request = await _leaveService.RejectAsync(id, dto ?? new ReviewLeaveRequestDto(), cancellationToken);
        return Ok(ApiResponse<LeaveRequestDto>.Ok(request, "Leave request rejected."));
    }

    /// <summary>Cancels a request: the owner while Pending; HR/Admin also when Approved (payroll not yet locked).</summary>
    [HttpPost("requests/{id:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<LeaveRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<LeaveRequestDto>>> Cancel(Guid id, ReviewLeaveRequestDto? dto, CancellationToken cancellationToken)
    {
        var request = await _leaveService.CancelAsync(id, dto ?? new ReviewLeaveRequestDto(), cancellationToken);
        return Ok(ApiResponse<LeaveRequestDto>.Ok(request, "Leave request cancelled."));
    }
}
