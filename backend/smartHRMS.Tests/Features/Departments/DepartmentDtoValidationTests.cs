using System.ComponentModel.DataAnnotations;
using smartHRMS.Application.Features.Departments.Dtos;
using smartHRMS.Application.Features.Designations.Dtos;
using Xunit;

namespace smartHRMS.Tests.Features.Departments;

/// <summary>Covers both Department and Designation DTOs, which share the same validation rules.</summary>
public class DepartmentDtoValidationTests
{
    private static List<ValidationResult> Validate(object dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results;
    }

    private static bool HasErrorFor(List<ValidationResult> results, string memberName) =>
        results.Any(r => r.MemberNames.Contains(memberName));

    [Fact]
    public void CreateDtos_WithValidName_HaveNoErrors()
    {
        Assert.Empty(Validate(new CreateDepartmentDto { Name = "Engineering" }));
        Assert.Empty(Validate(new CreateDesignationDto { Name = "Manager" }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateDtos_WithBlankName_Fail(string blank)
    {
        Assert.True(HasErrorFor(Validate(new CreateDepartmentDto { Name = blank }), nameof(CreateDepartmentDto.Name)));
        Assert.True(HasErrorFor(Validate(new CreateDesignationDto { Name = blank }), nameof(CreateDesignationDto.Name)));
    }

    [Fact]
    public void CreateDtos_WithTooLongNameOrDescription_Fail()
    {
        var department = Validate(new CreateDepartmentDto { Name = new string('x', 101), Description = new string('x', 501) });
        var designation = Validate(new CreateDesignationDto { Name = new string('x', 101), Description = new string('x', 501) });

        Assert.True(HasErrorFor(department, nameof(CreateDepartmentDto.Name)));
        Assert.True(HasErrorFor(department, nameof(CreateDepartmentDto.Description)));
        Assert.True(HasErrorFor(designation, nameof(CreateDesignationDto.Name)));
        Assert.True(HasErrorFor(designation, nameof(CreateDesignationDto.Description)));
    }

    [Fact]
    public void UpdateDtos_WithoutIsActive_Fail()
    {
        Assert.True(HasErrorFor(Validate(new UpdateDepartmentDto { Name = "Engineering" }), nameof(UpdateDepartmentDto.IsActive)));
        Assert.True(HasErrorFor(Validate(new UpdateDesignationDto { Name = "Manager" }), nameof(UpdateDesignationDto.IsActive)));
    }
}
