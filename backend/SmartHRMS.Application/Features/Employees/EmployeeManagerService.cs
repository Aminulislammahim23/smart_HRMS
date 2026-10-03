using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Common.Security;
using smartHRMS.Application.Features.Employees.Dtos;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Application.Features.Employees;

public interface IEmployeeManagerService
{
    /// <summary>Sets or clears the employee's manager (HR/Admin). Returns the new manager id.</summary>
    Task<Guid?> AssignAsync(Guid employeeId, AssignManagerDto dto, CancellationToken cancellationToken);
}

/// <summary>
/// Manager rules: the manager must exist and still work here; an employee can't manage themself; and the reporting
/// line can't loop (A manages B manages A).
/// </summary>
public class EmployeeManagerService : IEmployeeManagerService
{
    private const int MaxChainLength = 100;

    private readonly IEmployeeRepository _employeeRepository;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;

    public EmployeeManagerService(IEmployeeRepository employeeRepository, IAuditLogger auditLogger, ICurrentUser currentUser)
    {
        _employeeRepository = employeeRepository;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
    }

    public async Task<Guid?> AssignAsync(Guid employeeId, AssignManagerDto dto, CancellationToken cancellationToken)
    {
        _currentUser.EnsureHrOrAdmin();

        var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee with id '{employeeId}' was not found.");

        var managerId = dto.ManagerId == Guid.Empty ? null : dto.ManagerId;
        if (managerId is { } id)
        {
            if (id == employee.Id)
            {
                throw new BadRequestException("An employee can't be their own manager.");
            }

            var manager = await _employeeRepository.GetByIdAsync(id, cancellationToken)
                ?? throw new BadRequestException($"Employee with id '{id}' does not exist.");

            if (!EmployeeStatusRules.CurrentStatuses.Contains(manager.Status))
            {
                throw new BadRequestException($"Employee '{manager.EmployeeCode}' is {manager.Status} and can't be a manager.");
            }

            await EnsureNoLoopAsync(employee, manager, cancellationToken);
        }

        employee.ManagerId = managerId;
        employee.UpdatedAt = DateTime.UtcNow;
        _auditLogger.Add(AuditActions.ManagerAssigned, nameof(Employee), employee.Id, managerId is null ? "Manager cleared." : $"Manager set to {managerId}.");
        await _employeeRepository.SaveChangesAsync(cancellationToken);

        return managerId;
    }

    private async Task EnsureNoLoopAsync(Employee employee, Employee manager, CancellationToken cancellationToken)
    {
        var current = manager;
        for (var step = 0; step < MaxChainLength && current.ManagerId is { } next; step++)
        {
            if (next == employee.Id)
            {
                throw new BadRequestException($"'{manager.EmployeeCode}' reports to '{employee.EmployeeCode}' (directly or indirectly), so this would create a reporting loop.");
            }

            current = await _employeeRepository.GetByIdAsync(next, cancellationToken);
            if (current is null)
            {
                return;
            }
        }
    }
}
