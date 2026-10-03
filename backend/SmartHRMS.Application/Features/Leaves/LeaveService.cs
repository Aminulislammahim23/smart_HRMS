using smartHRMS.Application.Common.Calendar;
using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Common.Security;
using smartHRMS.Application.Common.Text;
using smartHRMS.Application.Features.Employees;
using smartHRMS.Application.Features.Leaves.Dtos;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.Leaves;

/// <summary>
/// Leave rules:
/// - Employees and Managers apply for themselves; HR/Admin may apply for any current employee;
/// - whole working days only; a range with no working day is refused; at most <see cref="MaxRangeDays"/> calendar days;
/// - no overlap with the employee's other Pending or Approved leave (409);
/// - the employee's direct manager, HR or Admin approve or reject Pending requests, but nobody reviews their own;
/// - the owner cancels their Pending request; HR/Admin also cancel Approved leave;
/// - leave can't be approved or cancelled once payroll for those dates is under review or final;
/// - who can see a request: the employee, their direct manager, HR and Admin.
/// </summary>
public class LeaveService : ILeaveService
{
    public const int MaxRangeDays = 90;

    private readonly ILeaveRequestRepository _leaveRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IPayrollRepository _payrollRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;
    private readonly WorkCalendar _calendar;

    public LeaveService(
        ILeaveRequestRepository leaveRepository,
        IEmployeeRepository employeeRepository,
        IPayrollRepository payrollRepository,
        IUserRepository userRepository,
        IAuditLogger auditLogger,
        ICurrentUser currentUser,
        WorkCalendar calendar)
    {
        _leaveRepository = leaveRepository;
        _employeeRepository = employeeRepository;
        _payrollRepository = payrollRepository;
        _userRepository = userRepository;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
        _calendar = calendar;
    }

    public List<LeaveTypeDto> GetLeaveTypes()
    {
        return Enum.GetValues<LeaveType>()
            .Select(type => new LeaveTypeDto { Name = type.ToString(), IsPaid = LeaveTypeRules.IsPaid(type) })
            .ToList();
    }

    public async Task<List<LeaveRequestDto>> GetAllAsync(LeaveQueryDto query, CancellationToken cancellationToken)
    {
        if (query.StartDate > query.EndDate)
        {
            throw new BadRequestException("The startDate cannot be after the endDate.");
        }

        var scope = await ResolveScopeAsync(query.Scope, cancellationToken);
        if (query.EmployeeId is { } employeeId)
        {
            if (scope is not null && !scope.Contains(employeeId))
            {
                throw new ForbiddenException("You can't view this employee's leave.");
            }

            scope = new[] { employeeId };
        }

        var filter = new LeaveFilter(scope, ParseEnum<LeaveStatus>(query.Status, "leave status"), ParseEnum<LeaveType>(query.LeaveType, "leave type"), query.StartDate, query.EndDate);
        var requests = await _leaveRepository.SearchAsync(filter, cancellationToken);
        return await MapAsync(requests, cancellationToken);
    }

    public async Task<LeaveRequestDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var request = await GetVisibleAsync(id, cancellationToken);
        return (await MapAsync(new[] { request }, cancellationToken))[0];
    }

    public async Task<LeaveRequestDto> CreateAsync(CreateLeaveRequestDto dto, CancellationToken cancellationToken)
    {
        var userId = _currentUser.RequireUserId();
        var employeeId = ResolveApplicant(dto.EmployeeId);
        var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee with id '{employeeId}' was not found.");

        if (!EmployeeStatusRules.CurrentStatuses.Contains(employee.Status))
        {
            throw new BadRequestException($"Employee '{employee.EmployeeCode}' is {employee.Status} and can't apply for leave.");
        }

        var start = dto.StartDate!.Value;
        var end = dto.EndDate!.Value;
        var type = dto.LeaveType!.Value;

        if (start > end)
        {
            throw new BadRequestException("The start date cannot be after the end date.");
        }

        if (end.DayNumber - start.DayNumber + 1 > MaxRangeDays)
        {
            throw new BadRequestException($"A leave request can cover at most {MaxRangeDays} calendar days.");
        }

        if (start < DateOnly.FromDateTime(employee.JoiningDate))
        {
            throw new BadRequestException($"Leave cannot start before the joining date ({employee.JoiningDate:yyyy-MM-dd}).");
        }

        var totalDays = _calendar.CountWorkingDays(start, end);
        if (totalDays == 0)
        {
            throw new BadRequestException("The selected dates contain no working days.");
        }

        if (await _leaveRepository.HasOverlapAsync(employee.Id, start, end, null, cancellationToken))
        {
            throw new ConflictException($"Employee '{employee.EmployeeCode}' already has pending or approved leave overlapping {start:yyyy-MM-dd} – {end:yyyy-MM-dd}.");
        }

        await EnsurePayrollOpenAsync(start, end, cancellationToken);

        var request = new LeaveRequest
        {
            EmployeeId = employee.Id,
            Employee = employee,
            LeaveType = type,
            StartDate = start,
            EndDate = end,
            TotalDays = totalDays,
            Reason = InputText.Optional(dto.Reason),
            Status = LeaveStatus.Pending,
            RequestedByUserId = userId,
        };

        await _leaveRepository.AddAsync(request, cancellationToken);
        _auditLogger.Add(AuditActions.LeaveRequested, nameof(LeaveRequest), request.Id, $"{employee.EmployeeCode}: {type} {start:yyyy-MM-dd}..{end:yyyy-MM-dd} ({totalDays} days).");
        await _leaveRepository.SaveChangesAsync(cancellationToken);

        return (await MapAsync(new[] { request }, cancellationToken))[0];
    }

    public Task<LeaveRequestDto> ApproveAsync(Guid id, ReviewLeaveRequestDto dto, CancellationToken cancellationToken)
    {
        return ReviewAsync(id, dto, LeaveStatus.Approved, AuditActions.LeaveApproved, cancellationToken);
    }

    public Task<LeaveRequestDto> RejectAsync(Guid id, ReviewLeaveRequestDto dto, CancellationToken cancellationToken)
    {
        return ReviewAsync(id, dto, LeaveStatus.Rejected, AuditActions.LeaveRejected, cancellationToken);
    }

    public async Task<LeaveRequestDto> CancelAsync(Guid id, ReviewLeaveRequestDto dto, CancellationToken cancellationToken)
    {
        var request = await GetVisibleAsync(id, cancellationToken);

        if (!CanCancel(request))
        {
            throw request.Status is LeaveStatus.Pending or LeaveStatus.Approved
                ? new ForbiddenException("You can't cancel this leave request.")
                : new ConflictException($"A {request.Status} leave request can't be cancelled.");
        }

        if (request.Status == LeaveStatus.Approved)
        {
            await EnsurePayrollOpenAsync(request.StartDate, request.EndDate, cancellationToken);
        }

        request.Status = LeaveStatus.Cancelled;
        request.ReviewedByUserId = _currentUser.RequireUserId();
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewComment = InputText.Optional(dto.Comment) ?? request.ReviewComment;
        request.UpdatedAt = DateTime.UtcNow;

        _auditLogger.Add(AuditActions.LeaveCancelled, nameof(LeaveRequest), request.Id);
        await _leaveRepository.SaveChangesAsync(cancellationToken);

        return (await MapAsync(new[] { request }, cancellationToken))[0];
    }

    private async Task<LeaveRequestDto> ReviewAsync(Guid id, ReviewLeaveRequestDto dto, LeaveStatus decision, string auditAction, CancellationToken cancellationToken)
    {
        var request = await GetVisibleAsync(id, cancellationToken);

        if (_currentUser.IsSelf(request.EmployeeId))
        {
            throw new ForbiddenException("You can't approve or reject your own leave.");
        }

        if (!IsReviewer(request))
        {
            throw new ForbiddenException("Only the employee's manager, HR or Admin can review this leave request.");
        }

        if (request.Status != LeaveStatus.Pending)
        {
            throw new ConflictException($"This leave request is already {request.Status}.");
        }

        if (decision == LeaveStatus.Approved)
        {
            await EnsurePayrollOpenAsync(request.StartDate, request.EndDate, cancellationToken);
        }

        request.Status = decision;
        request.ReviewedByUserId = _currentUser.RequireUserId();
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewComment = InputText.Optional(dto.Comment);
        request.UpdatedAt = DateTime.UtcNow;

        _auditLogger.Add(auditAction, nameof(LeaveRequest), request.Id, request.ReviewComment);
        await _leaveRepository.SaveChangesAsync(cancellationToken);

        return (await MapAsync(new[] { request }, cancellationToken))[0];
    }

    /// <summary>Employees and Managers apply for themselves only; HR/Admin for anyone (themselves by default).</summary>
    private Guid ResolveApplicant(Guid? requested)
    {
        if (requested is null || requested == Guid.Empty)
        {
            return _currentUser.RequireEmployeeId();
        }

        if (!_currentUser.IsSelf(requested.Value) && !_currentUser.IsHrOrAdmin())
        {
            throw new ForbiddenException("You can only apply for leave for yourself.");
        }

        return requested.Value;
    }

    /// <summary>The employee ids the caller may list, or null for everyone (HR/Admin "all").</summary>
    private async Task<IReadOnlyCollection<Guid>?> ResolveScopeAsync(string? scope, CancellationToken cancellationToken)
    {
        var normalized = string.IsNullOrWhiteSpace(scope) ? null : scope.Trim().ToLowerInvariant();
        var own = _currentUser.EmployeeId;

        switch (normalized)
        {
            case "mine":
                return own is null ? Array.Empty<Guid>() : new[] { own.Value };
            case "team":
                return own is null ? Array.Empty<Guid>() : await _employeeRepository.GetDirectReportIdsAsync(own.Value, cancellationToken);
            case "all":
                _currentUser.EnsureHrOrAdmin();
                return null;
            case null:
                if (_currentUser.IsHrOrAdmin())
                {
                    return null;
                }

                var visible = own is null ? new List<Guid>() : await _employeeRepository.GetDirectReportIdsAsync(own.Value, cancellationToken);
                if (own is not null)
                {
                    visible.Add(own.Value);
                }

                return visible;
            default:
                throw new BadRequestException($"'{scope}' is not a valid scope. Allowed values: mine, team, all.");
        }
    }

    private async Task<LeaveRequest> GetVisibleAsync(Guid id, CancellationToken cancellationToken)
    {
        var request = await _leaveRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Leave request with id '{id}' was not found.");

        if (!_currentUser.IsHrOrAdmin() && !_currentUser.IsSelf(request.EmployeeId) && !IsDirectManager(request))
        {
            // Not 403: don't reveal that someone else's request exists.
            throw new NotFoundException($"Leave request with id '{id}' was not found.");
        }

        return request;
    }

    private bool IsDirectManager(LeaveRequest request)
    {
        return _currentUser.EmployeeId is { } own && request.Employee?.ManagerId == own;
    }

    private bool IsReviewer(LeaveRequest request)
    {
        return !_currentUser.IsSelf(request.EmployeeId) && (_currentUser.IsHrOrAdmin() || IsDirectManager(request));
    }

    private bool CanCancel(LeaveRequest request)
    {
        return request.Status switch
        {
            LeaveStatus.Pending => _currentUser.IsSelf(request.EmployeeId) || _currentUser.IsHrOrAdmin(),
            LeaveStatus.Approved => _currentUser.IsHrOrAdmin(),
            _ => false,
        };
    }

    private async Task EnsurePayrollOpenAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (await _payrollRepository.IsDateRangeLockedAsync(from, to, cancellationToken))
        {
            throw new ConflictException("Payroll covering these dates is pending approval, approved or paid, so this leave can no longer change.");
        }
    }

    private static TEnum? ParseEnum<TEnum>(string? value, string label) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value, out _) || !Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
        {
            throw new BadRequestException($"'{value}' is not a valid {label}. Allowed values: {string.Join(", ", Enum.GetNames<TEnum>())}.");
        }

        return parsed;
    }

    private async Task<List<LeaveRequestDto>> MapAsync(IReadOnlyCollection<LeaveRequest> requests, CancellationToken cancellationToken)
    {
        var userIds = requests
            .SelectMany(r => new[] { r.RequestedByUserId, r.ReviewedByUserId })
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var names = userIds.Count == 0 ? new Dictionary<Guid, string>() : await _userRepository.GetDisplayNamesAsync(userIds, cancellationToken);

        string? NameOf(Guid? id) => id is { } value && names.TryGetValue(value, out var name) ? name : null;

        return requests.Select(request => new LeaveRequestDto
        {
            Id = request.Id,
            EmployeeId = request.EmployeeId,
            EmployeeCode = request.Employee?.EmployeeCode ?? string.Empty,
            EmployeeName = request.Employee is null ? string.Empty : $"{request.Employee.FirstName} {request.Employee.LastName}".Trim(),
            LeaveType = request.LeaveType.ToString(),
            IsPaid = LeaveTypeRules.IsPaid(request.LeaveType),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            TotalDays = request.TotalDays,
            Reason = request.Reason,
            Status = request.Status.ToString(),
            RequestedBy = NameOf(request.RequestedByUserId),
            ReviewedBy = NameOf(request.ReviewedByUserId),
            ReviewedAt = request.ReviewedAt,
            ReviewComment = request.ReviewComment,
            CanReview = request.Status == LeaveStatus.Pending && IsReviewer(request),
            CanCancel = CanCancel(request),
            CreatedAt = request.CreatedAt,
            UpdatedAt = request.UpdatedAt,
        }).ToList();
    }
}
