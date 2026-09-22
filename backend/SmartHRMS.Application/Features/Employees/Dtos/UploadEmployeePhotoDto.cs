namespace smartHRMS.Application.Features.Employees.Dtos;

/// <summary>
/// Carries an uploaded photo's raw content plus the client-supplied metadata down to the Application layer.
/// Deliberately built from primitives (not <c>IFormFile</c>) so the Application layer never depends on ASP.NET Core.
/// </summary>
public class UploadEmployeePhotoDto
{
    public required Stream Content { get; set; }

    /// <summary>Original client-supplied file name. Never trusted for storage — used only to read the extension.</summary>
    public required string FileName { get; set; }

    /// <summary>Client-supplied Content-Type header. Never trusted alone — cross-checked against the file's signature.</summary>
    public string? ContentType { get; set; }

    public long Length { get; set; }
}
