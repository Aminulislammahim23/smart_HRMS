namespace smartHRMS.Infrastructure.Storage;

/// <summary>
/// Turns a relative storage path into a physical path that is guaranteed to stay inside the storage root, whatever
/// the input contains ("..", absolute paths, drive letters). Shared by the photo and document storages.
/// </summary>
internal static class SafeStoragePath
{
    public static string Combine(string rootPath, string relativePath)
    {
        // Keep only plain path segments: drop empty, ".", ".." and anything with a drive/stream separator (":").
        var segments = relativePath
            .Split('/', '\\')
            .Select(segment => segment.Trim())
            .Where(segment => segment.Length > 0 && segment != "." && segment != ".." && !segment.Contains(':'));

        var root = Path.GetFullPath(rootPath);
        var fullPath = Path.GetFullPath(Path.Combine(new[] { root }.Concat(segments).ToArray()));

        // Defense in depth: even after filtering, refuse anything that doesn't resolve inside the root.
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The storage path resolves outside the storage root.");
        }

        return fullPath;
    }
}
