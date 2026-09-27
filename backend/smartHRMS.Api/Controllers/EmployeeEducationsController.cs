using Microsoft.AspNetCore.Mvc;
using smartHRMS.Application.Common.Models;
using smartHRMS.Application.Features.EmployeeEducations;
using smartHRMS.Application.Features.EmployeeEducations.Dtos;

namespace smartHRMS.Api.Controllers;

/// <summary>Educational qualifications (any number per employee).</summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/educations")]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
public class EmployeeEducationsController : ControllerBase
{
    private readonly IEmployeeEducationService _service;

    public EmployeeEducationsController(IEmployeeEducationService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<EmployeeEducationDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<EmployeeEducationDto>>>> GetAll(Guid employeeId, CancellationToken cancellationToken)
    {
        var educations = await _service.GetAllAsync(employeeId, cancellationToken);
        return Ok(ApiResponse<List<EmployeeEducationDto>>.Ok(educations, "Education records retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeEducationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EmployeeEducationDto>>> GetById(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        var education = await _service.GetByIdAsync(employeeId, id, cancellationToken);
        return Ok(ApiResponse<EmployeeEducationDto>.Ok(education, "Education record retrieved successfully."));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<EmployeeEducationDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<EmployeeEducationDto>>> Create(
        Guid employeeId, CreateEmployeeEducationDto dto, CancellationToken cancellationToken)
    {
        var education = await _service.CreateAsync(employeeId, dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { employeeId, id = education.Id }, ApiResponse<EmployeeEducationDto>.Ok(education, "Education record created successfully."));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeEducationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<EmployeeEducationDto>>> Update(
        Guid employeeId, Guid id, UpdateEmployeeEducationDto dto, CancellationToken cancellationToken)
    {
        var education = await _service.UpdateAsync(employeeId, id, dto, cancellationToken);
        return Ok(ApiResponse<EmployeeEducationDto>.Ok(education, "Education record updated successfully."));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(employeeId, id, cancellationToken);
        return Ok(ApiResponse.Ok("Education record deleted successfully."));
    }
}
