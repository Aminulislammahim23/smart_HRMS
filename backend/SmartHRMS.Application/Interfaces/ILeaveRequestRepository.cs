using smartHRMS.Application.Features.Leaves;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Interfaces;

public interface ILeaveRequestRepository
{
    /// <summary>Tracked, with the employee loaded.</summary>
    Task<LeaveRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Read-only, with employees loaded; newest start date first.</summary>
    Task<List<LeaveRequest>> SearchAsync(LeaveFilter filter, CancellationToken cancellationToken);

    /// <summary>True when the employee has a Pending or Approved request overlapping the range (other than <paramref name="excludeId"/>).</summary>
    Task<bool> HasOverlapAsync(Guid employeeId, DateOnly from, DateOnly to, Guid? excludeId, CancellationToken cancellationToken);

    /// <summary>Read-only. Requests with one of <paramref name="statuses"/> that overlap the range, for the given employees.</summary>
    Task<List<LeaveRequest>> GetOverlappingAsync(IReadOnlyCollection<Guid> employeeIds, DateOnly from, DateOnly to, IReadOnlyCollection<LeaveStatus> statuses, CancellationToken cancellationToken);

    Task AddAsync(LeaveRequest request, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
