namespace smartHRMS.Domain.Enums;

/// <summary>Kinds of leave. Only <see cref="Unpaid"/> leave reduces salary; every other type is paid.</summary>
public enum LeaveType
{
    Annual = 1,
    Sick = 2,
    Casual = 3,
    Unpaid = 4
}
