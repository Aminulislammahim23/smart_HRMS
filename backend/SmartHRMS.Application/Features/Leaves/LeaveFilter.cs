using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.Leaves;

/// <summary>
/// Already-validated search criteria. <see cref="EmployeeIds"/> limits the result to those employees (the caller's
/// visibility scope); null means every employee.
/// </summary>
public sealed record LeaveFilter(IReadOnlyCollection<Guid>? EmployeeIds, LeaveStatus? Status, LeaveType? LeaveType, DateOnly? From, DateOnly? To);
