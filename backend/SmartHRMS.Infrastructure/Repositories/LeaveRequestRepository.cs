using Microsoft.EntityFrameworkCore;
using smartHRMS.Application.Features.Leaves;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;
using smartHRMS.Infrastructure.Persistence;

namespace smartHRMS.Infrastructure.Repositories;

public class LeaveRequestRepository : ILeaveRequestRepository
{
    private readonly SmartHRMSDbContext _dbContext;

    public LeaveRequestRepository(SmartHRMSDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<LeaveRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.LeaveRequests
            .Include(l => l.Employee)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
    }

    public async Task<List<LeaveRequest>> SearchAsync(LeaveFilter filter, CancellationToken cancellationToken)
    {
        var query = _dbContext.LeaveRequests
            .AsNoTracking()
            .Include(l => l.Employee)
            .AsQueryable();

        if (filter.EmployeeIds is not null)
        {
            var ids = filter.EmployeeIds.ToList();
            query = query.Where(l => ids.Contains(l.EmployeeId));
        }

        if (filter.Status is not null)
        {
            query = query.Where(l => l.Status == filter.Status);
        }

        if (filter.LeaveType is not null)
        {
            query = query.Where(l => l.LeaveType == filter.LeaveType);
        }

        // Overlap with the requested range: a request that ends on or after From and starts on or before To.
        if (filter.From is not null)
        {
            query = query.Where(l => l.EndDate >= filter.From);
        }

        if (filter.To is not null)
        {
            query = query.Where(l => l.StartDate <= filter.To);
        }

        return await query
            .OrderByDescending(l => l.StartDate)
            .ThenBy(l => l.Employee!.EmployeeCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasOverlapAsync(Guid employeeId, DateOnly from, DateOnly to, Guid? excludeId, CancellationToken cancellationToken)
    {
        return await _dbContext.LeaveRequests.AnyAsync(l =>
            l.EmployeeId == employeeId
            && (l.Status == LeaveStatus.Pending || l.Status == LeaveStatus.Approved)
            && l.StartDate <= to && l.EndDate >= from
            && (excludeId == null || l.Id != excludeId), cancellationToken);
    }

    public async Task<List<LeaveRequest>> GetOverlappingAsync(
        IReadOnlyCollection<Guid> employeeIds, DateOnly from, DateOnly to, IReadOnlyCollection<LeaveStatus> statuses, CancellationToken cancellationToken)
    {
        var ids = employeeIds.ToList();
        var statusList = statuses.ToList();

        return await _dbContext.LeaveRequests
            .AsNoTracking()
            .Where(l => ids.Contains(l.EmployeeId) && statusList.Contains(l.Status) && l.StartDate <= to && l.EndDate >= from)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(LeaveRequest request, CancellationToken cancellationToken)
    {
        await _dbContext.LeaveRequests.AddAsync(request, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
