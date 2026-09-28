namespace smartHRMS.Application.Features.EmployeeDocuments.Dtos;

public class EmployeeDocumentDto
{
    public Guid Id { get; set; }

    public Guid EmployeeId { get; set; }

    /// <summary>Nid, Passport, BirthCertificate, EducationalCertificate, ExperienceCertificate, JoiningLetter, Cv, TinCertificate, ContractPaper or Other.</summary>
    public string DocumentType { get; set; } = string.Empty;

    public string DocumentName { get; set; } = string.Empty;

    public DateTime? IssueDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    /// <summary>Sanitized original file name, used as the download name.</summary>
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
