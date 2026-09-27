using smartHRMS.Application.Features.Departments.Dtos;

namespace smartHRMS.Application.Features.Departments;

public interface IDepartmentService
{
    Task<List<DepartmentDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<DepartmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<DepartmentDto> CreateAsync(CreateDepartmentDto dto, CancellationToken cancellationToken);

    Task<DepartmentDto> UpdateAsync(Guid id, UpdateDepartmentDto dto, CancellationToken cancellationToken);

    /// <summary>Soft delete: sets IsActive=false, never removes the row. Throws ConflictException while current employees are assigned.</summary>
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken);
}
