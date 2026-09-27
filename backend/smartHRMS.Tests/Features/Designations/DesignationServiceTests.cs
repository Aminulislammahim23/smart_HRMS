using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Features.Designations;
using smartHRMS.Application.Features.Designations.Dtos;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;
using smartHRMS.Tests.Fakes;
using Xunit;

namespace smartHRMS.Tests.Features.Designations;

public class DesignationServiceTests
{
    private static (DesignationService Service, FakeDesignationRepository Repository) CreateService()
    {
        var repository = new FakeDesignationRepository();
        return (new DesignationService(repository), repository);
    }

    private static Designation Seed(FakeDesignationRepository repository, string name = "Engineering", bool isActive = true)
    {
        var designation = new Designation { Name = name, IsActive = isActive };
        repository.Designations.Add(designation);
        return designation;
    }

    private static void AssignEmployee(FakeDesignationRepository repository, Designation designation, EmployeeStatus status)
    {
        repository.Employees.Add(new Employee { DesignationId = designation.Id, Status = status });
    }

    private static UpdateDesignationDto UpdateDto(string name = "Engineering", bool isActive = true) => new()
    {
        Name = name,
        IsActive = isActive,
    };

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesActiveDesignationWithTrimmedValues()
    {
        var (service, repository) = CreateService();

        var result = await service.CreateAsync(new CreateDesignationDto { Name = "  Finance ", Description = "   " }, CancellationToken.None);

        Assert.Equal("Finance", result.Name);
        Assert.Null(result.Description);
        Assert.True(result.IsActive);
        Assert.Equal(0, result.EmployeeCount);
        Assert.Single(repository.Designations);
    }

    [Theory]
    [InlineData("Engineering")]
    [InlineData("ENGINEERING")]
    [InlineData(" Engineering ")]
    public async Task CreateAsync_WhenNameAlreadyExists_ThrowsConflictException(string name)
    {
        var (service, repository) = CreateService();
        Seed(repository);

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(new CreateDesignationDto { Name = name }, CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdAsync_WhenMissing_ThrowsNotFoundException()
    {
        var (service, _) = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmployeeCountPerDesignation()
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
        var designation = Seed(repository);

        var result = await service.UpdateAsync(designation.Id, UpdateDto("Senior Engineer"), CancellationToken.None);

        Assert.Equal("Senior Engineer", result.Name);
        Assert.NotNull(result.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_KeepingItsOwnName_Succeeds()
    {
        var (service, repository) = CreateService();
        var designation = Seed(repository);

        var result = await service.UpdateAsync(designation.Id, UpdateDto("engineering"), CancellationToken.None);

        Assert.Equal("engineering", result.Name);
    }

    [Fact]
    public async Task UpdateAsync_WhenNameBelongsToAnotherDesignation_ThrowsConflictException()
    {
        var (service, repository) = CreateService();
        var designation = Seed(repository);
        Seed(repository, "Finance");

        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAsync(designation.Id, UpdateDto("Finance"), CancellationToken.None));
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
        var designation = Seed(repository);
        AssignEmployee(repository, designation, EmployeeStatus.Active);

        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAsync(designation.Id, UpdateDto(isActive: false), CancellationToken.None));
        Assert.True(designation.IsActive);
    }

    [Theory]
    [InlineData(EmployeeStatus.Active)]
    [InlineData(EmployeeStatus.OnLeave)]
    public async Task DeactivateAsync_WithCurrentEmployees_ThrowsConflictAndKeepsDesignationActive(EmployeeStatus status)
    {
        var (service, repository) = CreateService();
        var designation = Seed(repository);
        AssignEmployee(repository, designation, status);

        await Assert.ThrowsAsync<ConflictException>(() => service.DeactivateAsync(designation.Id, CancellationToken.None));
        Assert.True(designation.IsActive);
    }

    [Fact]
    public async Task DeactivateAsync_WithOnlyFormerEmployees_SoftDeletesDesignation()
    {
        var (service, repository) = CreateService();
        var designation = Seed(repository);
        AssignEmployee(repository, designation, EmployeeStatus.Resigned);
        AssignEmployee(repository, designation, EmployeeStatus.Inactive);

        await service.DeactivateAsync(designation.Id, CancellationToken.None);

        Assert.False(designation.IsActive);
        Assert.NotNull(designation.UpdatedAt);
        Assert.Single(repository.Designations);
    }

    [Fact]
    public async Task DeactivateAsync_WhenMissing_ThrowsNotFoundException()
    {
        var (service, _) = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.DeactivateAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
