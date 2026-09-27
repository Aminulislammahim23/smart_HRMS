using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Common.Text;
using smartHRMS.Application.Features.EmployeePersonalDetails.Dtos;
using smartHRMS.Application.Features.Employees;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using PersonalDetails = smartHRMS.Domain.Entities.EmployeePersonalDetails;

namespace smartHRMS.Application.Features.EmployeePersonalDetails;

public class EmployeePersonalDetailsService : IEmployeePersonalDetailsService
{
    private readonly IEmployeeOwnedRepository<PersonalDetails> _repository;
    private readonly IEmployeeRepository _employeeRepository;

    public EmployeePersonalDetailsService(IEmployeeOwnedRepository<PersonalDetails> repository, IEmployeeRepository employeeRepository)
    {
        _repository = repository;
        _employeeRepository = employeeRepository;
    }

    public async Task<EmployeePersonalDetailsDto> GetAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await GetEmployeeAsync(employeeId, cancellationToken);
        var details = await FindAsync(employeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee '{employeeId}' has no personal details yet.");

        return MapToDto(details, employee.DateOfBirth);
    }

    public async Task<EmployeePersonalDetailsDto> CreateAsync(Guid employeeId, CreateEmployeePersonalDetailsDto dto, CancellationToken cancellationToken)
    {
        var employee = await GetEmployeeAsync(employeeId, cancellationToken);

        if (await FindAsync(employeeId, cancellationToken) is not null)
        {
            throw new ConflictException($"Employee '{employeeId}' already has personal details. Use PUT to update them.");
        }

        var details = new PersonalDetails { EmployeeId = employeeId };
        await ApplyAsync(details, dto, cancellationToken);

        await _repository.AddAsync(details, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return MapToDto(details, employee.DateOfBirth);
    }

    public async Task<EmployeePersonalDetailsDto> UpdateAsync(Guid employeeId, UpdateEmployeePersonalDetailsDto dto, CancellationToken cancellationToken)
    {
        var employee = await GetEmployeeAsync(employeeId, cancellationToken);
        var details = await FindAsync(employeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee '{employeeId}' has no personal details yet. Use POST to create them.");

        await ApplyAsync(details, dto, cancellationToken);
        details.UpdatedAt = DateTime.UtcNow;

        await _repository.SaveChangesAsync(cancellationToken);

        return MapToDto(details, employee.DateOfBirth);
    }

    public async Task DeleteAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        await _employeeRepository.EnsureExistsAsync(employeeId, cancellationToken);
        var details = await FindAsync(employeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee '{employeeId}' has no personal details.");

        _repository.Remove(details);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    internal static EmployeePersonalDetailsDto MapToDto(PersonalDetails details, DateTime dateOfBirth)
    {
        return new EmployeePersonalDetailsDto
        {
            Id = details.Id,
            EmployeeId = details.EmployeeId,
            DateOfBirth = dateOfBirth,
            Gender = details.Gender?.ToString(),
            MaritalStatus = details.MaritalStatus?.ToString(),
            BloodGroup = details.BloodGroup?.ToString(),
            Nationality = details.Nationality,
            NationalId = details.NationalId,
            PassportNo = details.PassportNo,
            CreatedAt = details.CreatedAt,
            UpdatedAt = details.UpdatedAt,
        };
    }

    private async Task ApplyAsync(PersonalDetails details, CreateEmployeePersonalDetailsDto dto, CancellationToken cancellationToken)
    {
        var nationalId = InputText.Optional(dto.NationalId);
        var passportNo = InputText.Optional(dto.PassportNo)?.ToUpperInvariant();

        // The same NID/passport can't belong to two people. Checked here for a clear message; a unique index backs it up.
        if (nationalId is not null
            && await _repository.AnyAsync(p => p.NationalId == nationalId && p.Id != details.Id, cancellationToken))
        {
            throw new ConflictException($"National ID '{nationalId}' is already registered to another employee.");
        }

        if (passportNo is not null
            && await _repository.AnyAsync(p => p.PassportNo == passportNo && p.Id != details.Id, cancellationToken))
        {
            throw new ConflictException($"Passport number '{passportNo}' is already registered to another employee.");
        }

        details.Gender = dto.Gender;
        details.MaritalStatus = dto.MaritalStatus;
        details.BloodGroup = dto.BloodGroup;
        details.Nationality = InputText.Optional(dto.Nationality);
        details.NationalId = nationalId;
        details.PassportNo = passportNo;
    }

    private async Task<Employee> GetEmployeeAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        return await _employeeRepository.GetByIdAsync(employeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee with id '{employeeId}' was not found.");
    }

    private async Task<PersonalDetails?> FindAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        return (await _repository.GetByEmployeeIdAsync(employeeId, cancellationToken)).FirstOrDefault();
    }
}
