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
        var folderPath = ResolveFolder(subFolder);
        Directory.CreateDirectory(folderPath);

        var physicalPath = Path.Combine(folderPath, fileName);

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

        var physicalPath = ResolvePhysicalPath(relativeUrl);
        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }

        return Task.CompletedTask;
    }

    private string ResolveFolder(string subFolder)
    {
        var segments = subFolder.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Path.Combine(new[] { _rootPath }.Concat(segments).ToArray());
    }

    private string ResolvePhysicalPath(string relativeUrl)
    {
        // Never trust the stored/incoming path as-is: strip it down to bare segments so it can only
        // resolve to somewhere inside _rootPath, even if a caller ever passed a crafted value.
        var segments = relativeUrl
            .Split('/', '\\')
            .Where(segment => segment.Length > 0 && segment != "." && segment != "..");

        return Path.Combine(new[] { _rootPath }.Concat(segments).ToArray());
    }
}
