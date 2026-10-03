using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smartHRMS.Api.Auth;
using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Common.Models;
using smartHRMS.Application.Common.Security;
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
    private readonly IEmployeeProfileService _profileService;
    private readonly IEmployeeAccess _access;
    private readonly IEmployeeManagerService _managerService;
    private readonly ICurrentUser _currentUser;

    public EmployeesController(
        IEmployeeService employeeService,
        IEmployeeProfileService profileService,
        IEmployeeAccess access,
        IEmployeeManagerService managerService,
        ICurrentUser currentUser)
    {
        _employeeService = employeeService;
        _profileService = profileService;
        _access = access;
        _managerService = managerService;
        _currentUser = currentUser;
    }

    /// <summary>All employees (HR/Admin).</summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<EmployeeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<EmployeeDto>>>> GetAll(CancellationToken cancellationToken)
    {
        var employees = await _employeeService.GetAllAsync(cancellationToken);
        return Ok(ApiResponse<List<EmployeeDto>>.Ok(employees, "Employees retrieved successfully."));
    }

    /// <summary>One employee: HR/Admin, the employee, or their direct manager (who does not see the salary).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EmployeeDto>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        await _access.EnsureCanViewAsync(id, cancellationToken);
        var employee = await _employeeService.GetByIdAsync(id, cancellationToken);
        if (!_currentUser.IsHrOrAdmin() && !_currentUser.IsSelf(id))
        {
            employee.BasicSalary = null;
        }

        return Ok(ApiResponse<EmployeeDto>.Ok(employee, "Employee retrieved successfully."));
    }

    /// <summary>Full employee profile: personal and job information, department, designation, photo and active documents.</summary>
    [HttpGet("{id:guid}/profile")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EmployeeProfileDto>>> GetProfile(Guid id, CancellationToken cancellationToken)
    {
        _access.EnsureCanViewPrivate(id);
        var profile = await _profileService.GetProfileAsync(id, cancellationToken);
        return Ok(ApiResponse<EmployeeProfileDto>.Ok(profile, "Employee profile retrieved successfully."));
    }

    /// <summary>Sets (managerId) or clears (null) the employee's line manager, who then reviews their leave.</summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpPut("{id:guid}/manager")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EmployeeDto>>> AssignManager(Guid id, AssignManagerDto dto, CancellationToken cancellationToken)
    {
        await _managerService.AssignAsync(id, dto, cancellationToken);
        var employee = await _employeeService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<EmployeeDto>.Ok(employee, "Manager updated successfully."));
    }

    [Authorize(Policy = Policies.HrOrAdmin)]
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

    [Authorize(Policy = Policies.HrOrAdmin)]
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

    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await _employeeService.DeactivateAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok("Employee deactivated successfully."));
    }

    /// <summary>Uploads or replaces the employee's profile photo. Accepts .jpg, .jpeg, .png and .webp up to 5 MB.</summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
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
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpDelete("{id:guid}/photo")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EmployeeDto>>> RemovePhoto(Guid id, CancellationToken cancellationToken)
    {
        var employee = await _employeeService.RemovePhotoAsync(id, cancellationToken);
        return Ok(ApiResponse<EmployeeDto>.Ok(employee, "Employee photo removed successfully."));
    }
}
