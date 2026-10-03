using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Common.Money;
using smartHRMS.Application.Common.Security;
using smartHRMS.Application.Features.Payroll.Dtos;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Application.Features.Payroll;

public interface ISalaryStructureService
{
    /// <summary>Salary of every employee (HR/Admin).</summary>
    Task<List<SalaryStructureDto>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>One employee's salary (HR/Admin, or the employee themself).</summary>
    Task<SalaryStructureDto> GetAsync(Guid employeeId, CancellationToken cancellationToken);

    /// <summary>Replaces basic salary, allowances and tax (HR/Admin). Amounts are rounded to 2 decimals.</summary>
    Task<SalaryStructureDto> UpdateAsync(Guid employeeId, UpdateSalaryStructureDto dto, CancellationToken cancellationToken);
}

/// <summary>
/// The basic salary stays on the employee record (one place); allowances and tax live in
/// <see cref="EmployeeSalaryStructure"/>. Changes affect payroll from the next calculation; already calculated or
/// approved records keep their amounts.
/// </summary>
public class SalaryStructureService : ISalaryStructureService
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IEmployeeOwnedRepository<EmployeeSalaryStructure> _structureRepository;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;

    public SalaryStructureService(
        IEmployeeRepository employeeRepository,
        IEmployeeOwnedRepository<EmployeeSalaryStructure> structureRepository,
        IAuditLogger auditLogger,
        ICurrentUser currentUser)
    {
        _employeeRepository = employeeRepository;
        _structureRepository = structureRepository;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
    }

    public async Task<List<SalaryStructureDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        _currentUser.EnsureHrOrAdmin();

        var employees = await _employeeRepository.GetAllAsync(cancellationToken);
        var structures = (await _structureRepository.GetAllAsync(cancellationToken)).ToDictionary(s => s.EmployeeId);

        return employees.Select(employee => MapToDto(employee, structures.GetValueOrDefault(employee.Id))).ToList();
    }

    public async Task<SalaryStructureDto> GetAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        _currentUser.EnsureSelfOrHrOrAdmin(employeeId);

        var employee = await GetEmployeeAsync(employeeId, cancellationToken);
        var structure = (await _structureRepository.GetByEmployeeIdAsync(employee.Id, cancellationToken)).FirstOrDefault();
        return MapToDto(employee, structure);
    }

    public async Task<SalaryStructureDto> UpdateAsync(Guid employeeId, UpdateSalaryStructureDto dto, CancellationToken cancellationToken)
    {
        _currentUser.EnsureHrOrAdmin();

        var employee = await GetEmployeeAsync(employeeId, cancellationToken);
        var structure = (await _structureRepository.GetByEmployeeIdAsync(employee.Id, cancellationToken)).FirstOrDefault();

        if (structure is null)
        {
            structure = new EmployeeSalaryStructure { EmployeeId = employee.Id };
            await _structureRepository.AddAsync(structure, cancellationToken);
        }
        else
        {
            structure.UpdatedAt = DateTime.UtcNow;
        }

        employee.BasicSalary = Money.Round(dto.BasicSalary!.Value);
        employee.UpdatedAt = DateTime.UtcNow;
        structure.HouseRent = Money.Round(dto.HouseRent);
        structure.MedicalAllowance = Money.Round(dto.MedicalAllowance);
        structure.TransportAllowance = Money.Round(dto.TransportAllowance);
        structure.OtherAllowance = Money.Round(dto.OtherAllowance);
        structure.MonthlyTax = Money.Round(dto.MonthlyTax);
        structure.MonthlyProvidentFund = Money.Round(dto.MonthlyProvidentFund);

        // The audit entry records that the salary changed, not the amounts (salary data stays out of the log).
        _auditLogger.Add(AuditActions.SalaryStructureUpdated, nameof(EmployeeSalaryStructure), employee.Id, $"Salary structure of {employee.EmployeeCode} updated.");
        await _structureRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(employee, structure);
    }

    private async Task<Employee> GetEmployeeAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        return await _employeeRepository.GetByIdAsync(employeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee with id '{employeeId}' was not found.");
    }

    private static SalaryStructureDto MapToDto(Employee employee, EmployeeSalaryStructure? structure)
    {
        var allowances = (structure?.HouseRent ?? 0) + (structure?.MedicalAllowance ?? 0) + (structure?.TransportAllowance ?? 0) + (structure?.OtherAllowance ?? 0);

        return new SalaryStructureDto
        {
            EmployeeId = employee.Id,
            EmployeeCode = employee.EmployeeCode,
            EmployeeName = $"{employee.FirstName} {employee.LastName}".Trim(),
            DepartmentName = employee.Department?.Name,
            DesignationName = employee.Designation?.Name,
            EmployeeStatus = employee.Status.ToString(),
            BasicSalary = employee.BasicSalary,
            HouseRent = structure?.HouseRent ?? 0,
            MedicalAllowance = structure?.MedicalAllowance ?? 0,
            TransportAllowance = structure?.TransportAllowance ?? 0,
            OtherAllowance = structure?.OtherAllowance ?? 0,
            MonthlyTax = structure?.MonthlyTax ?? 0,
            MonthlyProvidentFund = structure?.MonthlyProvidentFund ?? 0,
            MonthlyGross = employee.BasicSalary is { } basic ? basic + allowances : null,
            UpdatedAt = structure?.UpdatedAt ?? structure?.CreatedAt,
        };
    }
}
