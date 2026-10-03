using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smartHRMS.Api.Auth;
using smartHRMS.Application.Common.Models;
using smartHRMS.Application.Features.Designations;
using smartHRMS.Application.Features.Designations.Dtos;

namespace smartHRMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
public class DesignationsController : ControllerBase
{
    private readonly IDesignationService _designationService;

    public DesignationsController(IDesignationService designationService)
    {
        _designationService = designationService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<DesignationDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<DesignationDto>>>> GetAll(CancellationToken cancellationToken)
    {
        var designations = await _designationService.GetAllAsync(cancellationToken);
        return Ok(ApiResponse<List<DesignationDto>>.Ok(designations, "Designations retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DesignationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DesignationDto>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var designation = await _designationService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<DesignationDto>.Ok(designation, "Designation retrieved successfully."));
    }

    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<DesignationDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<DesignationDto>>> Create(CreateDesignationDto dto, CancellationToken cancellationToken)
    {
        var designation = await _designationService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(
            nameof(GetById),
            new { id = designation.Id },
            ApiResponse<DesignationDto>.Ok(designation, "Designation created successfully."));
    }

    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DesignationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<DesignationDto>>> Update(Guid id, UpdateDesignationDto dto, CancellationToken cancellationToken)
    {
        var designation = await _designationService.UpdateAsync(id, dto, cancellationToken);
        return Ok(ApiResponse<DesignationDto>.Ok(designation, "Designation updated successfully."));
    }

    /// <summary>Soft delete: deactivates the designation (no row is removed). Returns 409 while active or on-leave employees are assigned to it.</summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<object>>> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await _designationService.DeactivateAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok("Designation deactivated successfully."));
    }
}
