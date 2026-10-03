using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.Leaves;

public static class LeaveTypeRules
{
    /// <summary>Paid leave keeps full salary; unpaid leave is deducted by payroll.</summary>
    public static bool IsPaid(LeaveType type) => type != LeaveType.Unpaid;
}
