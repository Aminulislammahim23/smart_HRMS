using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Tests.Fakes;

public class FakeEmployeeDocumentRepository : IEmployeeDocumentRepository
{
    public List<EmployeeDocument> Documents { get; } = new();

    /// <summary>When set, SaveChangesAsync throws it (simulates a database failure after the file was stored).</summary>
    public Exception? SaveChangesException { get; set; }

    public Task<EmployeeDocument?> GetByIdAsync(Guid employeeId, Guid documentId, CancellationToken cancellationToken)
    {
        return Task.FromResult(Documents.FirstOrDefault(d => d.Id == documentId && d.EmployeeId == employeeId));
    }

    public Task<List<EmployeeDocument>> GetByEmployeeIdAsync(Guid employeeId, bool includeInactive, CancellationToken cancellationToken)
    {
        return Task.FromResult(Documents
            .Where(d => d.EmployeeId == employeeId && (includeInactive || d.IsActive))
            .OrderByDescending(d => d.CreatedAt)
            .ToList());
    }

    public Task AddAsync(EmployeeDocument document, CancellationToken cancellationToken)
    {
        Documents.Add(document);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return SaveChangesException is null ? Task.CompletedTask : Task.FromException(SaveChangesException);
    }
}
