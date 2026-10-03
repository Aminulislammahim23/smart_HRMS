using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smartHRMS.Api.Auth;
using smartHRMS.Application.Features.Employees;
using smartHRMS.Application.Common.Models;
using smartHRMS.Application.Features.EmployeePersonalDetails;
using smartHRMS.Application.Features.EmployeePersonalDetails.Dtos;

namespace smartHRMS.Api.Controllers;

/// <summary>The employee's single personal-details record (gender, marital status, blood group, nationality, NID, passport).</summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/personal-details")]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
public class EmployeePersonalDetailsController : ControllerBase
{
    private readonly IEmployeePersonalDetailsService _service;
    private readonly IEmployeeAccess _access;

    public EmployeePersonalDetailsController(IEmployeePersonalDetailsService service, IEmployeeAccess access)
    {
        _service = service;
        _access = access;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<EmployeePersonalDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EmployeePersonalDetailsDto>>> Get(Guid employeeId, CancellationToken cancellationToken)
    {
        _access.EnsureCanViewPrivate(employeeId);
        var details = await _service.GetAsync(employeeId, cancellationToken);
        return Ok(ApiResponse<EmployeePersonalDetailsDto>.Ok(details, "Personal details retrieved successfully."));
    }

    /// <summary>Creates the personal details. 409 if they already exist (use PUT).</summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<EmployeePersonalDetailsDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<EmployeePersonalDetailsDto>>> Create(
        Guid employeeId, CreateEmployeePersonalDetailsDto dto, CancellationToken cancellationToken)
    {
        var details = await _service.CreateAsync(employeeId, dto, cancellationToken);
        return CreatedAtAction(nameof(Get), new { employeeId }, ApiResponse<EmployeePersonalDetailsDto>.Ok(details, "Personal details created successfully."));
    }

    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<EmployeePersonalDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<EmployeePersonalDetailsDto>>> Update(
        Guid employeeId, UpdateEmployeePersonalDetailsDto dto, CancellationToken cancellationToken)
    {
        var details = await _service.UpdateAsync(employeeId, dto, cancellationToken);
        return Ok(ApiResponse<EmployeePersonalDetailsDto>.Ok(details, "Personal details updated successfully."));
    }

    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpDelete]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid employeeId, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(employeeId, cancellationToken);
        return Ok(ApiResponse.Ok("Personal details deleted successfully."));
    }
}
