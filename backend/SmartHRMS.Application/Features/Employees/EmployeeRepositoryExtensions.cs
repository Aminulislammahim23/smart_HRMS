using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Interfaces;

namespace smartHRMS.Application.Features.Employees;

internal static class EmployeeRepositoryExtensions
{
    /// <summary>Throws <see cref="NotFoundException"/> (404) unless the employee exists.</summary>
    public static async Task EnsureExistsAsync(this IEmployeeRepository repository, Guid employeeId, CancellationToken cancellationToken)
    {
        if (!await repository.ExistsAsync(employeeId, cancellationToken))
        {
            throw new NotFoundException($"Employee with id '{employeeId}' was not found.");
        }
    }
}
