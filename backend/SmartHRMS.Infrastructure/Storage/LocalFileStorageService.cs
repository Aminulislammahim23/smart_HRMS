using smartHRMS.Application.Interfaces;

namespace smartHRMS.Infrastructure.Storage;

/// <summary>
/// Local-disk implementation of <see cref="IFileStorageService"/>, storing files under a public
/// static-file root (the API's wwwroot). Swappable for a cloud object-storage implementation later
/// without any change to the Application or Domain layers.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public LocalFileStorageService(string rootPath)
    {
        _rootPath = rootPath;
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(Stream content, string fileName, string subFolder, CancellationToken cancellationToken)
    {
        var physicalPath = SafeStoragePath.Combine(_rootPath, $"{subFolder}/{fileName}");
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);

        await using (var fileStream = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            content.Position = 0;
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        return $"/{subFolder.Trim('/').Replace('\\', '/')}/{fileName}";
    }

    public Task DeleteAsync(string? relativeUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl))
        {
            return Task.CompletedTask;
        }

        // Never trust the stored/incoming path as-is: it can only resolve to somewhere inside _rootPath.
        var physicalPath = SafeStoragePath.Combine(_rootPath, relativeUrl);
        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }

        return Task.CompletedTask;
    }
}
