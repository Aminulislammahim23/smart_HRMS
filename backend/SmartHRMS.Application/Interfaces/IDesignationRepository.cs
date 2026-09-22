namespace smartHRMS.Application.Interfaces;

public interface IDesignationRepository
{
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);
}
