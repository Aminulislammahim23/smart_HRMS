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
        await ValidateDepartmentAndDesignationAsync(dto.DepartmentId, dto.DesignationId, cancellationToken);

        if (await _employeeRepository.EmployeeCodeExistsAsync(dto.EmployeeCode, null, cancellationToken))
        {
            throw new ConflictException($"Employee code '{dto.EmployeeCode}' is already in use.");
        }

        if (await _employeeRepository.EmailExistsAsync(dto.Email, null, cancellationToken))
        {
            throw new ConflictException($"Email '{dto.Email}' is already in use.");
        }

        var employee = new Employee
        {
            EmployeeCode = dto.EmployeeCode,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            Phone = dto.Phone,
            DateOfBirth = dto.DateOfBirth,
            JoiningDate = dto.JoiningDate,
            DepartmentId = dto.DepartmentId,
            DesignationId = dto.DesignationId,
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

        await ValidateDepartmentAndDesignationAsync(dto.DepartmentId, dto.DesignationId, cancellationToken);

        if (await _employeeRepository.EmailExistsAsync(dto.Email, id, cancellationToken))
        {
            throw new ConflictException($"Email '{dto.Email}' is already in use.");
        }

        employee.FirstName = dto.FirstName;
        employee.LastName = dto.LastName;
        employee.Email = dto.Email;
        employee.Phone = dto.Phone;
        employee.DateOfBirth = dto.DateOfBirth;
        employee.JoiningDate = dto.JoiningDate;
        employee.DepartmentId = dto.DepartmentId;
        employee.DesignationId = dto.DesignationId;
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

    private async Task ValidateDepartmentAndDesignationAsync(Guid departmentId, Guid designationId, CancellationToken cancellationToken)
    {
        if (!await _departmentRepository.ExistsAsync(departmentId, cancellationToken))
        {
            throw new BadRequestException($"Department with id '{departmentId}' does not exist.");
        }

        if (!await _designationRepository.ExistsAsync(designationId, cancellationToken))
        {
            throw new BadRequestException($"Designation with id '{designationId}' does not exist.");
        }
    }

    private static EmployeeDto MapToDto(Employee employee)
    {
        return new EmployeeDto
        {
            Id = employee.Id,
            EmployeeCode = employee.EmployeeCode,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            Email = employee.Email,
            Phone = employee.Phone,
            DateOfBirth = employee.DateOfBirth,
            JoiningDate = employee.JoiningDate,
            DepartmentId = employee.DepartmentId,
            DepartmentName = employee.Department?.Name,
            DesignationId = employee.DesignationId,
            DesignationName = employee.Designation?.Name,
            Status = employee.Status.ToString(),
            PhotoUrl = employee.PhotoUrl,
            CreatedAt = employee.CreatedAt,
            UpdatedAt = employee.UpdatedAt,
        };
    }
}
