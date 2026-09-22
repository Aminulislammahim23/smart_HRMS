using System.ComponentModel.DataAnnotations;
using smartHRMS.Application.Features.Employees.Dtos;
using Xunit;

namespace smartHRMS.Tests.Features.Employees;

public class EmployeeDtoValidationTests
{
    private static CreateEmployeeDto ValidCreateDto() => new()
    {
        EmployeeCode = "EMP-001",
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane.doe@example.com",
        Phone = "+8801711000001",
        DateOfBirth = new DateTime(1990, 1, 1),
        JoiningDate = new DateTime(2024, 1, 1),
        DepartmentId = Guid.NewGuid(),
        DesignationId = Guid.NewGuid(),
    };

    private static UpdateEmployeeDto ValidUpdateDto() => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane.doe@example.com",
        Phone = "+8801711000001",
        DateOfBirth = new DateTime(1990, 1, 1),
        JoiningDate = new DateTime(2024, 1, 1),
        DepartmentId = Guid.NewGuid(),
        DesignationId = Guid.NewGuid(),
    };

    private static List<ValidationResult> Validate(object dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results;
    }

    private static bool HasErrorFor(List<ValidationResult> results, string memberName) =>
        results.Any(r => r.MemberNames.Contains(memberName));

    [Fact]
    public void CreateDto_WithValidData_HasNoErrors()
    {
        Assert.Empty(Validate(ValidCreateDto()));
    }

    [Fact]
    public void CreateDto_WithoutPhone_IsValid()
    {
        var dto = ValidCreateDto();
        dto.Phone = null;

        Assert.Empty(Validate(dto));
    }

    [Fact]
    public void CreateDto_WithEmptyDepartmentId_Fails()
    {
        var dto = ValidCreateDto();
        dto.DepartmentId = Guid.Empty;

        Assert.True(HasErrorFor(Validate(dto), nameof(CreateEmployeeDto.DepartmentId)));
    }

    [Fact]
    public void CreateDto_WithEmptyDesignationId_Fails()
    {
        var dto = ValidCreateDto();
        dto.DesignationId = Guid.Empty;

        Assert.True(HasErrorFor(Validate(dto), nameof(CreateEmployeeDto.DesignationId)));
    }

    [Fact]
    public void CreateDto_WithDefaultJoiningDate_Fails()
    {
        var dto = ValidCreateDto();
        dto.JoiningDate = default;

        Assert.True(HasErrorFor(Validate(dto), nameof(CreateEmployeeDto.JoiningDate)));
    }

    [Fact]
    public void CreateDto_WithInvalidEmail_Fails()
    {
        var dto = ValidCreateDto();
        dto.Email = "not-an-email";

        Assert.True(HasErrorFor(Validate(dto), nameof(CreateEmployeeDto.Email)));
    }

    [Fact]
    public void CreateDto_WithInvalidPhone_Fails()
    {
        var dto = ValidCreateDto();
        dto.Phone = "call me maybe";

        Assert.True(HasErrorFor(Validate(dto), nameof(CreateEmployeeDto.Phone)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateDto_WithBlankName_Fails(string blank)
    {
        var dto = ValidCreateDto();
        dto.FirstName = blank;
        dto.LastName = blank;

        var results = Validate(dto);

        Assert.True(HasErrorFor(results, nameof(CreateEmployeeDto.FirstName)));
        Assert.True(HasErrorFor(results, nameof(CreateEmployeeDto.LastName)));
    }

    [Fact]
    public void UpdateDto_WithValidData_HasNoErrors()
    {
        Assert.Empty(Validate(ValidUpdateDto()));
    }

    [Fact]
    public void UpdateDto_WithEmptyIdsAndDefaultJoiningDate_FailsForEach()
    {
        var dto = ValidUpdateDto();
        dto.DepartmentId = Guid.Empty;
        dto.DesignationId = Guid.Empty;
        dto.JoiningDate = default;

        var results = Validate(dto);

        Assert.True(HasErrorFor(results, nameof(UpdateEmployeeDto.DepartmentId)));
        Assert.True(HasErrorFor(results, nameof(UpdateEmployeeDto.DesignationId)));
        Assert.True(HasErrorFor(results, nameof(UpdateEmployeeDto.JoiningDate)));
    }
}
