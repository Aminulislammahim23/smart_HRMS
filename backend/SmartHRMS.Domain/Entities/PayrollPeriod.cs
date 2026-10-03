using smartHRMS.Domain.Common;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Domain.Entities;

/// <summary>
/// One payroll run over a date range (normally a calendar month). Periods that are not Cancelled never overlap.
/// Every status change records who made it and when.
/// </summary>
public class PayrollPeriod : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public PayrollPeriodStatus Status { get; set; } = PayrollPeriodStatus.Draft;

    public string? Notes { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTime? CalculatedAt { get; set; }

    public Guid? CalculatedByUserId { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public Guid? SubmittedByUserId { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    public DateTime? PaidAt { get; set; }

    public Guid? PaidByUserId { get; set; }

    public DateTime? CancelledAt { get; set; }

    public Guid? CancelledByUserId { get; set; }

    /// <summary>Optimistic concurrency: two simultaneous status changes can't both succeed.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<PayrollRecord> Records { get; set; } = new List<PayrollRecord>();
}
