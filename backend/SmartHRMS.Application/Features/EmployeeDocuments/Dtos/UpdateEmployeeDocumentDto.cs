using System.ComponentModel.DataAnnotations;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.EmployeeDocuments.Dtos;

/// <summary>Edits a document's metadata. The file itself can't be replaced: upload a new document instead.</summary>
public class UpdateEmployeeDocumentDto
{
    /// <summary>Optional; omitting it keeps the current type.</summary>
    [EnumDataType(typeof(EmployeeDocumentType))]
    public EmployeeDocumentType? DocumentType { get; set; }

    /// <summary>Full update: omitting it clears the description.</summary>
    [MaxLength(500)]
    public string? Description { get; set; }
}
