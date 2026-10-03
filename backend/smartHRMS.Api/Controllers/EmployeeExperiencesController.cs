using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smartHRMS.Api.Auth;
using smartHRMS.Application.Features.Employees;
using smartHRMS.Application.Common.Models;
using smartHRMS.Application.Features.EmployeeExperiences;
using smartHRMS.Application.Features.EmployeeExperiences.Dtos;

namespace smartHRMS.Api.Controllers;

/// <summary>Previous or ongoing employment elsewhere (any number per employee).</summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/experiences")]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
public class EmployeeExperiencesController : ControllerBase
{
    private readonly IEmployeeExperienceService _service;
    private readonly IEmployeeAccess _access;

    public EmployeeExperiencesController(IEmployeeExperienceService service, IEmployeeAccess access)
    {
        _service = service;
        _access = access;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<EmployeeExperienceDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<EmployeeExperienceDto>>>> GetAll(Guid employeeId, CancellationToken cancellationToken)
    {
        _access.EnsureCanViewPrivate(employeeId);
        var experiences = await _service.GetAllAsync(employeeId, cancellationToken);
        return Ok(ApiResponse<List<EmployeeExperienceDto>>.Ok(experiences, "Experience records retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeExperienceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EmployeeExperienceDto>>> GetById(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        _access.EnsureCanViewPrivate(employeeId);
        var experience = await _service.GetByIdAsync(employeeId, id, cancellationToken);
        return Ok(ApiResponse<EmployeeExperienceDto>.Ok(experience, "Experience record retrieved successfully."));
    }

    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<EmployeeExperienceDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<EmployeeExperienceDto>>> Create(
        Guid employeeId, CreateEmployeeExperienceDto dto, CancellationToken cancellationToken)
    {
        var experience = await _service.CreateAsync(employeeId, dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { employeeId, id = experience.Id }, ApiResponse<EmployeeExperienceDto>.Ok(experience, "Experience record created successfully."));
    }

    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeExperienceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<EmployeeExperienceDto>>> Update(
        Guid employeeId, Guid id, UpdateEmployeeExperienceDto dto, CancellationToken cancellationToken)
    {
        var experience = await _service.UpdateAsync(employeeId, id, dto, cancellationToken);
        return Ok(ApiResponse<EmployeeExperienceDto>.Ok(experience, "Experience record updated successfully."));
    }

    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(employeeId, id, cancellationToken);
        return Ok(ApiResponse.Ok("Experience record deleted successfully."));
    }
}
