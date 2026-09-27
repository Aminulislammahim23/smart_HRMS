namespace smartHRMS.Application.Common.Files;

/// <summary>
/// Checks a file's leading "magic bytes", so a renamed or relabeled file is caught even when its extension and
/// Content-Type header lie. Shared by the photo and document upload rules.
/// </summary>
internal static class FileSignature
{
    /// <summary>
    /// Reads up to <paramref name="count"/> bytes from the start of <paramref name="content"/> and leaves the position
    /// unchanged. Returns null if the stream can't be seeked (nothing can be verified then).
    /// </summary>
    public static async Task<byte[]?> ReadHeaderAsync(Stream content, int count, CancellationToken cancellationToken)
    {
        if (!content.CanSeek)
        {
            return null;
        }

        var originalPosition = content.Position;
        var buffer = new byte[count];
        int bytesRead;
        try
        {
            content.Position = 0;
            bytesRead = await content.ReadAsync(buffer.AsMemory(0, count), cancellationToken);
        }
        finally
        {
            content.Position = originalPosition;
        }

        return buffer[..bytesRead];
    }

    public static bool StartsWith(byte[] header, params byte[] prefix)
    {
        return header.Length >= prefix.Length && header.AsSpan(0, prefix.Length).SequenceEqual(prefix);
    }
}
