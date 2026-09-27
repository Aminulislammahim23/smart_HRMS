using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Features.EmployeeDocuments.Dtos;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.EmployeeDocuments;

public class EmployeeDocumentService : IEmployeeDocumentService
{
    private const int MaxFileNameLength = 255;

    private readonly IEmployeeDocumentRepository _documentRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDocumentStorageService _documentStorage;
    private readonly EmployeeDocumentOptions _options;

    public EmployeeDocumentService(
        IEmployeeDocumentRepository documentRepository,
        IEmployeeRepository employeeRepository,
        IDocumentStorageService documentStorage,
        EmployeeDocumentOptions options)
    {
        _documentRepository = documentRepository;
        _employeeRepository = employeeRepository;
        _documentStorage = documentStorage;
        _options = options;
    }

    public async Task<EmployeeDocumentDto> UploadAsync(Guid employeeId, UploadEmployeeDocumentDto upload, CancellationToken cancellationToken)
    {
        await EnsureEmployeeExistsAsync(employeeId, cancellationToken);

        var documentType = ParseDocumentType(upload.DocumentType);
        var description = NormalizeOptional(upload.Description);
        if (description?.Length > 500)
        {
            throw new BadRequestException("The description must be 500 characters or fewer.");
        }

        if (upload.Length <= 0)
        {
            throw new BadRequestException("The uploaded file is empty.");
        }

        if (upload.Length > _options.MaxFileSizeBytes)
        {
            throw new BadRequestException($"The file exceeds the maximum allowed size of {FormatSize(_options.MaxFileSizeBytes)}.");
        }

        var extension = Path.GetExtension(upload.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !_options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new BadRequestException($"Only these file types are allowed: {string.Join(", ", _options.AllowedExtensions)}.");
        }

        if (!await EmployeeDocumentPolicy.MatchesSignatureAsync(upload.Content, extension, cancellationToken))
        {
            throw new BadRequestException($"The file content does not match a real {extension} file.");
        }

        // The stored name is generated here; the client's file name is kept only as a sanitized download name.
        var storageKey = await _documentStorage.SaveAsync(
            upload.Content, $"{Guid.NewGuid():N}{extension}", $"{employeeId:N}", cancellationToken);

        var document = new EmployeeDocument
        {
            EmployeeId = employeeId,
            DocumentType = documentType,
            FileName = SanitizeFileName(upload.FileName, extension),
            FilePath = storageKey,
            ContentType = EmployeeDocumentPolicy.GetContentType(extension),
            FileSizeBytes = upload.Length,
            Description = description,
            IsActive = true,
        };

        try
        {
            await _documentRepository.AddAsync(document, cancellationToken);
            await _documentRepository.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // The row was never saved, so don't leave an orphaned file behind.
            await _documentStorage.DeleteAsync(storageKey, CancellationToken.None);
            throw;
        }

        return MapToDto(document);
    }

    public async Task<List<EmployeeDocumentDto>> GetByEmployeeAsync(Guid employeeId, bool includeInactive, CancellationToken cancellationToken)
    {
        await EnsureEmployeeExistsAsync(employeeId, cancellationToken);

        var documents = await _documentRepository.GetByEmployeeIdAsync(employeeId, includeInactive, cancellationToken);
        return documents.Select(MapToDto).ToList();
    }

    public async Task<EmployeeDocumentDto> GetByIdAsync(Guid employeeId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await GetExistingAsync(employeeId, documentId, cancellationToken);
        return MapToDto(document);
    }

    public async Task<EmployeeDocumentFileDto> DownloadAsync(Guid employeeId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await GetActiveAsync(employeeId, documentId, cancellationToken);

        var content = await _documentStorage.OpenReadAsync(document.FilePath, cancellationToken)
            ?? throw new NotFoundException($"The file for document '{documentId}' is no longer available.");

        return new EmployeeDocumentFileDto
        {
            Content = content,
            FileName = document.FileName,
            ContentType = document.ContentType,
        };
    }

    public async Task<EmployeeDocumentDto> UpdateAsync(Guid employeeId, Guid documentId, UpdateEmployeeDocumentDto dto, CancellationToken cancellationToken)
    {
        var document = await GetActiveAsync(employeeId, documentId, cancellationToken);

        document.DocumentType = dto.DocumentType ?? document.DocumentType;
        document.Description = NormalizeOptional(dto.Description);
        document.UpdatedAt = DateTime.UtcNow;

        await _documentRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(document);
    }

    public async Task DeactivateAsync(Guid employeeId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await GetExistingAsync(employeeId, documentId, cancellationToken);

        if (!document.IsActive)
        {
            return;
        }

        document.IsActive = false;
        document.UpdatedAt = DateTime.UtcNow;

        await _documentRepository.SaveChangesAsync(cancellationToken);
    }

    internal static EmployeeDocumentDto MapToDto(EmployeeDocument document)
    {
        return new EmployeeDocumentDto
        {
            Id = document.Id,
            EmployeeId = document.EmployeeId,
            DocumentType = document.DocumentType.ToString(),
            FileName = document.FileName,
            ContentType = document.ContentType,
            FileSizeBytes = document.FileSizeBytes,
            Description = document.Description,
            IsActive = document.IsActive,
            UploadedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt,
            DownloadUrl = $"/api/employees/{document.EmployeeId}/documents/{document.Id}/download",
        };
    }

    private async Task EnsureEmployeeExistsAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        if (!await _employeeRepository.ExistsAsync(employeeId, cancellationToken))
        {
            throw new NotFoundException($"Employee with id '{employeeId}' was not found.");
        }
    }

    private async Task<EmployeeDocument> GetExistingAsync(Guid employeeId, Guid documentId, CancellationToken cancellationToken)
    {
        await EnsureEmployeeExistsAsync(employeeId, cancellationToken);

        // Looked up by employee AND document id: another employee's document is simply "not found" here.
        return await _documentRepository.GetByIdAsync(employeeId, documentId, cancellationToken)
            ?? throw new NotFoundException($"Document with id '{documentId}' was not found for employee '{employeeId}'.");
    }

    private async Task<EmployeeDocument> GetActiveAsync(Guid employeeId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await GetExistingAsync(employeeId, documentId, cancellationToken);

        return document.IsActive
            ? document
            : throw new NotFoundException($"Document with id '{documentId}' has been deactivated.");
    }

    private static EmployeeDocumentType ParseDocumentType(string? value)
    {
        var allowed = string.Join(", ", Enum.GetNames<EmployeeDocumentType>());

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BadRequestException($"The document type is required. Allowed values: {allowed}.");
        }

        // Names only: a number like "3" would otherwise parse as whatever enum value it happens to match.
        if (int.TryParse(value, out _)
            || !Enum.TryParse<EmployeeDocumentType>(value.Trim(), ignoreCase: true, out var documentType)
            || !Enum.IsDefined(documentType))
        {
            throw new BadRequestException($"'{value}' is not a valid document type. Allowed values: {allowed}.");
        }

        return documentType;
    }

    /// <summary>
    /// Reduces a client-supplied name to a harmless display name: no directory parts ("../", "C:\"), no invalid or
    /// control characters, bounded length, and always ending in the validated extension.
    /// </summary>
    private static string SanitizeFileName(string originalName, string extension)
    {
        var name = Path.GetFileNameWithoutExtension(originalName.Replace('\\', '/').Split('/').Last());
        var invalid = Path.GetInvalidFileNameChars();
        name = new string(name.Where(c => !invalid.Contains(c) && !char.IsControl(c)).ToArray()).Trim(' ', '.');

        if (name.Length == 0)
        {
            name = "document";
        }

        var maxNameLength = MaxFileNameLength - extension.Length;
        return (name.Length > maxNameLength ? name[..maxNameLength] : name) + extension;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string FormatSize(long bytes)
    {
        return bytes >= 1024 * 1024 ? $"{bytes / (1024.0 * 1024.0):0.#} MB" : $"{bytes / 1024.0:0.#} KB";
    }
}
