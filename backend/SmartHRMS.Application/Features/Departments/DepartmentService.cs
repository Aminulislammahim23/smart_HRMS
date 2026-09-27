using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Features.Departments.Dtos;
using smartHRMS.Application.Features.Employees;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Application.Features.Departments;

public class DepartmentService : IDepartmentService
{
    private readonly IDepartmentRepository _departmentRepository;

    public DepartmentService(IDepartmentRepository departmentRepository)
    {
        _departmentRepository = departmentRepository;
    }

    public async Task<List<DepartmentDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var departments = await _departmentRepository.GetAllAsync(cancellationToken);
        var employeeCounts = await _departmentRepository.GetEmployeeCountsAsync(cancellationToken);

        return departments
            .Select(department => MapToDto(department, employeeCounts.GetValueOrDefault(department.Id)))
            .ToList();
    }

    public async Task<DepartmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var department = await GetExistingAsync(id, cancellationToken);
        var employeeCount = await _departmentRepository.CountEmployeesAsync(id, null, cancellationToken);

        return MapToDto(department, employeeCount);
    }

    public async Task<DepartmentDto> CreateAsync(CreateDepartmentDto dto, CancellationToken cancellationToken)
    {
        var name = dto.Name.Trim();

        if (await _departmentRepository.NameExistsAsync(name, null, cancellationToken))
        {
            throw new ConflictException($"Department '{name}' already exists.");
        }

        var department = new Department
        {
            Name = name,
            Description = NormalizeDescription(dto.Description),
            IsActive = true,
        };

        await _departmentRepository.AddAsync(department, cancellationToken);
        await _departmentRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(department, 0);
    }

    public async Task<DepartmentDto> UpdateAsync(Guid id, UpdateDepartmentDto dto, CancellationToken cancellationToken)
    {
        var department = await GetExistingAsync(id, cancellationToken);
        var name = dto.Name.Trim();

        if (await _departmentRepository.NameExistsAsync(name, id, cancellationToken))
        {
            throw new ConflictException($"Department '{name}' already exists.");
        }

        // Deactivating through PUT must follow the same rule as DELETE, otherwise it would be a way around it.
        var isActive = dto.IsActive!.Value;
        if (department.IsActive && !isActive)
        {
            await EnsureNoCurrentEmployeesAsync(department, cancellationToken);
        }

        department.Name = name;
        department.Description = NormalizeDescription(dto.Description);
        department.IsActive = isActive;
        department.UpdatedAt = DateTime.UtcNow;

        await _departmentRepository.SaveChangesAsync(cancellationToken);

        var employeeCount = await _departmentRepository.CountEmployeesAsync(id, null, cancellationToken);
        return MapToDto(department, employeeCount);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var department = await GetExistingAsync(id, cancellationToken);

        if (!department.IsActive)
        {
            return;
        }

        await EnsureNoCurrentEmployeesAsync(department, cancellationToken);

        department.IsActive = false;
        department.UpdatedAt = DateTime.UtcNow;

        await _departmentRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task<Department> GetExistingAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _departmentRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Department with id '{id}' was not found.");
    }

    private async Task EnsureNoCurrentEmployeesAsync(Department department, CancellationToken cancellationToken)
    {
        var currentEmployees = await _departmentRepository.CountEmployeesAsync(
            department.Id, EmployeeStatusRules.CurrentStatuses, cancellationToken);

        if (currentEmployees > 0)
        {
            throw new ConflictException(
                $"Department '{department.Name}' cannot be deactivated because {currentEmployees} active or on-leave employee(s) are assigned to it. Reassign them first.");
        }
    }

    private static string? NormalizeDescription(string? description)
    {
        return string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    private static DepartmentDto MapToDto(Department department, int employeeCount)
    {
        return new DepartmentDto
        {
            Id = department.Id,
            Name = department.Name,
            Description = department.Description,
            IsActive = department.IsActive,
            EmployeeCount = employeeCount,
            CreatedAt = department.CreatedAt,
            UpdatedAt = department.UpdatedAt,
        };
    }
}
