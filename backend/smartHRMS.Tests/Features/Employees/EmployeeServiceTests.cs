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
    private static (EmployeeService Service, FakeEmployeeRepository EmployeeRepository, FakeDepartmentRepository DepartmentRepository, FakeDesignationRepository DesignationRepository, FakeFileStorageService FileStorageService) CreateService()
    {
        var employeeRepository = new FakeEmployeeRepository();
        var departmentRepository = new FakeDepartmentRepository();
        var designationRepository = new FakeDesignationRepository();
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
        DepartmentId = Guid.NewGuid(),
        DesignationId = Guid.NewGuid(),
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
        var (service, _, departmentRepository, _, _) = CreateService();
        departmentRepository.ShouldExist = false;

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(ValidCreateDto(), CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_WhenDesignationDoesNotExist_ThrowsBadRequestException()
    {
        var (service, _, _, designationRepository, _) = CreateService();
        designationRepository.ShouldExist = false;

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(ValidCreateDto(), CancellationToken.None));
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
}
