using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Features.Employees.Dtos;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.Employees;

public class EmployeeService : IEmployeeService
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IDesignationRepository _designationRepository;
    private readonly IFileStorageService _fileStorageService;

    public EmployeeService(
        IEmployeeRepository employeeRepository,
        IDepartmentRepository departmentRepository,
        IDesignationRepository designationRepository,
        IFileStorageService fileStorageService)
    {
        _employeeRepository = employeeRepository;
        _departmentRepository = departmentRepository;
        _designationRepository = designationRepository;
        _fileStorageService = fileStorageService;
    }

    public async Task<List<EmployeeDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var employees = await _employeeRepository.GetAllAsync(cancellationToken);
        return employees.Select(MapToDto).ToList();
    }

    public async Task<EmployeeDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Employee with id '{id}' was not found.");

        return MapToDto(employee);
    }

    public async Task<EmployeeDto> CreateAsync(CreateEmployeeDto dto, CancellationToken cancellationToken)
    {
        // Trim before the uniqueness checks, otherwise " EMP-001" or " a@b.com" slips past them as a different value.
        var employeeCode = dto.EmployeeCode.Trim();
        var email = dto.Email.Trim();

        await ValidateDepartmentAndDesignationAsync(dto.DepartmentId, dto.DesignationId, null, cancellationToken);

        if (await _employeeRepository.EmployeeCodeExistsAsync(employeeCode, null, cancellationToken))
        {
            throw new ConflictException($"Employee code '{employeeCode}' is already in use.");
        }

        if (await _employeeRepository.EmailExistsAsync(email, null, cancellationToken))
        {
            throw new ConflictException($"Email '{email}' is already in use.");
        }

        var employee = new Employee
        {
            EmployeeCode = employeeCode,
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Email = email,
            Phone = dto.Phone,
            Address = NormalizeOptional(dto.Address),
            Gender = dto.Gender,
            DateOfBirth = dto.DateOfBirth,
            JoiningDate = dto.JoiningDate,
            DepartmentId = dto.DepartmentId,
            DesignationId = dto.DesignationId,
            EmploymentType = dto.EmploymentType ?? EmploymentType.FullTime,
            Status = EmployeeStatus.Active,
        };

        await _employeeRepository.AddAsync(employee, cancellationToken);
        await _employeeRepository.SaveChangesAsync(cancellationToken);

        var created = await _employeeRepository.GetByIdAsync(employee.Id, cancellationToken)
            ?? throw new NotFoundException($"Employee with id '{employee.Id}' was not found.");

        return MapToDto(created);
    }

    public async Task<EmployeeDto> UpdateAsync(Guid id, UpdateEmployeeDto dto, CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Employee with id '{id}' was not found.");

        var email = dto.Email.Trim();

        var newStatus = dto.Status ?? employee.Status;

        // Bringing a former employee back (to Active/OnLeave) is a fresh assignment, so the department and designation
        // must be active again. Otherwise only a changed department/designation is re-checked for being active.
        var isReactivation = EmployeeStatusRules.CurrentStatuses.Contains(newStatus)
            && !EmployeeStatusRules.CurrentStatuses.Contains(employee.Status);

        await ValidateDepartmentAndDesignationAsync(dto.DepartmentId, dto.DesignationId, isReactivation ? null : employee, cancellationToken);

        if (await _employeeRepository.EmailExistsAsync(email, id, cancellationToken))
        {
            throw new ConflictException($"Email '{email}' is already in use.");
        }

        employee.FirstName = dto.FirstName.Trim();
        employee.LastName = dto.LastName.Trim();
        employee.Email = email;
        employee.Phone = dto.Phone;
        employee.Address = NormalizeOptional(dto.Address);
        employee.Gender = dto.Gender;
        employee.DateOfBirth = dto.DateOfBirth;
        employee.JoiningDate = dto.JoiningDate;
        employee.DepartmentId = dto.DepartmentId;
        employee.DesignationId = dto.DesignationId;
        employee.EmploymentType = dto.EmploymentType ?? employee.EmploymentType;
        employee.Status = newStatus;
        employee.UpdatedAt = DateTime.UtcNow;

        await _employeeRepository.SaveChangesAsync(cancellationToken);

        var updated = await _employeeRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Employee with id '{id}' was not found.");

        return MapToDto(updated);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Employee with id '{id}' was not found.");

        employee.Status = EmployeeStatus.Inactive;
        employee.UpdatedAt = DateTime.UtcNow;

        await _employeeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<EmployeeDto> UploadPhotoAsync(Guid id, UploadEmployeePhotoDto photo, CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Employee with id '{id}' was not found.");

        if (photo.Length <= 0)
        {
            throw new BadRequestException("The uploaded photo is empty.");
        }

        if (photo.Length > EmployeePhotoPolicy.MaxFileSizeBytes)
        {
            throw new BadRequestException($"The photo exceeds the maximum allowed size of {EmployeePhotoPolicy.MaxFileSizeBytes / (1024 * 1024)} MB.");
        }

        var extension = Path.GetExtension(photo.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !EmployeePhotoPolicy.AllowedExtensions.Contains(extension))
        {
            throw new BadRequestException("Only .jpg, .jpeg, .png and .webp photos are allowed.");
        }

        if (string.IsNullOrWhiteSpace(photo.ContentType) || !EmployeePhotoPolicy.AllowedContentTypes.Contains(photo.ContentType))
        {
            throw new BadRequestException("The uploaded file is not a recognized image type.");
        }

        if (!await EmployeePhotoPolicy.MatchesImageSignatureAsync(photo.Content, extension, cancellationToken))
        {
            throw new BadRequestException("The file content does not match a supported image format.");
        }

        // Never trust the client-supplied file name: build a safe name from the employee id instead.
        var safeFileName = $"{employee.Id:N}{extension.ToLowerInvariant()}";
        var previousPhotoUrl = employee.PhotoUrl;

        // Save the new file before touching the database, so a failed upload never clears the existing photo.
        var newPhotoUrl = await _fileStorageService.SaveAsync(
            photo.Content, safeFileName, EmployeePhotoPolicy.StorageSubFolder, cancellationToken);

        employee.PhotoUrl = newPhotoUrl;
        employee.UpdatedAt = DateTime.UtcNow;
        await _employeeRepository.SaveChangesAsync(cancellationToken);

        // Only remove the old file once the new one is safely stored and the database update has committed.
        if (!string.IsNullOrWhiteSpace(previousPhotoUrl) &&
            !string.Equals(previousPhotoUrl, newPhotoUrl, StringComparison.OrdinalIgnoreCase))
        {
            await _fileStorageService.DeleteAsync(previousPhotoUrl, cancellationToken);
        }

        return MapToDto(employee);
    }

    public async Task<EmployeeDto> RemovePhotoAsync(Guid id, CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Employee with id '{id}' was not found.");

        if (string.IsNullOrWhiteSpace(employee.PhotoUrl))
        {
            return MapToDto(employee);
        }

        var photoUrl = employee.PhotoUrl;
        employee.PhotoUrl = null;
        employee.UpdatedAt = DateTime.UtcNow;
        await _employeeRepository.SaveChangesAsync(cancellationToken);

        await _fileStorageService.DeleteAsync(photoUrl, cancellationToken);

        return MapToDto(employee);
    }

    /// <summary>
    /// Both references must exist. A new assignment must also be active; an employee who already belongs to a
    /// since-deactivated department/designation (<paramref name="currentEmployee"/>) can still have other details edited.
    /// </summary>
    private async Task ValidateDepartmentAndDesignationAsync(Guid departmentId, Guid designationId, Employee? currentEmployee, CancellationToken cancellationToken)
    {
        var department = await _departmentRepository.GetByIdAsync(departmentId, cancellationToken)
            ?? throw new BadRequestException($"Department with id '{departmentId}' does not exist.");

        if (!department.IsActive && departmentId != currentEmployee?.DepartmentId)
        {
            throw new BadRequestException($"Department '{department.Name}' is inactive and cannot be assigned to employees.");
        }

        var designation = await _designationRepository.GetByIdAsync(designationId, cancellationToken)
            ?? throw new BadRequestException($"Designation with id '{designationId}' does not exist.");

        if (!designation.IsActive && designationId != currentEmployee?.DesignationId)
        {
            throw new BadRequestException($"Designation '{designation.Name}' is inactive and cannot be assigned to employees.");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static EmployeeDto MapToDto(Employee employee)
    {
        return new EmployeeDto
        {
            Id = employee.Id,
            EmployeeCode = employee.EmployeeCode,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            FullName = $"{employee.FirstName} {employee.LastName}".Trim(),
            Email = employee.Email,
            Phone = employee.Phone,
            Address = employee.Address,
            Gender = employee.Gender?.ToString(),
            DateOfBirth = employee.DateOfBirth,
            JoiningDate = employee.JoiningDate,
            DepartmentId = employee.DepartmentId,
            DepartmentName = employee.Department?.Name,
            DesignationId = employee.DesignationId,
            DesignationName = employee.Designation?.Name,
            EmploymentType = employee.EmploymentType.ToString(),
            Status = employee.Status.ToString(),
            IsActive = EmployeeStatusRules.CurrentStatuses.Contains(employee.Status),
            PhotoUrl = employee.PhotoUrl,
            CreatedAt = employee.CreatedAt,
            UpdatedAt = employee.UpdatedAt,
        };
    }
}
