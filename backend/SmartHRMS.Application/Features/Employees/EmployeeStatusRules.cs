using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.Employees;

internal static class EmployeeStatusRules
{
    /// <summary>
    /// Employees who still work here (leave is temporary). A department or designation can't be deactivated while
    /// any of them are assigned to it; Inactive/Resigned/Terminated records only keep it for history.
    /// </summary>
    public static readonly IReadOnlyCollection<EmployeeStatus> CurrentStatuses = new[] { EmployeeStatus.Active, EmployeeStatus.OnLeave };
}
