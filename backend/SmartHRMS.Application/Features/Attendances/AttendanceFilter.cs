using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.Attendances;

/// <summary>Already-validated search criteria passed to the repository. Null means "no restriction".</summary>
public sealed record AttendanceFilter(Guid? EmployeeId, DateOnly? From, DateOnly? To, AttendanceStatus? Status);
