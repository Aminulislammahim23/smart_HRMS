using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Features.Employees;
using smartHRMS.Application.Features.Employees.Dtos;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;
using smartHRMS.Tests.Fakes;
using Xunit;

namespace smartHRMS.Tests.Features.Employees;

public class EmployeeServiceTests
{
    private static readonly Guid DepartmentId = Guid.NewGuid();
    private static readonly Guid DesignationId = Guid.NewGuid();

    private static (EmployeeService Service, FakeEmployeeRepository EmployeeRepository, FakeDepartmentRepository DepartmentRepository, FakeDesignationRepository DesignationRepository, FakeFileStorageService FileStorageService) CreateService()
    {
        var employeeRepository = new FakeEmployeeRepository();
        var departmentRepository = new FakeDepartmentRepository();
        var designationRepository = new FakeDesignationRepository();
        departmentRepository.Departments.Add(new Department { Id = DepartmentId, Name = "Engineering" });
        designationRepository.Designations.Add(new Designation { Id = DesignationId, Name = "Software Engineer" });
        var fileStorageService = new FakeFileStorageService();
        var service = new EmployeeService(employeeRepository, departmentRepository, designationRepository, fileStorageService);

        return (service, employeeRepository, departmentRepository, designationRepository, fileStorageService);
    }

    private static CreateEmployeeDto ValidCreateDto() => new()
    {
        EmployeeCode = "EMP-001",
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane.doe@example.com",
        DateOfBirth = new DateTime(1990, 1, 1),
        JoiningDate = new DateTime(2024, 1, 1),
        DepartmentId = DepartmentId,
        DesignationId = DesignationId,
    };

    private static UpdateEmployeeDto ValidUpdateDto() => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane.doe@example.com",
        DateOfBirth = new DateTime(1990, 1, 1),
        JoiningDate = new DateTime(2024, 1, 1),
        DepartmentId = DepartmentId,
        DesignationId = DesignationId,
    };

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesEmployeeWithActiveStatus()
    {
        var (service, _, _, _, _) = CreateService();

        var result = await service.CreateAsync(ValidCreateDto(), CancellationToken.None);

        Assert.Equal(EmployeeStatus.Active.ToString(), result.Status);
        Assert.Equal("EMP-001", result.EmployeeCode);
    }

    [Fact]
    public async Task CreateAsync_WhenDepartmentDoesNotExist_ThrowsBadRequestException()
    {
        var (service, _, _, _, _) = CreateService();
        var dto = ValidCreateDto();
        dto.DepartmentId = Guid.NewGuid();

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(dto, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_WhenDesignationDoesNotExist_ThrowsBadRequestException()
    {
        var (service, _, _, _, _) = CreateService();
        var dto = ValidCreateDto();
        dto.DesignationId = Guid.NewGuid();

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(dto, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_WhenEmployeeCodeAlreadyExists_ThrowsConflictException()
    {
        var (service, employeeRepository, _, _, _) = CreateService();
        employeeRepository.Employees.Add(new Employee { EmployeeCode = "EMP-001", Email = "other@example.com" });

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(ValidCreateDto(), CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_WhenEmailAlreadyExists_ThrowsConflictException()
    {
        var (service, employeeRepository, _, _, _) = CreateService();
        employeeRepository.Employees.Add(new Employee { EmployeeCode = "EMP-999", Email = "jane.doe@example.com" });

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(ValidCreateDto(), CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdAsync_WhenEmployeeDoesNotExist_ThrowsNotFoundException()
    {
        var (service, _, _, _, _) = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task DeactivateAsync_SetsStatusToInactive()
    {
        var (service, employeeRepository, _, _, _) = CreateService();
        var employee = new Employee { EmployeeCode = "EMP-001", Email = "jane.doe@example.com", Status = EmployeeStatus.Active };
        employeeRepository.Employees.Add(employee);

        await service.DeactivateAsync(employee.Id, CancellationToken.None);

        Assert.Equal(EmployeeStatus.Inactive, employee.Status);
    }

    [Fact]
    public async Task CreateAsync_WithSurroundingWhitespace_StoresTrimmedValues()
    {
        var (service, employeeRepository, _, _, _) = CreateService();
        var dto = ValidCreateDto();
        dto.EmployeeCode = "  EMP-001 ";
        dto.Email = " jane.doe@example.com ";
        dto.FirstName = " Jane ";

        await service.CreateAsync(dto, CancellationToken.None);

        var stored = Assert.Single(employeeRepository.Employees);
        Assert.Equal("EMP-001", stored.EmployeeCode);
        Assert.Equal("jane.doe@example.com", stored.Email);
        Assert.Equal("Jane", stored.FirstName);
    }

    [Fact]
    public async Task CreateAsync_WhenPaddedEmployeeCodeMatchesExisting_ThrowsConflictException()
    {
        var (service, employeeRepository, _, _, _) = CreateService();
        employeeRepository.Employees.Add(new Employee { EmployeeCode = "EMP-001", Email = "other@example.com" });
        var dto = ValidCreateDto();
        dto.EmployeeCode = " EMP-001";

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(dto, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_WhenPaddedEmailBelongsToAnotherEmployee_ThrowsConflictException()
    {
        var (service, employeeRepository, _, _, _) = CreateService();
        var employee = new Employee { EmployeeCode = "EMP-001", Email = "jane.doe@example.com" };
        employeeRepository.Employees.Add(employee);
        employeeRepository.Employees.Add(new Employee { EmployeeCode = "EMP-002", Email = "taken@example.com" });

        var dto = ValidUpdateDto();
        dto.Email = " taken@example.com ";

        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAsync(employee.Id, dto, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_WhenDepartmentIsInactive_ThrowsBadRequestException()
    {
        var (service, _, departmentRepository, _, _) = CreateService();
        departmentRepository.Departments.Single().IsActive = false;

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(ValidCreateDto(), CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_WhenDesignationIsInactive_ThrowsBadRequestException()
    {
        var (service, _, _, designationRepository, _) = CreateService();
        designationRepository.Designations.Single().IsActive = false;

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(ValidCreateDto(), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_WhenMovingToInactiveDepartment_ThrowsBadRequestException()
    {
        var (service, employeeRepository, departmentRepository, _, _) = CreateService();
        var employee = new Employee { EmployeeCode = "EMP-001", Email = "jane.doe@example.com", DepartmentId = DepartmentId, DesignationId = DesignationId };
        employeeRepository.Employees.Add(employee);
        var closedDepartment = new Department { Name = "Closed", IsActive = false };
        departmentRepository.Departments.Add(closedDepartment);

        var dto = ValidUpdateDto();
        dto.DepartmentId = closedDepartment.Id;

        await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateAsync(employee.Id, dto, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_WhenStayingInDepartmentThatWasDeactivated_Succeeds()
    {
        var (service, employeeRepository, departmentRepository, _, _) = CreateService();
        var employee = new Employee { EmployeeCode = "EMP-001", Email = "jane.doe@example.com", DepartmentId = DepartmentId, DesignationId = DesignationId };
        employeeRepository.Employees.Add(employee);
        departmentRepository.Departments.Single().IsActive = false;

        var dto = ValidUpdateDto();
        dto.FirstName = "Janet";

        var result = await service.UpdateAsync(employee.Id, dto, CancellationToken.None);

        Assert.Equal("Janet", result.FirstName);
    }

    [Fact]
    public async Task CreateAsync_ComputesFullNameAndDefaultsEmploymentTypeToFullTime()
    {
        var (service, _, _, _, _) = CreateService();

        var result = await service.CreateAsync(ValidCreateDto(), CancellationToken.None);

        Assert.Equal("Jane Doe", result.FullName);
        Assert.Equal("FullTime", result.EmploymentType);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task UpdateAsync_WithoutStatusOrEmploymentType_KeepsCurrentValues()
    {
        var (service, employeeRepository, _, _, _) = CreateService();
        var employee = new Employee { EmployeeCode = "EMP-001", Email = "jane.doe@example.com", DepartmentId = DepartmentId, DesignationId = DesignationId, Status = EmployeeStatus.OnLeave, EmploymentType = EmploymentType.Contract };
        employeeRepository.Employees.Add(employee);

        var result = await service.UpdateAsync(employee.Id, ValidUpdateDto(), CancellationToken.None);

        Assert.Equal("OnLeave", result.Status);
        Assert.Equal("Contract", result.EmploymentType);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task UpdateAsync_CanChangeStatusAndEmploymentType()
    {
        var (service, employeeRepository, _, _, _) = CreateService();
        var employee = new Employee { EmployeeCode = "EMP-001", Email = "jane.doe@example.com", DepartmentId = DepartmentId, DesignationId = DesignationId };
        employeeRepository.Employees.Add(employee);
        var dto = ValidUpdateDto();
        dto.Status = EmployeeStatus.Resigned;
        dto.EmploymentType = EmploymentType.PartTime;

        var result = await service.UpdateAsync(employee.Id, dto, CancellationToken.None);

        Assert.Equal("Resigned", result.Status);
        Assert.Equal("PartTime", result.EmploymentType);
        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task UpdateAsync_ReactivatingIntoInactiveDepartment_ThrowsBadRequestException()
    {
        var (service, employeeRepository, departmentRepository, _, _) = CreateService();
        var employee = new Employee { EmployeeCode = "EMP-001", Email = "jane.doe@example.com", DepartmentId = DepartmentId, DesignationId = DesignationId, Status = EmployeeStatus.Inactive };
        employeeRepository.Employees.Add(employee);
        departmentRepository.Departments.Single().IsActive = false;
        var dto = ValidUpdateDto();
        dto.Status = EmployeeStatus.Active;

        await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateAsync(employee.Id, dto, CancellationToken.None));
        Assert.Equal(EmployeeStatus.Inactive, employee.Status);
    }

    [Fact]
    public async Task UpdateAsync_ReactivatingInActiveDepartment_Succeeds()
    {
        var (service, employeeRepository, _, _, _) = CreateService();
        var employee = new Employee { EmployeeCode = "EMP-001", Email = "jane.doe@example.com", DepartmentId = DepartmentId, DesignationId = DesignationId, Status = EmployeeStatus.Inactive };
        employeeRepository.Employees.Add(employee);
        var dto = ValidUpdateDto();
        dto.Status = EmployeeStatus.Active;

        var result = await service.UpdateAsync(employee.Id, dto, CancellationToken.None);

        Assert.Equal("Active", result.Status);
    }

    [Fact]
    public async Task CreateAsync_StoresBasicSalary()
    {
        var (service, _, _, _, _) = CreateService();
        var dto = ValidCreateDto();
        dto.BasicSalary = 45000.50m;

        var result = await service.CreateAsync(dto, CancellationToken.None);

        Assert.Equal(45000.50m, result.BasicSalary);
    }

    [Fact]
    public async Task CreateAsync_WithFutureDateOfBirth_ThrowsBadRequestException()
    {
        var (service, _, _, _, _) = CreateService();
        var dto = ValidCreateDto();
        dto.DateOfBirth = DateTime.UtcNow.Date.AddDays(1);

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(dto, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_WithJoiningDateBeforeDateOfBirth_ThrowsBadRequestException()
    {
        var (service, _, _, _, _) = CreateService();
        var dto = ValidCreateDto();
        dto.JoiningDate = dto.DateOfBirth.AddDays(-1);

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(dto, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_WithoutBasicSalary_KeepsTheCurrentSalary()
    {
        var (service, employeeRepository, _, _, _) = CreateService();
        var employee = new Employee { EmployeeCode = "EMP-001", Email = "jane.doe@example.com", DepartmentId = DepartmentId, DesignationId = DesignationId, BasicSalary = 60000m };
        employeeRepository.Employees.Add(employee);

        var result = await service.UpdateAsync(employee.Id, ValidUpdateDto(), CancellationToken.None);

        Assert.Equal(60000m, result.BasicSalary);
    }
}
