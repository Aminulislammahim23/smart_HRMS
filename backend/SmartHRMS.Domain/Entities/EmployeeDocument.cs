using smartHRMS.Domain.Common;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Domain.Entities;

/// <summary>
/// A file (NID, CV, contract, ...) attached to an employee. Only metadata lives in the database; the file itself
/// is in private storage. <see cref="BaseEntity.CreatedAt"/> is the upload time.
/// </summary>
public class EmployeeDocument : BaseEntity
{
    public Guid EmployeeId { get; set; }

    public Employee? Employee { get; set; }

    public EmployeeDocumentType DocumentType { get; set; }

    /// <summary>Sanitized original file name, used only as the download name. Never used to locate the file.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Server-generated storage key relative to the private document root. Never sent to clients.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Derived by the server from the validated extension, not taken from the client.</summary>
    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public string? Description { get; set; }

    /// <summary>Soft delete: false hides the document; the row and file are kept for HR history.</summary>
    public bool IsActive { get; set; } = true;
}
