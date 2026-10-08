namespace smartHRMS.Application.Features.Audit;

public class AuditLogDto
{
    public Guid Id { get; set; }

    public DateTime OccurredAt { get; set; }

    public Guid? UserId { get; set; }

    public string? Username { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    public string? Details { get; set; }
}

/// <summary>Query-string filters for the audit log (Admin only); all optional.</summary>
public class AuditLogQueryDto
{
    public string? EntityType { get; set; }

    public Guid? EntityId { get; set; }

    public string? Action { get; set; }

    /// <summary>Day 19: "payroll" limits the log to payroll entities (periods, records, payslips, salary structures, payments, reports).</summary>
    public string? Category { get; set; }

    /// <summary>User name contains.</summary>
    public string? Username { get; set; }

    /// <summary>Office dates (Asia/Dhaka) the entries were written on, inclusive.</summary>
    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    /// <summary>Newest entries first; 1–500, default 100.</summary>
    public int? Take { get; set; }
}
