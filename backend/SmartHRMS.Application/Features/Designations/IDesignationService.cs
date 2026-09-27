using smartHRMS.Application.Features.Designations.Dtos;

namespace smartHRMS.Application.Features.Designations;

public interface IDesignationService
{
    Task<List<DesignationDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<DesignationDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<DesignationDto> CreateAsync(CreateDesignationDto dto, CancellationToken cancellationToken);

    Task<DesignationDto> UpdateAsync(Guid id, UpdateDesignationDto dto, CancellationToken cancellationToken);

    /// <summary>Soft delete: sets IsActive=false, never removes the row. Throws ConflictException while current employees are assigned.</summary>
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken);
}
