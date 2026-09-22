using smartHRMS.Application.Features.Employees.Dtos;

namespace smartHRMS.Application.Features.Employees;

public interface IEmployeeService
{
    Task<List<EmployeeDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<EmployeeDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<EmployeeDto> CreateAsync(CreateEmployeeDto dto, CancellationToken cancellationToken);

    Task<EmployeeDto> UpdateAsync(Guid id, UpdateEmployeeDto dto, CancellationToken cancellationToken);

    Task DeactivateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Validates and stores a new profile photo, replacing any existing one. Throws NotFoundException if the employee doesn't exist, or BadRequestException if the file fails validation.</summary>
    Task<EmployeeDto> UploadPhotoAsync(Guid id, UploadEmployeePhotoDto photo, CancellationToken cancellationToken);

    /// <summary>Removes the employee's profile photo, if any. Idempotent — succeeds even if no photo is set.</summary>
    Task<EmployeeDto> RemovePhotoAsync(Guid id, CancellationToken cancellationToken);
}
