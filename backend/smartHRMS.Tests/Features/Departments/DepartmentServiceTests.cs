using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Features.Departments;
using smartHRMS.Application.Features.Departments.Dtos;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;
using smartHRMS.Tests.Fakes;
using Xunit;

namespace smartHRMS.Tests.Features.Departments;

public class DepartmentServiceTests
{
    private static (DepartmentService Service, FakeDepartmentRepository Repository) CreateService()
    {
        var repository = new FakeDepartmentRepository();
        return (new DepartmentService(repository), repository);
    }

    private static Department Seed(FakeDepartmentRepository repository, string name = "Engineering", bool isActive = true)
    {
        var department = new Department { Name = name, IsActive = isActive };
        repository.Departments.Add(department);
        return department;
    }

    private static void AssignEmployee(FakeDepartmentRepository repository, Department department, EmployeeStatus status)
    {
        repository.Employees.Add(new Employee { DepartmentId = department.Id, Status = status });
    }

    private static UpdateDepartmentDto UpdateDto(string name = "Engineering", bool isActive = true) => new()
    {
        Name = name,
        IsActive = isActive,
    };

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesActiveDepartmentWithTrimmedValues()
    {
        var (service, repository) = CreateService();

        var result = await service.CreateAsync(new CreateDepartmentDto { Name = "  Finance ", Description = "   " }, CancellationToken.None);

        Assert.Equal("Finance", result.Name);
        Assert.Null(result.Description);
        Assert.True(result.IsActive);
        Assert.Equal(0, result.EmployeeCount);
        Assert.Single(repository.Departments);
    }

    [Theory]
    [InlineData("Engineering")]
    [InlineData("ENGINEERING")]
    [InlineData(" Engineering ")]
    public async Task CreateAsync_WhenNameAlreadyExists_ThrowsConflictException(string name)
    {
        var (service, repository) = CreateService();
        Seed(repository);

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(new CreateDepartmentDto { Name = name }, CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdAsync_WhenMissing_ThrowsNotFoundException()
    {
        var (service, _) = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmployeeCountPerDepartment()
    {
        var (service, repository) = CreateService();
        var engineering = Seed(repository);
        Seed(repository, "Finance");
        AssignEmployee(repository, engineering, EmployeeStatus.Active);
        AssignEmployee(repository, engineering, EmployeeStatus.Resigned);

        var result = await service.GetAllAsync(CancellationToken.None);

        Assert.Equal(2, result.Single(d => d.Name == "Engineering").EmployeeCount);
        Assert.Equal(0, result.Single(d => d.Name == "Finance").EmployeeCount);
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesFieldsAndUpdatedAt()
    {
        var (service, repository) = CreateService();
        var department = Seed(repository);

        var result = await service.UpdateAsync(department.Id, UpdateDto("Platform Engineering"), CancellationToken.None);

        Assert.Equal("Platform Engineering", result.Name);
        Assert.NotNull(result.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_KeepingItsOwnName_Succeeds()
    {
        var (service, repository) = CreateService();
        var department = Seed(repository);

        var result = await service.UpdateAsync(department.Id, UpdateDto("engineering"), CancellationToken.None);

        Assert.Equal("engineering", result.Name);
    }

    [Fact]
    public async Task UpdateAsync_WhenNameBelongsToAnotherDepartment_ThrowsConflictException()
    {
        var (service, repository) = CreateService();
        var department = Seed(repository);
        Seed(repository, "Finance");

        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAsync(department.Id, UpdateDto("Finance"), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_WhenMissing_ThrowsNotFoundException()
    {
        var (service, _) = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(Guid.NewGuid(), UpdateDto(), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_DeactivatingWithCurrentEmployees_ThrowsConflictException()
    {
        var (service, repository) = CreateService();
        var department = Seed(repository);
        AssignEmployee(repository, department, EmployeeStatus.Active);

        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAsync(department.Id, UpdateDto(isActive: false), CancellationToken.None));
        Assert.True(department.IsActive);
    }

    [Theory]
    [InlineData(EmployeeStatus.Active)]
    [InlineData(EmployeeStatus.OnLeave)]
    public async Task DeactivateAsync_WithCurrentEmployees_ThrowsConflictAndKeepsDepartmentActive(EmployeeStatus status)
    {
        var (service, repository) = CreateService();
        var department = Seed(repository);
        AssignEmployee(repository, department, status);

        await Assert.ThrowsAsync<ConflictException>(() => service.DeactivateAsync(department.Id, CancellationToken.None));
        Assert.True(department.IsActive);
    }

    [Fact]
    public async Task DeactivateAsync_WithOnlyFormerEmployees_SoftDeletesDepartment()
    {
        var (service, repository) = CreateService();
        var department = Seed(repository);
        AssignEmployee(repository, department, EmployeeStatus.Resigned);
        AssignEmployee(repository, department, EmployeeStatus.Inactive);

        await service.DeactivateAsync(department.Id, CancellationToken.None);

        Assert.False(department.IsActive);
        Assert.NotNull(department.UpdatedAt);
        Assert.Single(repository.Departments);
    }

    [Fact]
    public async Task DeactivateAsync_WhenMissing_ThrowsNotFoundException()
    {
        var (service, _) = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.DeactivateAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
