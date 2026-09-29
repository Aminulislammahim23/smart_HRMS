namespace smartHRMS.Application.Features.Attendances.Dtos;

/// <summary>
/// Query-string filters. Use either <see cref="Date"/> or a <see cref="StartDate"/>/<see cref="EndDate"/> range
/// (either end may be omitted). All filters are optional and combine with AND.
/// </summary>
public class AttendanceQueryDto
{
    public DateOnly? Date { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    /// <summary>Status name (Present, Late, Absent, HalfDay, Leave), case-insensitive.</summary>
    public string? Status { get; set; }
}
