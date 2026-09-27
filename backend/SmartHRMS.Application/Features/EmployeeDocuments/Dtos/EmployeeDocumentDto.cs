namespace smartHRMS.Application.Features.EmployeeDocuments.Dtos;

public class EmployeeDocumentDto
{
    public Guid Id { get; set; }

    public Guid EmployeeId { get; set; }

    /// <summary>Nid, Passport, EducationalCertificate, Cv, JoiningLetter, ContractPaper or Other.</summary>
    public string DocumentType { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime UploadedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    /// <summary>API route that streams the file. The storage location itself is never exposed.</summary>
    public string DownloadUrl { get; set; } = string.Empty;
}
