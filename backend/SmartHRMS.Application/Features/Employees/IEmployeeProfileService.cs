using smartHRMS.Application.Features.Employees.Dtos;

namespace smartHRMS.Application.Features.Employees;

public interface IEmployeeProfileService
{
    /// <summary>The full profile (personal, job, department/designation, photo, active documents). 404 if the employee doesn't exist.</summary>
    Task<EmployeeProfileDto> GetProfileAsync(Guid employeeId, CancellationToken cancellationToken);
}
