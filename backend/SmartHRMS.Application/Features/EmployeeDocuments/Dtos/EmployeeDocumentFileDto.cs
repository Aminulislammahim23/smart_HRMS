namespace smartHRMS.Application.Features.EmployeeDocuments.Dtos;

/// <summary>A document file ready to stream to the client. The caller owns (and must dispose) <see cref="Content"/>.</summary>
public class EmployeeDocumentFileDto
{
    public required Stream Content { get; set; }

    public required string FileName { get; set; }

    public required string ContentType { get; set; }
}
