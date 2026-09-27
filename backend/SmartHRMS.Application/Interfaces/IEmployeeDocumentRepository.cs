using smartHRMS.Domain.Entities;

namespace smartHRMS.Application.Interfaces;

public interface IEmployeeDocumentRepository
{
    /// <summary>
    /// Returns the document only if it belongs to <paramref name="employeeId"/>, so a document can never be reached
    /// through another employee's route by changing an id.
    /// </summary>
    Task<EmployeeDocument?> GetByIdAsync(Guid employeeId, Guid documentId, CancellationToken cancellationToken);

    /// <summary>Documents of one employee, newest first.</summary>
    Task<List<EmployeeDocument>> GetByEmployeeIdAsync(Guid employeeId, bool includeInactive, CancellationToken cancellationToken);

    Task AddAsync(EmployeeDocument document, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
