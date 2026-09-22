using Microsoft.Extensions.DependencyInjection;
using smartHRMS.Application.Features.Employees;

namespace smartHRMS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IEmployeeService, EmployeeService>();

        return services;
    }
}
