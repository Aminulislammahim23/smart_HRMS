namespace smartHRMS.Application.Interfaces;

/// <summary>
/// Private file storage for employee documents. Unlike <see cref="IFileStorageService"/> (public photos served as
/// static files), nothing stored here is reachable by URL: files are only streamed back through the API.
/// </summary>
public interface IDocumentStorageService
{
    /// <summary>
    /// Saves <paramref name="content"/> as <paramref name="subFolder"/>/<paramref name="fileName"/> (both already sanitized)
    /// and returns the relative storage key to persist (e.g. "3f2c.../9a1b....pdf").
    /// </summary>
    Task<string> SaveAsync(Stream content, string fileName, string subFolder, CancellationToken cancellationToken);

    /// <summary>Opens the stored file for reading, or returns null if it no longer exists.</summary>
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>Deletes the stored file. A missing file is treated as already deleted.</summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}
