using Microsoft.Extensions.DependencyInjection;
using smartHRMS.Application.Common.Calendar;
using smartHRMS.Application.Features.Attendances;
using smartHRMS.Application.Features.Audit;
using smartHRMS.Application.Features.Auth;
using smartHRMS.Application.Features.Departments;
using smartHRMS.Application.Features.Designations;
using smartHRMS.Application.Features.EmployeeAddresses;
using smartHRMS.Application.Features.EmployeeDocuments;
using smartHRMS.Application.Features.EmployeeEducations;
using smartHRMS.Application.Features.EmployeeEmergencyContacts;
using smartHRMS.Application.Features.EmployeeExperiences;
using smartHRMS.Application.Features.EmployeePersonalDetails;
using smartHRMS.Application.Features.Employees;
using smartHRMS.Application.Features.Leaves;
using smartHRMS.Application.Features.Payments;
using smartHRMS.Application.Features.Payroll;
using smartHRMS.Application.Features.Users;

namespace smartHRMS.Application;

public static class DependencyInjection
{
    /// <param name="documentOptions">Upload rules for employee documents, read from configuration by the API.</param>
    /// <param name="attendanceOptions">Attendance time zone and late rule, read from configuration by the API.</param>
    /// <param name="workCalendarOptions">Weekly days off, used to count leave and payroll working days.</param>
    /// <param name="payrollOptions">Company details for payslips and payroll rules.</param>
    /// <param name="authOptions">Sign-in lockout rules.</param>
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        EmployeeDocumentOptions documentOptions,
        AttendanceOptions attendanceOptions,
        WorkCalendarOptions workCalendarOptions,
        PayrollOptions payrollOptions,
        AuthOptions authOptions)
    {
        // Fail at startup, not on the first upload or check-in, if the configured rules are unusable.
        EmployeeDocumentPolicy.EnsureValid(documentOptions);
        AttendanceClock.EnsureValid(attendanceOptions);
        WorkCalendar.EnsureValid(workCalendarOptions);
        PayrollOptions.EnsureValid(payrollOptions);
        AuthOptions.EnsureValid(authOptions);
        services.AddSingleton(documentOptions);
        services.AddSingleton(attendanceOptions);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AttendanceClock>();
        services.AddSingleton(workCalendarOptions);
        services.AddSingleton<WorkCalendar>();
        services.AddSingleton(payrollOptions);
        services.AddSingleton(authOptions);

        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IEmployeeProfileService, EmployeeProfileService>();
        services.AddScoped<IEmployeeDocumentService, EmployeeDocumentService>();
        services.AddScoped<IEmployeePersonalDetailsService, EmployeePersonalDetailsService>();
        services.AddScoped<IEmployeeAddressService, EmployeeAddressService>();
        services.AddScoped<IEmployeeEmergencyContactService, EmployeeEmergencyContactService>();
        services.AddScoped<IEmployeeEducationService, EmployeeEducationService>();
        services.AddScoped<IEmployeeExperienceService, EmployeeExperienceService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IDesignationService, DesignationService>();
        services.AddScoped<IAttendanceService, AttendanceService>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IEmployeeAccess, EmployeeAccess>();
        services.AddScoped<IEmployeeManagerService, EmployeeManagerService>();
        services.AddScoped<ILeaveService, LeaveService>();
        services.AddScoped<ISalaryStructureService, SalaryStructureService>();
        services.AddScoped<IPayrollService, PayrollService>();
        services.AddScoped<IPayslipService, PayslipService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        return services;
    }
}
