using Microsoft.Extensions.DependencyInjection;
using smartHRMS.Application.Features.Attendances;
using smartHRMS.Application.Features.Departments;
using smartHRMS.Application.Features.Designations;
using smartHRMS.Application.Features.EmployeeAddresses;
using smartHRMS.Application.Features.EmployeeDocuments;
using smartHRMS.Application.Features.EmployeeEducations;
using smartHRMS.Application.Features.EmployeeEmergencyContacts;
using smartHRMS.Application.Features.EmployeeExperiences;
using smartHRMS.Application.Features.EmployeePersonalDetails;
using smartHRMS.Application.Features.Employees;

namespace smartHRMS.Application;

public static class DependencyInjection
{
    /// <param name="documentOptions">Upload rules for employee documents, read from configuration by the API.</param>
    /// <param name="attendanceOptions">Attendance time zone and late rule, read from configuration by the API.</param>
    public static IServiceCollection AddApplication(
        this IServiceCollection services, EmployeeDocumentOptions documentOptions, AttendanceOptions attendanceOptions)
    {
        // Fail at startup, not on the first upload or check-in, if the configured rules are unusable.
        EmployeeDocumentPolicy.EnsureValid(documentOptions);
        AttendanceClock.EnsureValid(attendanceOptions);
        services.AddSingleton(documentOptions);
        services.AddSingleton(attendanceOptions);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AttendanceClock>();

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

        return services;
    }
}
