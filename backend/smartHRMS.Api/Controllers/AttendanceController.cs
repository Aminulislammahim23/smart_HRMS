using Microsoft.AspNetCore.Mvc;
using smartHRMS.Application.Common.Models;
using smartHRMS.Application.Features.Attendances;
using smartHRMS.Application.Features.Attendances.Dtos;

namespace smartHRMS.Api.Controllers;

/// <summary>
/// Employee attendance: HR-maintained records (CRUD), self check-in/check-out at the server's office-local time, and
/// filtered lists. Dates are "yyyy-MM-dd" and times "HH:mm:ss" in the configured attendance time zone.
/// </summary>
[ApiController]
[Route("api/attendance")]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
public class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _attendanceService;

    public AttendanceController(IAttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
    }

    /// <summary>
    /// Lists attendance, newest date first. Optional filters: employeeId, date, or a startDate/endDate range, and
    /// status (Present, Late, Absent, HalfDay, Leave).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<AttendanceDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<List<AttendanceDto>>>> GetAll([FromQuery] AttendanceListQueryDto query, CancellationToken cancellationToken)
    {
        var records = await _attendanceService.GetAllAsync(query, cancellationToken);
        return Ok(ApiResponse<List<AttendanceDto>>.Ok(records, "Attendance retrieved successfully."));
    }

    /// <summary>Attendance history of one employee, with the same date and status filters.</summary>
    [HttpGet("/api/employees/{employeeId:guid}/attendance")]
    [ProducesResponseType(typeof(ApiResponse<List<AttendanceDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<AttendanceDto>>>> GetByEmployee(
        Guid employeeId, [FromQuery] AttendanceQueryDto query, CancellationToken cancellationToken)
    {
        var records = await _attendanceService.GetByEmployeeAsync(employeeId, query, cancellationToken);
        return Ok(ApiResponse<List<AttendanceDto>>.Ok(records, "Employee attendance retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AttendanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AttendanceDto>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var record = await _attendanceService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<AttendanceDto>.Ok(record, "Attendance retrieved successfully."));
    }

    /// <summary>Creates a manual attendance record (HR). One record per employee and date.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AttendanceDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<AttendanceDto>>> Create(CreateAttendanceDto dto, CancellationToken cancellationToken)
    {
        var record = await _attendanceService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = record.Id }, ApiResponse<AttendanceDto>.Ok(record, "Attendance created successfully."));
    }

    /// <summary>Corrects status, times and remarks (HR). Working minutes are recalculated.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AttendanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AttendanceDto>>> Update(Guid id, UpdateAttendanceDto dto, CancellationToken cancellationToken)
    {
        var record = await _attendanceService.UpdateAsync(id, dto, cancellationToken);
        return Ok(ApiResponse<AttendanceDto>.Ok(record, "Attendance updated successfully."));
    }

    /// <summary>Permanently deletes an attendance record (HR correction).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _attendanceService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok("Attendance deleted successfully."));
    }

    /// <summary>Checks the employee in for today at the current office time (Late after the configured threshold).</summary>
    [HttpPost("check-in")]
    [ProducesResponseType(typeof(ApiResponse<AttendanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<AttendanceDto>>> CheckIn(CheckInDto dto, CancellationToken cancellationToken)
    {
        var record = await _attendanceService.CheckInAsync(dto, cancellationToken);
        return Ok(ApiResponse<AttendanceDto>.Ok(record, "Checked in successfully."));
    }

    /// <summary>Checks the employee out for today and calculates the working minutes.</summary>
    [HttpPost("check-out")]
    [ProducesResponseType(typeof(ApiResponse<AttendanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<AttendanceDto>>> CheckOut(CheckOutDto dto, CancellationToken cancellationToken)
    {
        var record = await _attendanceService.CheckOutAsync(dto, cancellationToken);
        return Ok(ApiResponse<AttendanceDto>.Ok(record, "Checked out successfully."));
    }
}
