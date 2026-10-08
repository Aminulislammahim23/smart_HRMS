using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using smartHRMS.Application.Interfaces;
using smartHRMS.Infrastructure.Persistence;
using smartHRMS.Infrastructure.Repositories;
using smartHRMS.Infrastructure.Security;
using smartHRMS.Infrastructure.Storage;

namespace smartHRMS.Infrastructure;

public static class DependencyInjection
{
    /// <param name="fileStorageRootPath">Public root for profile photos (served as static files).</param>
    /// <param name="documentStorageRootPath">Private root for employee documents. Must not be under the public root.</param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, string connectionString, string fileStorageRootPath, string documentStorageRootPath)
    {
        services.AddDbContext<SmartHRMSDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IDesignationRepository, DesignationRepository>();
        services.AddScoped<IEmployeeDocumentRepository, EmployeeDocumentRepository>();
        services.AddScoped(typeof(IEmployeeOwnedRepository<>), typeof(EmployeeOwnedRepository<>));
        services.AddScoped<IAttendanceRepository, AttendanceRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ILeaveRequestRepository, LeaveRequestRepository>();
        services.AddScoped<IPayrollRepository, PayrollRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPayrollReportRepository, PayrollReportRepository>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<IAuditLogReader, AuditLogReader>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

        services.AddSingleton<IFileStorageService>(new LocalFileStorageService(fileStorageRootPath));
        services.AddSingleton<IDocumentStorageService>(new LocalDocumentStorageService(documentStorageRootPath));

        return services;
    }
}
