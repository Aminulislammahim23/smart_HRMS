using Microsoft.Extensions.DependencyInjection;
using smartHRMS.Application.Features.Departments;
using smartHRMS.Application.Features.Designations;
using smartHRMS.Application.Features.Employees;

namespace smartHRMS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IDesignationService, DesignationService>();

        return services;
    }
}
