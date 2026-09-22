namespace smartHRMS.Application.Interfaces;

public interface IDepartmentRepository
{
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);
}
