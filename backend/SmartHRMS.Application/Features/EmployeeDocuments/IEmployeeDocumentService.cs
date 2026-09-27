using smartHRMS.Application.Features.EmployeeDocuments.Dtos;

namespace smartHRMS.Application.Features.EmployeeDocuments;

/// <summary>
/// Every operation is scoped to one employee: a document is only found through the employee it belongs to, so
/// requesting employee A's document under employee B's route returns 404.
/// </summary>
public interface IEmployeeDocumentService
{
    Task<EmployeeDocumentDto> UploadAsync(Guid employeeId, UploadEmployeeDocumentDto upload, CancellationToken cancellationToken);

    Task<List<EmployeeDocumentDto>> GetByEmployeeAsync(Guid employeeId, bool includeInactive, CancellationToken cancellationToken);

    /// <summary>Returns the metadata of an active or deactivated document.</summary>
    Task<EmployeeDocumentDto> GetByIdAsync(Guid employeeId, Guid documentId, CancellationToken cancellationToken);

    /// <summary>Opens an active document's file. Deactivated documents can't be downloaded.</summary>
    Task<EmployeeDocumentFileDto> DownloadAsync(Guid employeeId, Guid documentId, CancellationToken cancellationToken);

    Task<EmployeeDocumentDto> UpdateAsync(Guid employeeId, Guid documentId, UpdateEmployeeDocumentDto dto, CancellationToken cancellationToken);

    /// <summary>Soft delete: sets IsActive=false and keeps the row and file. Idempotent.</summary>
    Task DeactivateAsync(Guid employeeId, Guid documentId, CancellationToken cancellationToken);
}
