using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Common.Models;
using smartHRMS.Application.Features.Employees;
using smartHRMS.Application.Features.Employees.Dtos;

namespace smartHRMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employeeService;

    public EmployeesController(IEmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<EmployeeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<EmployeeDto>>>> GetAll(CancellationToken cancellationToken)
    {
        var employees = await _employeeService.GetAllAsync(cancellationToken);
        return Ok(ApiResponse<List<EmployeeDto>>.Ok(employees, "Employees retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EmployeeDto>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var employee = await _employeeService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<EmployeeDto>.Ok(employee, "Employee retrieved successfully."));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<EmployeeDto>>> Create(CreateEmployeeDto dto, CancellationToken cancellationToken)
    {
        var employee = await _employeeService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(
            nameof(GetById),
            new { id = employee.Id },
            ApiResponse<EmployeeDto>.Ok(employee, "Employee created successfully."));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<EmployeeDto>>> Update(Guid id, UpdateEmployeeDto dto, CancellationToken cancellationToken)
    {
        var employee = await _employeeService.UpdateAsync(id, dto, cancellationToken);
        return Ok(ApiResponse<EmployeeDto>.Ok(employee, "Employee updated successfully."));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await _employeeService.DeactivateAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok("Employee deactivated successfully."));
    }

    /// <summary>Uploads or replaces the employee's profile photo. Accepts .jpg, .jpeg, .png and .webp up to 5 MB.</summary>
    [HttpPut("{id:guid}/photo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EmployeeDto>>> UploadPhoto(Guid id, [FromForm] IFormFile? photo, CancellationToken cancellationToken)
    {
        if (photo is null)
        {
            throw new BadRequestException("A photo file is required.");
        }

        await using var stream = photo.OpenReadStream();

        var employee = await _employeeService.UploadPhotoAsync(
            id,
            new UploadEmployeePhotoDto
            {
                Content = stream,
                FileName = photo.FileName,
                ContentType = photo.ContentType,
                Length = photo.Length,
            },
            cancellationToken);

        return Ok(ApiResponse<EmployeeDto>.Ok(employee, "Employee photo uploaded successfully."));
    }

    /// <summary>Removes the employee's profile photo, if any. Does not delete the employee.</summary>
    [HttpDelete("{id:guid}/photo")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EmployeeDto>>> RemovePhoto(Guid id, CancellationToken cancellationToken)
    {
        var employee = await _employeeService.RemovePhotoAsync(id, cancellationToken);
        return Ok(ApiResponse<EmployeeDto>.Ok(employee, "Employee photo removed successfully."));
    }
}
