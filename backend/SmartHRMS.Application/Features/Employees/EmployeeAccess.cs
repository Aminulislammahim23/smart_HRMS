using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Common.Security;
using smartHRMS.Application.Interfaces;

namespace smartHRMS.Application.Features.Employees;

/// <summary>
/// Who may read an employee's records: HR and Admin (everyone), the employee themself, and — for the profile and
/// attendance only — their direct manager. Salary, documents and payroll are never shown to managers.
/// </summary>
public interface IEmployeeAccess
{
    /// <summary>403 unless the user is HR/Admin, the employee, or their direct manager.</summary>
    Task EnsureCanViewAsync(Guid employeeId, CancellationToken cancellationToken);

    /// <summary>403 unless the user is HR/Admin or the employee (private data: documents, salary, payroll).</summary>
    void EnsureCanViewPrivate(Guid employeeId);
}

public class EmployeeAccess : IEmployeeAccess
{
    private readonly ICurrentUser _currentUser;
    private readonly IEmployeeRepository _employeeRepository;

    public EmployeeAccess(ICurrentUser currentUser, IEmployeeRepository employeeRepository)
    {
        _currentUser = currentUser;
        _employeeRepository = employeeRepository;
    }

    public async Task EnsureCanViewAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        if (_currentUser.IsHrOrAdmin() || _currentUser.IsSelf(employeeId))
        {
            return;
        }

        if (_currentUser.EmployeeId is { } own)
        {
            var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken);
            if (employee?.ManagerId == own)
            {
                return;
            }
        }

        throw new ForbiddenException("You can only view your own records or those of your direct reports.");
    }

    public void EnsureCanViewPrivate(Guid employeeId)
    {
        _currentUser.EnsureSelfOrHrOrAdmin(employeeId);
    }
}
