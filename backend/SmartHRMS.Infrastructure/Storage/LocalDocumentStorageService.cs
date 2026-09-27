using smartHRMS.Application.Interfaces;

namespace smartHRMS.Infrastructure.Storage;

/// <summary>
/// Local-disk implementation of <see cref="IDocumentStorageService"/>. Its root must be a folder that is NOT served as
/// static content (it defaults to App_Data, outside wwwroot), so documents can only be read through the API.
/// </summary>
public class LocalDocumentStorageService : IDocumentStorageService
{
    private readonly string _rootPath;

    public LocalDocumentStorageService(string rootPath)
    {
        _rootPath = rootPath;
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(Stream content, string fileName, string subFolder, CancellationToken cancellationToken)
    {
        var storageKey = $"{subFolder}/{fileName}";
        var physicalPath = SafeStoragePath.Combine(_rootPath, storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);

        // CreateNew: names are random, so an existing file means something is wrong — never overwrite it.
        await using (var fileStream = new FileStream(physicalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            content.Position = 0;
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        return storageKey;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var physicalPath = SafeStoragePath.Combine(_rootPath, storageKey);

        Stream? stream = File.Exists(physicalPath)
            ? new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true)
            : null;

        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var physicalPath = SafeStoragePath.Combine(_rootPath, storageKey);
        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }

        return Task.CompletedTask;
    }
}
