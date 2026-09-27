using smartHRMS.Application.Features.EmployeePersonalDetails.Dtos;

namespace smartHRMS.Application.Features.EmployeePersonalDetails;

public interface IEmployeePersonalDetailsService
{
    Task<EmployeePersonalDetailsDto> GetAsync(Guid employeeId, CancellationToken cancellationToken);

    /// <summary>409 if the employee already has personal details (use PUT to change them).</summary>
    Task<EmployeePersonalDetailsDto> CreateAsync(Guid employeeId, CreateEmployeePersonalDetailsDto dto, CancellationToken cancellationToken);

    Task<EmployeePersonalDetailsDto> UpdateAsync(Guid employeeId, UpdateEmployeePersonalDetailsDto dto, CancellationToken cancellationToken);

    Task DeleteAsync(Guid employeeId, CancellationToken cancellationToken);
}
