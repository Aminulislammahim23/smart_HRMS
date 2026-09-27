using smartHRMS.Application.Common.Files;

namespace smartHRMS.Application.Features.Employees;

/// <summary>
/// Server-side rules for employee profile photo uploads. Centralized here so the accepted formats/size
/// are defined once and enforced regardless of what a client claims about the file.
/// </summary>
internal static class EmployeePhotoPolicy
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    public const string StorageSubFolder = "uploads/employees";

    public static readonly IReadOnlySet<string> AllowedExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    public static readonly IReadOnlySet<string> AllowedContentTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };

    /// <summary>
    /// Confirms the stream actually starts with the magic bytes for <paramref name="extension"/>, so a
    /// renamed/relabeled non-image file is rejected even if the extension and Content-Type header lied.
    /// Leaves the stream position unchanged. Returns true (unverified, not rejected) if the stream can't be seeked.
    /// </summary>
    public static async Task<bool> MatchesImageSignatureAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        var header = await FileSignature.ReadHeaderAsync(content, 12, cancellationToken);
        if (header is null)
        {
            return true;
        }

        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => FileSignature.StartsWith(header, 0xFF, 0xD8, 0xFF),
            ".png" => FileSignature.StartsWith(header, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A),
            // RIFF....WEBP: bytes 4-7 are the file size, so check the two tags separately.
            ".webp" => FileSignature.StartsWith(header, 0x52, 0x49, 0x46, 0x46) && header.Length >= 12
                       && header.AsSpan(8, 4).SequenceEqual(new byte[] { 0x57, 0x45, 0x42, 0x50 }),
            _ => false,
        };
    }
}
