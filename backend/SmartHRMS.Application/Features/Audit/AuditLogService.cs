using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Common.Security;
using smartHRMS.Application.Common.Text;
using smartHRMS.Application.Features.Attendances;
using smartHRMS.Application.Interfaces;

namespace smartHRMS.Application.Features.Audit;

public interface IAuditLogService
{
    Task<List<AuditLogDto>> SearchAsync(AuditLogQueryDto query, CancellationToken cancellationToken);
}

/// <summary>Read access to the audit log, for Admins only.</summary>
public class AuditLogService : IAuditLogService
{
    /// <summary>The entity types written by payroll, payslip, salary, payment and report actions (category "payroll").</summary>
    public static readonly IReadOnlyCollection<string> PayrollEntityTypes = new[]
    {
        "PayrollPeriod", "PayrollRecord", "Payslip", "EmployeeSalaryStructure", "PaymentBatch", "PaymentTransaction", "PayrollReport",
    };

    private readonly IAuditLogReader _reader;
    private readonly ICurrentUser _currentUser;
    private readonly AttendanceClock _clock;

    public AuditLogService(IAuditLogReader reader, ICurrentUser currentUser, AttendanceClock clock)
    {
        _reader = reader;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<List<AuditLogDto>> SearchAsync(AuditLogQueryDto query, CancellationToken cancellationToken)
    {
        _currentUser.EnsureAdmin();

        var take = query.Take ?? 100;
        if (take is < 1 or > 500)
        {
            throw new BadRequestException("take must be between 1 and 500.");
        }

        if (query.From > query.To)
        {
            throw new BadRequestException("The 'from' date can't be after the 'to' date.");
        }

        var entityType = InputText.Optional(query.EntityType);
        IReadOnlyCollection<string>? entityTypes = entityType is null ? null : new[] { entityType };
        switch (InputText.Optional(query.Category)?.ToLowerInvariant())
        {
            case null:
                break;
            case "payroll":
                entityTypes = entityType is null ? PayrollEntityTypes : PayrollEntityTypes.Where(t => t == entityType).ToList();
                break;
            default:
                throw new BadRequestException($"'{query.Category}' is not a valid category. Allowed values: payroll.");
        }

        var filter = new AuditLogFilter(
            entityTypes,
            query.EntityId,
            InputText.Optional(query.Action),
            InputText.Optional(query.Username),
            query.From is { } from ? _clock.StartOfDayUtc(from) : null,
            query.To is { } to ? _clock.StartOfDayUtc(to.AddDays(1)) : null,
            take);
        return await _reader.SearchAsync(filter, cancellationToken);
    }
}
