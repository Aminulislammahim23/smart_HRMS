using Microsoft.EntityFrameworkCore;
using smartHRMS.Application.Features.Attendances;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Infrastructure.Persistence;

namespace smartHRMS.Infrastructure.Repositories;

public class AttendanceRepository : IAttendanceRepository
{
    private readonly SmartHRMSDbContext _dbContext;

    public AttendanceRepository(SmartHRMSDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Attendance?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Attendances
            .Include(a => a.Employee)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<Attendance?> GetByEmployeeAndDateAsync(Guid employeeId, DateOnly attendanceDate, CancellationToken cancellationToken)
    {
        return await _dbContext.Attendances
            .Include(a => a.Employee)
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.AttendanceDate == attendanceDate, cancellationToken);
    }

    public async Task<List<Attendance>> SearchAsync(AttendanceFilter filter, CancellationToken cancellationToken)
    {
        var query = _dbContext.Attendances
            .AsNoTracking()
            .Include(a => a.Employee)
            .AsQueryable();

        if (filter.EmployeeId is not null)
        {
            query = query.Where(a => a.EmployeeId == filter.EmployeeId);
        }

        if (filter.From is not null)
        {
            query = query.Where(a => a.AttendanceDate >= filter.From);
        }

        if (filter.To is not null)
        {
            query = query.Where(a => a.AttendanceDate <= filter.To);
        }

        if (filter.Status is not null)
        {
            query = query.Where(a => a.Status == filter.Status);
        }

        return await query
            .OrderByDescending(a => a.AttendanceDate)
            .ThenBy(a => a.Employee!.EmployeeCode)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Attendance attendance, CancellationToken cancellationToken)
    {
        await _dbContext.Attendances.AddAsync(attendance, cancellationToken);
    }

    public void Remove(Attendance attendance)
    {
        _dbContext.Attendances.Remove(attendance);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
