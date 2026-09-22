using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using smartHRMS.Application.Interfaces;
using smartHRMS.Infrastructure.Persistence;
using smartHRMS.Infrastructure.Repositories;
using smartHRMS.Infrastructure.Storage;

namespace smartHRMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString, string fileStorageRootPath)
    {
        services.AddDbContext<SmartHRMSDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IDesignationRepository, DesignationRepository>();

        services.AddSingleton<IFileStorageService>(new LocalFileStorageService(fileStorageRootPath));

        return services;
    }
}
