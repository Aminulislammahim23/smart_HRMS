using smartHRMS.Application.Features.Leaves;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Tests.Fakes;

public class FakeLeaveRequestRepository : ILeaveRequestRepository
{
    private readonly FakeEmployeeRepository _employees;

    public FakeLeaveRequestRepository(FakeEmployeeRepository employees)
    {
        _employees = employees;
    }

    public List<LeaveRequest> Requests { get; } = new();

    public Task<LeaveRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(Attach(Requests.FirstOrDefault(r => r.Id == id)));
    }

    public Task<List<LeaveRequest>> SearchAsync(LeaveFilter filter, CancellationToken cancellationToken)
    {
        var query = Requests.AsEnumerable();
        if (filter.EmployeeIds is not null)
        {
            query = query.Where(r => filter.EmployeeIds.Contains(r.EmployeeId));
        }

        if (filter.Status is not null)
        {
            query = query.Where(r => r.Status == filter.Status);
        }

        if (filter.LeaveType is not null)
        {
            query = query.Where(r => r.LeaveType == filter.LeaveType);
        }

        if (filter.From is not null)
        {
            query = query.Where(r => r.EndDate >= filter.From);
        }

        if (filter.To is not null)
        {
            query = query.Where(r => r.StartDate <= filter.To);
        }

        return Task.FromResult(query.OrderByDescending(r => r.StartDate).Select(r => Attach(r)!).ToList());
    }

    public Task<bool> HasOverlapAsync(Guid employeeId, DateOnly from, DateOnly to, Guid? excludeId, CancellationToken cancellationToken)
    {
        return Task.FromResult(Requests.Any(r =>
            r.EmployeeId == employeeId && r.Status is LeaveStatus.Pending or LeaveStatus.Approved
            && r.StartDate <= to && r.EndDate >= from && r.Id != excludeId));
    }

    public Task<List<LeaveRequest>> GetOverlappingAsync(IReadOnlyCollection<Guid> employeeIds, DateOnly from, DateOnly to, IReadOnlyCollection<LeaveStatus> statuses, CancellationToken cancellationToken)
    {
        return Task.FromResult(Requests
            .Where(r => employeeIds.Contains(r.EmployeeId) && statuses.Contains(r.Status) && r.StartDate <= to && r.EndDate >= from)
            .ToList());
    }

    public Task AddAsync(LeaveRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private LeaveRequest? Attach(LeaveRequest? request)
    {
        if (request is not null)
        {
            request.Employee ??= _employees.Employees.FirstOrDefault(e => e.Id == request.EmployeeId);
        }

        return request;
    }
}
