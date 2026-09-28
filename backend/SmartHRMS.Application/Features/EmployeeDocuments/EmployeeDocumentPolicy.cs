using smartHRMS.Application.Common.Files;

namespace smartHRMS.Application.Features.EmployeeDocuments;

/// <summary>
/// The file formats the server knows how to verify, with the Content-Type it serves each one as. Configuration can
/// only choose from this list, so an executable (.exe, .bat, .ps1, .dll, ...) can never be allowed by mistake.
/// </summary>
internal static class EmployeeDocumentPolicy
{
    private sealed record KnownFormat(string ContentType, Func<byte[], bool> MatchesSignature);

    private static readonly IReadOnlyDictionary<string, KnownFormat> KnownFormats =
        new Dictionary<string, KnownFormat>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = new("application/pdf", h => FileSignature.StartsWith(h, 0x25, 0x50, 0x44, 0x46, 0x2D)), // %PDF-
            [".jpg"] = new("image/jpeg", h => FileSignature.StartsWith(h, 0xFF, 0xD8, 0xFF)),
            [".jpeg"] = new("image/jpeg", h => FileSignature.StartsWith(h, 0xFF, 0xD8, 0xFF)),
            [".png"] = new("image/png", h => FileSignature.StartsWith(h, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)),
            // Legacy Word: OLE compound file header.
            [".doc"] = new("application/msword", h => FileSignature.StartsWith(h, 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1)),
            // Word 2007+: a ZIP container.
            [".docx"] = new(
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                h => FileSignature.StartsWith(h, 0x50, 0x4B, 0x03, 0x04)),
        };

    /// <summary>Fails fast at startup if the configuration is unusable or names a format that can't be verified.</summary>
    public static void EnsureValid(EmployeeDocumentOptions options)
    {
        if (options.MaxFileSizeBytes <= 0)
        {
            throw new InvalidOperationException($"{EmployeeDocumentOptions.SectionName}:MaxFileSizeBytes must be greater than zero.");
        }

        if (options.AllowedExtensions.Length == 0)
        {
            throw new InvalidOperationException($"{EmployeeDocumentOptions.SectionName}:AllowedExtensions must list at least one extension.");
        }

        var unsupported = options.AllowedExtensions.Where(extension => !KnownFormats.ContainsKey(extension)).ToList();
        if (unsupported.Count > 0)
        {
            throw new InvalidOperationException(
                $"{EmployeeDocumentOptions.SectionName}:AllowedExtensions contains unsupported types: {string.Join(", ", unsupported)}. " +
                $"Supported: {string.Join(", ", KnownFormats.Keys)}.");
        }
    }

    public static string GetContentType(string extension) => KnownFormats[extension].ContentType;

    /// <summary>
    /// The client-declared Content-Type must agree with the extension. Missing or generic types are accepted because
    /// some browsers send application/octet-stream for Office files; the signature check is what really proves the format.
    /// </summary>
    public static bool IsAcceptableClientContentType(string extension, string? contentType)
    {
        var declared = contentType?.Split(';')[0].Trim();
        if (string.IsNullOrEmpty(declared) || declared.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var expected = KnownFormats[extension].ContentType;
        return declared.Equals(expected, StringComparison.OrdinalIgnoreCase)
            || (expected == "image/jpeg" && declared.Equals("image/pjpeg", StringComparison.OrdinalIgnoreCase));
    }

    public static async Task<bool> MatchesSignatureAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        var header = await FileSignature.ReadHeaderAsync(content, 8, cancellationToken);
        return header is null || KnownFormats[extension].MatchesSignature(header);
    }
}
