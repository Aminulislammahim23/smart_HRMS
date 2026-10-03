using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smartHRMS.Api.Auth;
using smartHRMS.Application.Features.Employees;
using smartHRMS.Application.Common.Models;
using smartHRMS.Application.Features.EmployeeAddresses;
using smartHRMS.Application.Features.EmployeeAddresses.Dtos;

namespace smartHRMS.Api.Controllers;

/// <summary>Present and permanent addresses (at most one of each per employee).</summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/addresses")]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
public class EmployeeAddressesController : ControllerBase
{
    private readonly IEmployeeAddressService _service;
    private readonly IEmployeeAccess _access;

    public EmployeeAddressesController(IEmployeeAddressService service, IEmployeeAccess access)
    {
        _service = service;
        _access = access;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<EmployeeAddressDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<EmployeeAddressDto>>>> GetAll(Guid employeeId, CancellationToken cancellationToken)
    {
        _access.EnsureCanViewPrivate(employeeId);
        var addresses = await _service.GetAllAsync(employeeId, cancellationToken);
        return Ok(ApiResponse<List<EmployeeAddressDto>>.Ok(addresses, "Addresses retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeAddressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EmployeeAddressDto>>> GetById(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        _access.EnsureCanViewPrivate(employeeId);
        var address = await _service.GetByIdAsync(employeeId, id, cancellationToken);
        return Ok(ApiResponse<EmployeeAddressDto>.Ok(address, "Address retrieved successfully."));
    }

    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<EmployeeAddressDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<EmployeeAddressDto>>> Create(
        Guid employeeId, CreateEmployeeAddressDto dto, CancellationToken cancellationToken)
    {
        var address = await _service.CreateAsync(employeeId, dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { employeeId, id = address.Id }, ApiResponse<EmployeeAddressDto>.Ok(address, "Address created successfully."));
    }

    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeAddressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<EmployeeAddressDto>>> Update(
        Guid employeeId, Guid id, UpdateEmployeeAddressDto dto, CancellationToken cancellationToken)
    {
        var address = await _service.UpdateAsync(employeeId, id, dto, cancellationToken);
        return Ok(ApiResponse<EmployeeAddressDto>.Ok(address, "Address updated successfully."));
    }

    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(employeeId, id, cancellationToken);
        return Ok(ApiResponse.Ok("Address deleted successfully."));
    }
}
