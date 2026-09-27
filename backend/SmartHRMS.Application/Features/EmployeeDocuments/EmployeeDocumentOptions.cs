namespace smartHRMS.Application.Features.EmployeeDocuments;

/// <summary>
/// Upload rules for employee documents, bound from the "EmployeeDocuments" section of appsettings.json so they can be
/// changed without touching code. Validated once at startup by <see cref="EmployeeDocumentPolicy.EnsureValid"/>.
/// </summary>
public class EmployeeDocumentOptions
{
    public const string SectionName = "EmployeeDocuments";

    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Extensions such as ".pdf". Each must be one <see cref="EmployeeDocumentPolicy"/> knows how to verify.</summary>
    public string[] AllowedExtensions { get; set; } = Array.Empty<string>();
}
