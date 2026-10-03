using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smartHRMS.Api.Auth;
using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Common.Models;
using smartHRMS.Application.Features.EmployeeDocuments;
using smartHRMS.Application.Features.EmployeeDocuments.Dtos;
using smartHRMS.Application.Features.Employees;

namespace smartHRMS.Api.Controllers;

/// <summary>
/// Documents are always addressed through their employee, so a document id only works under the employee it
/// belongs to; anything else is a 404.
/// </summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/documents")]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
public class EmployeeDocumentsController : ControllerBase
{
    private readonly IEmployeeDocumentService _documentService;
    private readonly IEmployeeAccess _access;

    public EmployeeDocumentsController(IEmployeeDocumentService documentService, IEmployeeAccess access)
    {
        _documentService = documentService;
        _access = access;
    }

    /// <summary>Lists the employee's documents, newest first. Deactivated documents are included only with includeInactive=true.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<EmployeeDocumentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<EmployeeDocumentDto>>>> GetAll(
        Guid employeeId, [FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        _access.EnsureCanViewPrivate(employeeId);
        var documents = await _documentService.GetByEmployeeAsync(employeeId, includeInactive, cancellationToken);
        return Ok(ApiResponse<List<EmployeeDocumentDto>>.Ok(documents, "Employee documents retrieved successfully."));
    }

    [HttpGet("{documentId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EmployeeDocumentDto>>> GetById(
        Guid employeeId, Guid documentId, CancellationToken cancellationToken)
    {
        _access.EnsureCanViewPrivate(employeeId);
        var document = await _documentService.GetByIdAsync(employeeId, documentId, cancellationToken);
        return Ok(ApiResponse<EmployeeDocumentDto>.Ok(document, "Employee document retrieved successfully."));
    }

    /// <summary>
    /// Uploads a document (multipart/form-data): file, documentType (Nid, Passport, BirthCertificate,
    /// EducationalCertificate, ExperienceCertificate, JoiningLetter, Cv, TinCertificate, ContractPaper, Other),
    /// documentName, and optional issueDate, expiryDate (yyyy-MM-dd) and description. Allowed types and size come
    /// from configuration.
    /// </summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDocumentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EmployeeDocumentDto>>> Upload(
        Guid employeeId,
        [FromForm] IFormFile? file,
        [FromForm] string? documentType,
        [FromForm] string? documentName,
        [FromForm] DateTime? issueDate,
        [FromForm] DateTime? expiryDate,
        [FromForm] string? description,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            throw new BadRequestException("A document file is required.");
        }

        await using var stream = file.OpenReadStream();

        var document = await _documentService.UploadAsync(
            employeeId,
            new UploadEmployeeDocumentDto
            {
                Content = stream,
                FileName = file.FileName,
                ContentType = file.ContentType,
                Length = file.Length,
                DocumentType = documentType,
                DocumentName = documentName,
                IssueDate = issueDate,
                ExpiryDate = expiryDate,
                Description = description,
            },
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { employeeId, documentId = document.Id },
            ApiResponse<EmployeeDocumentDto>.Ok(document, "Employee document uploaded successfully."));
    }

    /// <summary>Streams an active document as a download (Content-Disposition: attachment).</summary>
    [HttpGet("{documentId:guid}/download")]
    [Produces("application/octet-stream")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound, "application/json")]
    public async Task<IActionResult> Download(Guid employeeId, Guid documentId, CancellationToken cancellationToken)
    {
        _access.EnsureCanViewPrivate(employeeId);
        var file = await _documentService.DownloadAsync(employeeId, documentId, cancellationToken);

        // Never let a browser guess a different (e.g. executable/HTML) type than the one validated at upload.
        Response.Headers.XContentTypeOptions = "nosniff";

        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Updates an active document's type and/or description. The file itself can't be changed.</summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpPut("{documentId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EmployeeDocumentDto>>> Update(
        Guid employeeId, Guid documentId, UpdateEmployeeDocumentDto dto, CancellationToken cancellationToken)
    {
        var document = await _documentService.UpdateAsync(employeeId, documentId, dto, cancellationToken);
        return Ok(ApiResponse<EmployeeDocumentDto>.Ok(document, "Employee document updated successfully."));
    }

    /// <summary>Soft delete: deactivates the document. The record and file are kept for HR history.</summary>
    [Authorize(Policy = Policies.HrOrAdmin)]
    [HttpDelete("{documentId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Deactivate(Guid employeeId, Guid documentId, CancellationToken cancellationToken)
    {
        await _documentService.DeactivateAsync(employeeId, documentId, cancellationToken);
        return Ok(ApiResponse.Ok("Employee document deactivated successfully."));
    }
}
