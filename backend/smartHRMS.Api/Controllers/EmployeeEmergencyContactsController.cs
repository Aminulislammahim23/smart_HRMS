using Microsoft.AspNetCore.Mvc;
using smartHRMS.Application.Common.Models;
using smartHRMS.Application.Features.EmployeeEmergencyContacts;
using smartHRMS.Application.Features.EmployeeEmergencyContacts.Dtos;

namespace smartHRMS.Api.Controllers;

/// <summary>People to contact in an emergency (any number per employee).</summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/emergency-contacts")]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
public class EmployeeEmergencyContactsController : ControllerBase
{
    private readonly IEmployeeEmergencyContactService _service;

    public EmployeeEmergencyContactsController(IEmployeeEmergencyContactService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<EmployeeEmergencyContactDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<EmployeeEmergencyContactDto>>>> GetAll(Guid employeeId, CancellationToken cancellationToken)
    {
        var contacts = await _service.GetAllAsync(employeeId, cancellationToken);
        return Ok(ApiResponse<List<EmployeeEmergencyContactDto>>.Ok(contacts, "Emergency contacts retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeEmergencyContactDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EmployeeEmergencyContactDto>>> GetById(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        var contact = await _service.GetByIdAsync(employeeId, id, cancellationToken);
        return Ok(ApiResponse<EmployeeEmergencyContactDto>.Ok(contact, "Emergency contact retrieved successfully."));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<EmployeeEmergencyContactDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<EmployeeEmergencyContactDto>>> Create(
        Guid employeeId, CreateEmployeeEmergencyContactDto dto, CancellationToken cancellationToken)
    {
        var contact = await _service.CreateAsync(employeeId, dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { employeeId, id = contact.Id }, ApiResponse<EmployeeEmergencyContactDto>.Ok(contact, "Emergency contact created successfully."));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeEmergencyContactDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<EmployeeEmergencyContactDto>>> Update(
        Guid employeeId, Guid id, UpdateEmployeeEmergencyContactDto dto, CancellationToken cancellationToken)
    {
        var contact = await _service.UpdateAsync(employeeId, id, dto, cancellationToken);
        return Ok(ApiResponse<EmployeeEmergencyContactDto>.Ok(contact, "Emergency contact updated successfully."));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(employeeId, id, cancellationToken);
        return Ok(ApiResponse.Ok("Emergency contact deleted successfully."));
    }
}
