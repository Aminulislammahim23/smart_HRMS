using smartHRMS.Domain.Common;

namespace smartHRMS.Domain.Entities;

/// <summary>An append-only record of an important action (sign-in, approvals, payroll changes). CreatedAt is when it happened.</summary>
public class AuditLog : BaseEntity
{
    /// <summary>Null for anonymous actions such as a failed sign-in.</summary>
    public Guid? UserId { get; set; }

    public string? Username { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    public string? Details { get; set; }
}
