namespace smartHRMS.Domain.Enums;

/// <summary>
/// What a signed-in user may do. Employee: own data only. Manager: an employee who also reviews the leave of their
/// direct reports. HR: people, attendance, leave and payroll administration. Admin: everything, including users,
/// payroll approval and payment.
/// </summary>
public enum UserRole
{
    Employee = 1,
    Manager = 2,
    HR = 3,
    Admin = 4
}
