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
        if (!content.CanSeek)
        {
            return true;
        }

        var originalPosition = content.Position;
        var header = new byte[12];
        int bytesRead;
        try
        {
            content.Position = 0;
            bytesRead = await content.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        }
        finally
        {
            content.Position = originalPosition;
        }

        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => bytesRead >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
                                      && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A,
            ".webp" => bytesRead >= 12 && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                                        && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50,
            _ => false,
        };
    }
}
