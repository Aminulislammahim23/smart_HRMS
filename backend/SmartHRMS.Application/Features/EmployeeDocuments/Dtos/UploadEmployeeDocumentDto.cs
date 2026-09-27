namespace smartHRMS.Application.Features.EmployeeDocuments.Dtos;

/// <summary>
/// An uploaded document's content plus the client-supplied form values, built from primitives (not IFormFile) so the
/// Application layer never depends on ASP.NET Core. Everything here is untrusted until the service validates it.
/// </summary>
public class UploadEmployeeDocumentDto
{
    public required Stream Content { get; set; }

    /// <summary>Original client file name: its extension is validated and a sanitized copy is kept as the download name.</summary>
    public required string FileName { get; set; }

    public long Length { get; set; }

    /// <summary>Raw form value, parsed by the service so the error can list the accepted types.</summary>
    public string? DocumentType { get; set; }

    public string? Description { get; set; }
}
