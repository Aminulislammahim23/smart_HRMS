namespace smartHRMS.Application.Interfaces;

/// <summary>
/// Storage-agnostic abstraction for saving and deleting uploaded files. Application code depends only
/// on this interface, never on a physical file system, so the implementation can later be swapped for
/// AWS S3, Azure Blob Storage, Cloudinary, etc. without touching the Domain or Application layers.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Saves <paramref name="content"/> under <paramref name="subFolder"/> using the given
    /// (already-sanitized) <paramref name="fileName"/>, overwriting any existing file at that path.
    /// Returns the relative public URL the caller can persist and later serve (e.g. "/uploads/employees/xyz.jpg").
    /// </summary>
    Task<string> SaveAsync(Stream content, string fileName, string subFolder, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the file previously returned by <see cref="SaveAsync"/>. A null/empty path, or a path
    /// that no longer exists, is treated as already-deleted and does not throw.
    /// </summary>
    Task DeleteAsync(string? relativeUrl, CancellationToken cancellationToken);
}
