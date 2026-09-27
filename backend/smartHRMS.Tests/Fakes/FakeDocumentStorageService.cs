using smartHRMS.Application.Interfaces;

namespace smartHRMS.Tests.Fakes;

/// <summary>In-memory document storage: keeps saved bytes by storage key.</summary>
public class FakeDocumentStorageService : IDocumentStorageService
{
    public Dictionary<string, byte[]> Files { get; } = new();

    public List<string> DeletedKeys { get; } = new();

    public async Task<string> SaveAsync(Stream content, string fileName, string subFolder, CancellationToken cancellationToken)
    {
        using var copy = new MemoryStream();
        content.Position = 0;
        await content.CopyToAsync(copy, cancellationToken);

        var key = $"{subFolder}/{fileName}";
        Files[key] = copy.ToArray();
        return key;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        Stream? stream = Files.TryGetValue(storageKey, out var bytes) ? new MemoryStream(bytes) : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        Files.Remove(storageKey);
        DeletedKeys.Add(storageKey);
        return Task.CompletedTask;
    }
}
