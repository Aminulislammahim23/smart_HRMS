using smartHRMS.Application.Interfaces;

namespace smartHRMS.Tests.Fakes;

public class FakeFileStorageService : IFileStorageService
{
    public List<string> SavedPaths { get; } = new();

    public List<string> DeletedPaths { get; } = new();

    public Task<string> SaveAsync(Stream content, string fileName, string subFolder, CancellationToken cancellationToken)
    {
        var path = $"/{subFolder.Trim('/')}/{fileName}";
        SavedPaths.Add(path);
        return Task.FromResult(path);
    }

    public Task DeleteAsync(string? relativeUrl, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(relativeUrl))
        {
            DeletedPaths.Add(relativeUrl);
        }

        return Task.CompletedTask;
    }
}
