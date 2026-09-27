using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Features.Designations.Dtos;
using smartHRMS.Application.Features.Employees;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Application.Features.Designations;

public class DesignationService : IDesignationService
{
    private readonly IDesignationRepository _designationRepository;

    public DesignationService(IDesignationRepository designationRepository)
    {
        _designationRepository = designationRepository;
    }

    public async Task<List<DesignationDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var designations = await _designationRepository.GetAllAsync(cancellationToken);
        var employeeCounts = await _designationRepository.GetEmployeeCountsAsync(cancellationToken);

        return designations
            .Select(designation => MapToDto(designation, employeeCounts.GetValueOrDefault(designation.Id)))
            .ToList();
    }

    public async Task<DesignationDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var designation = await GetExistingAsync(id, cancellationToken);
        var employeeCount = await _designationRepository.CountEmployeesAsync(id, null, cancellationToken);

        return MapToDto(designation, employeeCount);
    }

    public async Task<DesignationDto> CreateAsync(CreateDesignationDto dto, CancellationToken cancellationToken)
    {
        var name = dto.Name.Trim();

        if (await _designationRepository.NameExistsAsync(name, null, cancellationToken))
        {
            throw new ConflictException($"Designation '{name}' already exists.");
        }

        var designation = new Designation
        {
            Name = name,
            Description = NormalizeDescription(dto.Description),
            IsActive = true,
        };

        await _designationRepository.AddAsync(designation, cancellationToken);
        await _designationRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(designation, 0);
    }

    public async Task<DesignationDto> UpdateAsync(Guid id, UpdateDesignationDto dto, CancellationToken cancellationToken)
    {
        var designation = await GetExistingAsync(id, cancellationToken);
        var name = dto.Name.Trim();

        if (await _designationRepository.NameExistsAsync(name, id, cancellationToken))
        {
            throw new ConflictException($"Designation '{name}' already exists.");
        }

        // Deactivating through PUT must follow the same rule as DELETE, otherwise it would be a way around it.
        var isActive = dto.IsActive!.Value;
        if (designation.IsActive && !isActive)
        {
            await EnsureNoCurrentEmployeesAsync(designation, cancellationToken);
        }

        designation.Name = name;
        designation.Description = NormalizeDescription(dto.Description);
        designation.IsActive = isActive;
        designation.UpdatedAt = DateTime.UtcNow;

        await _designationRepository.SaveChangesAsync(cancellationToken);

        var employeeCount = await _designationRepository.CountEmployeesAsync(id, null, cancellationToken);
        return MapToDto(designation, employeeCount);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var designation = await GetExistingAsync(id, cancellationToken);

        if (!designation.IsActive)
        {
            return;
        }

        await EnsureNoCurrentEmployeesAsync(designation, cancellationToken);

        designation.IsActive = false;
        designation.UpdatedAt = DateTime.UtcNow;

        await _designationRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task<Designation> GetExistingAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _designationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Designation with id '{id}' was not found.");
    }

    private async Task EnsureNoCurrentEmployeesAsync(Designation designation, CancellationToken cancellationToken)
    {
        var currentEmployees = await _designationRepository.CountEmployeesAsync(
            designation.Id, EmployeeStatusRules.CurrentStatuses, cancellationToken);

        if (currentEmployees > 0)
        {
            throw new ConflictException(
                $"Designation '{designation.Name}' cannot be deactivated because {currentEmployees} active or on-leave employee(s) are assigned to it. Reassign them first.");
        }
    }

    private static string? NormalizeDescription(string? description)
    {
        return string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    private static DesignationDto MapToDto(Designation designation, int employeeCount)
    {
        return new DesignationDto
        {
            Id = designation.Id,
            Name = designation.Name,
            Description = designation.Description,
            IsActive = designation.IsActive,
            EmployeeCount = employeeCount,
            CreatedAt = designation.CreatedAt,
            UpdatedAt = designation.UpdatedAt,
        };
    }
}
