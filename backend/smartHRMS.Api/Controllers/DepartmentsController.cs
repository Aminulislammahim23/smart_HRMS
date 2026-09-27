using Microsoft.AspNetCore.Mvc;
using smartHRMS.Application.Common.Models;
using smartHRMS.Application.Features.Departments;
using smartHRMS.Application.Features.Departments.Dtos;

namespace smartHRMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _departmentService;

    public DepartmentsController(IDepartmentService departmentService)
    {
        _departmentService = departmentService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<DepartmentDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<DepartmentDto>>>> GetAll(CancellationToken cancellationToken)
    {
        var departments = await _departmentService.GetAllAsync(cancellationToken);
        return Ok(ApiResponse<List<DepartmentDto>>.Ok(departments, "Departments retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DepartmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DepartmentDto>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var department = await _departmentService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<DepartmentDto>.Ok(department, "Department retrieved successfully."));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<DepartmentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<DepartmentDto>>> Create(CreateDepartmentDto dto, CancellationToken cancellationToken)
    {
        var department = await _departmentService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(
            nameof(GetById),
            new { id = department.Id },
            ApiResponse<DepartmentDto>.Ok(department, "Department created successfully."));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DepartmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<DepartmentDto>>> Update(Guid id, UpdateDepartmentDto dto, CancellationToken cancellationToken)
    {
        var department = await _departmentService.UpdateAsync(id, dto, cancellationToken);
        return Ok(ApiResponse<DepartmentDto>.Ok(department, "Department updated successfully."));
    }

    /// <summary>Soft delete: deactivates the department (no row is removed). Returns 409 while active or on-leave employees are assigned to it.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<object>>> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await _departmentService.DeactivateAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok("Department deactivated successfully."));
    }
}
