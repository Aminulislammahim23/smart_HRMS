using System.ComponentModel.DataAnnotations;
using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Features.EmployeeAddresses;
using smartHRMS.Application.Features.EmployeeAddresses.Dtos;
using smartHRMS.Application.Features.EmployeeEducations;
using smartHRMS.Application.Features.EmployeeEducations.Dtos;
using smartHRMS.Application.Features.EmployeeEmergencyContacts;
using smartHRMS.Application.Features.EmployeeEmergencyContacts.Dtos;
using smartHRMS.Application.Features.EmployeeExperiences;
using smartHRMS.Application.Features.EmployeeExperiences.Dtos;
using smartHRMS.Application.Features.EmployeePersonalDetails;
using smartHRMS.Application.Features.EmployeePersonalDetails.Dtos;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;
using smartHRMS.Tests.Fakes;
using Xunit;
using PersonalDetails = smartHRMS.Domain.Entities.EmployeePersonalDetails;

namespace smartHRMS.Tests.Features.EmployeeProfile;

/// <summary>Business rules of the Day 11 profile records: personal details, addresses, contacts, education, experience.</summary>
public class EmployeeProfileRecordTests
{
    private static readonly DateTime DateOfBirth = new(1990, 6, 15);

    private static (FakeEmployeeRepository Employees, Employee Employee, Employee Other) Employees()
    {
        var repository = new FakeEmployeeRepository();
        var employee = new Employee { EmployeeCode = "EMP-001", Email = "a@example.com", DateOfBirth = DateOfBirth };
        var other = new Employee { EmployeeCode = "EMP-002", Email = "b@example.com", DateOfBirth = DateOfBirth };
        repository.Employees.Add(employee);
        repository.Employees.Add(other);
        return (repository, employee, other);
    }

    private static List<ValidationResult> Validate(object dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results;
    }

    // ---------------- Personal details ----------------

    [Fact]
    public async Task PersonalDetails_CreateGetUpdateDelete()
    {
        var (employees, employee, _) = Employees();
        var repository = new FakeEmployeeOwnedRepository<PersonalDetails>();
        var service = new EmployeePersonalDetailsService(repository, employees);

        var created = await service.CreateAsync(employee.Id, new CreateEmployeePersonalDetailsDto
        {
            Gender = Gender.Male, MaritalStatus = MaritalStatus.Single, NationalId = " 1234567890 ", PassportNo = "ab123456", Nationality = " Bangladeshi ",
        }, CancellationToken.None);

        Assert.Equal(DateOfBirth, created.DateOfBirth);
        Assert.Equal("1234567890", created.NationalId);
        Assert.Equal("AB123456", created.PassportNo);
        Assert.Equal("Bangladeshi", created.Nationality);

        var updated = await service.UpdateAsync(employee.Id, new UpdateEmployeePersonalDetailsDto { MaritalStatus = MaritalStatus.Married }, CancellationToken.None);
        Assert.Equal("Married", updated.MaritalStatus);
        Assert.Null(updated.NationalId); // full update clears omitted fields
        Assert.NotNull(updated.UpdatedAt);

        await service.DeleteAsync(employee.Id, CancellationToken.None);
        Assert.Empty(repository.Items);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(employee.Id, CancellationToken.None));
    }

    [Fact]
    public async Task PersonalDetails_SecondCreate_ThrowsConflict()
    {
        var (employees, employee, _) = Employees();
        var service = new EmployeePersonalDetailsService(new FakeEmployeeOwnedRepository<PersonalDetails>(), employees);
        await service.CreateAsync(employee.Id, new CreateEmployeePersonalDetailsDto(), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(employee.Id, new CreateEmployeePersonalDetailsDto(), CancellationToken.None));
    }

    [Fact]
    public async Task PersonalDetails_NationalIdOrPassportOfAnotherEmployee_ThrowsConflict()
    {
        var (employees, employee, other) = Employees();
        var service = new EmployeePersonalDetailsService(new FakeEmployeeOwnedRepository<PersonalDetails>(), employees);
        await service.CreateAsync(employee.Id, new CreateEmployeePersonalDetailsDto { NationalId = "1234567890", PassportNo = "AB123456" }, CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(other.Id, new CreateEmployeePersonalDetailsDto { NationalId = "1234567890" }, CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(other.Id, new CreateEmployeePersonalDetailsDto { PassportNo = "ab123456" }, CancellationToken.None));
    }

    [Fact]
    public async Task PersonalDetails_UpdateKeepingOwnNationalId_Succeeds()
    {
        var (employees, employee, _) = Employees();
        var service = new EmployeePersonalDetailsService(new FakeEmployeeOwnedRepository<PersonalDetails>(), employees);
        await service.CreateAsync(employee.Id, new CreateEmployeePersonalDetailsDto { NationalId = "1234567890" }, CancellationToken.None);

        var updated = await service.UpdateAsync(employee.Id, new UpdateEmployeePersonalDetailsDto { NationalId = "1234567890" }, CancellationToken.None);

        Assert.Equal("1234567890", updated.NationalId);
    }

    [Fact]
    public async Task PersonalDetails_UpdateOrGetWhenNoneOrEmployeeMissing_ThrowsNotFound()
    {
        var (employees, employee, _) = Employees();
        var service = new EmployeePersonalDetailsService(new FakeEmployeeOwnedRepository<PersonalDetails>(), employees);

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(employee.Id, new UpdateEmployeePersonalDetailsDto(), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(Guid.NewGuid(), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(Guid.NewGuid(), new CreateEmployeePersonalDetailsDto(), CancellationToken.None));
    }

    [Theory]
    [InlineData("1234567890", true)]
    [InlineData("1234567890123", true)]
    [InlineData("12345678901234567", true)]
    [InlineData("12345678901", false)]
    [InlineData("12345ABCDE", false)]
    public void PersonalDetailsDto_ValidatesNationalIdFormat(string nationalId, bool valid)
    {
        var results = Validate(new CreateEmployeePersonalDetailsDto { NationalId = nationalId });

        Assert.Equal(valid, results.Count == 0);
    }

    [Theory]
    [InlineData("AB123", false)]
    [InlineData("AB-123456", false)]
    [InlineData("AB1234567", true)]
    public void PersonalDetailsDto_ValidatesPassportFormat(string passportNo, bool valid)
    {
        Assert.Equal(valid, Validate(new CreateEmployeePersonalDetailsDto { PassportNo = passportNo }).Count == 0);
    }

    // ---------------- Addresses ----------------

    private static CreateEmployeeAddressDto Address(AddressType type) => new()
    {
        AddressType = type, Address = " Road 1 ", City = "Dhaka", District = "Dhaka", PostalCode = "1207",
    };

    [Fact]
    public async Task Address_OnlyOnePresentAndOnePermanentPerEmployee()
    {
        var (employees, employee, other) = Employees();
        var service = new EmployeeAddressService(new FakeEmployeeOwnedRepository<EmployeeAddress>(), employees);

        var present = await service.CreateAsync(employee.Id, Address(AddressType.Present), CancellationToken.None);
        var permanent = await service.CreateAsync(employee.Id, Address(AddressType.Permanent), CancellationToken.None);
        await service.CreateAsync(other.Id, Address(AddressType.Present), CancellationToken.None); // other employee: fine

        Assert.Equal("Road 1", present.Address);
        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(employee.Id, Address(AddressType.Present), CancellationToken.None));
        // Changing the permanent address into a second present address is also a duplicate.
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdateAsync(employee.Id, permanent.Id, new UpdateEmployeeAddressDto { AddressType = AddressType.Present, Address = "x", City = "c", District = "d" }, CancellationToken.None));
        // Updating an address while keeping its own type is fine.
        var updated = await service.UpdateAsync(employee.Id, present.Id, new UpdateEmployeeAddressDto { AddressType = AddressType.Present, Address = "Road 2", City = "Dhaka", District = "Dhaka" }, CancellationToken.None);
        Assert.Equal("Road 2", updated.Address);
        Assert.Null(updated.PostalCode);
    }

    [Fact]
    public async Task Address_OfAnotherEmployee_IsNotFound()
    {
        var (employees, employee, other) = Employees();
        var service = new EmployeeAddressService(new FakeEmployeeOwnedRepository<EmployeeAddress>(), employees);
        var address = await service.CreateAsync(employee.Id, Address(AddressType.Present), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(other.Id, address.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(other.Id, address.Id, CancellationToken.None));
        Assert.Single(await service.GetAllAsync(employee.Id, CancellationToken.None));
    }

    [Fact]
    public void AddressDto_RequiresTypeAddressCityDistrict()
    {
        var results = Validate(new CreateEmployeeAddressDto());
        var members = results.SelectMany(r => r.MemberNames).ToList();

        Assert.Contains(nameof(CreateEmployeeAddressDto.AddressType), members);
        Assert.Contains(nameof(CreateEmployeeAddressDto.Address), members);
        Assert.Contains(nameof(CreateEmployeeAddressDto.City), members);
        Assert.Contains(nameof(CreateEmployeeAddressDto.District), members);
    }

    // ---------------- Emergency contacts ----------------

    [Fact]
    public async Task EmergencyContact_AllowsManyButNotTheSamePhoneTwice()
    {
        var (employees, employee, _) = Employees();
        var service = new EmployeeEmergencyContactService(new FakeEmployeeOwnedRepository<EmployeeEmergencyContact>(), employees);

        await service.CreateAsync(employee.Id, new CreateEmployeeEmergencyContactDto { Name = "A", Relationship = "Father", Phone = "+8801700000001", Email = "  " }, CancellationToken.None);
        var second = await service.CreateAsync(employee.Id, new CreateEmployeeEmergencyContactDto { Name = "B", Relationship = "Mother", Phone = "+8801700000002" }, CancellationToken.None);

        Assert.Equal(2, (await service.GetAllAsync(employee.Id, CancellationToken.None)).Count);
        Assert.Null(second.Email);
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(employee.Id, new CreateEmployeeEmergencyContactDto { Name = "C", Relationship = "Brother", Phone = "+8801700000001" }, CancellationToken.None));
    }

    [Fact]
    public void EmergencyContactDto_RequiresNameRelationshipAndValidPhone()
    {
        var members = Validate(new CreateEmployeeEmergencyContactDto { Phone = "abc!!", Email = "bad" }).SelectMany(r => r.MemberNames).ToList();

        Assert.Contains(nameof(CreateEmployeeEmergencyContactDto.Name), members);
        Assert.Contains(nameof(CreateEmployeeEmergencyContactDto.Relationship), members);
        Assert.Contains(nameof(CreateEmployeeEmergencyContactDto.Phone), members);
        Assert.Contains(nameof(CreateEmployeeEmergencyContactDto.Email), members);
    }

    // ---------------- Education ----------------

    [Theory]
    [InlineData(1989)] // before the birth year
    [InlineData(2100)] // too far in the future
    public async Task Education_WithImpossiblePassingYear_ThrowsBadRequest(int year)
    {
        var (employees, employee, _) = Employees();
        var service = new EmployeeEducationService(new FakeEmployeeOwnedRepository<EmployeeEducation>(), employees);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.CreateAsync(employee.Id, new CreateEmployeeEducationDto { Degree = "SSC", Institution = "School", PassingYear = year }, CancellationToken.None));
    }

    [Fact]
    public async Task Education_AllowsMultipleRecordsNewestFirstButRejectsExactDuplicates()
    {
        var (employees, employee, _) = Employees();
        var service = new EmployeeEducationService(new FakeEmployeeOwnedRepository<EmployeeEducation>(), employees);

        await service.CreateAsync(employee.Id, new CreateEmployeeEducationDto { Degree = "SSC", Institution = "School", PassingYear = 2006 }, CancellationToken.None);
        await service.CreateAsync(employee.Id, new CreateEmployeeEducationDto { Degree = "BSc", Institution = "University", PassingYear = 2012 }, CancellationToken.None);

        var list = await service.GetAllAsync(employee.Id, CancellationToken.None);
        Assert.Equal(new[] { "BSc", "SSC" }, list.Select(e => e.Degree));
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(employee.Id, new CreateEmployeeEducationDto { Degree = " SSC ", Institution = "School", PassingYear = 2006 }, CancellationToken.None));
    }

    [Fact]
    public void EducationDto_RequiresDegreeInstitutionAndYear()
    {
        var members = Validate(new CreateEmployeeEducationDto()).SelectMany(r => r.MemberNames).ToList();

        Assert.Contains(nameof(CreateEmployeeEducationDto.Degree), members);
        Assert.Contains(nameof(CreateEmployeeEducationDto.Institution), members);
        Assert.Contains(nameof(CreateEmployeeEducationDto.PassingYear), members);
    }

    // ---------------- Experience ----------------

    private static CreateEmployeeExperienceDto Job(DateTime start, DateTime? end) => new()
    {
        CompanyName = "Acme", Designation = "Developer", StartDate = start, EndDate = end,
    };

    [Fact]
    public async Task Experience_RejectsImpossibleDates()
    {
        var (employees, employee, _) = Employees();
        var service = new EmployeeExperienceService(new FakeEmployeeOwnedRepository<EmployeeExperience>(), employees);
        var today = DateTime.UtcNow.Date;

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(employee.Id, Job(new DateTime(2020, 1, 1), new DateTime(2019, 1, 1)), CancellationToken.None));
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(employee.Id, Job(today.AddDays(1), null), CancellationToken.None));
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(employee.Id, Job(new DateTime(2020, 1, 1), today.AddDays(1)), CancellationToken.None));
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(employee.Id, Job(DateOfBirth.AddDays(-1), null), CancellationToken.None));
    }

    [Fact]
    public async Task Experience_AllowsCurrentJobAndListsNewestFirst()
    {
        var (employees, employee, _) = Employees();
        var service = new EmployeeExperienceService(new FakeEmployeeOwnedRepository<EmployeeExperience>(), employees);

        await service.CreateAsync(employee.Id, Job(new DateTime(2012, 1, 1), new DateTime(2015, 12, 31)), CancellationToken.None);
        var current = await service.CreateAsync(employee.Id, Job(new DateTime(2016, 1, 1), null), CancellationToken.None);
        var sameDay = await service.CreateAsync(employee.Id, Job(new DateTime(2011, 5, 5), new DateTime(2011, 5, 5)), CancellationToken.None);

        Assert.True(current.IsCurrent);
        Assert.False(sameDay.IsCurrent);
        var list = await service.GetAllAsync(employee.Id, CancellationToken.None);
        Assert.Equal(new[] { 2016, 2012, 2011 }, list.Select(x => x.StartDate.Year));
    }

    [Fact]
    public void ExperienceDto_RequiresCompanyDesignationAndStartDate()
    {
        var members = Validate(new CreateEmployeeExperienceDto()).SelectMany(r => r.MemberNames).ToList();

        Assert.Contains(nameof(CreateEmployeeExperienceDto.CompanyName), members);
        Assert.Contains(nameof(CreateEmployeeExperienceDto.Designation), members);
        Assert.Contains(nameof(CreateEmployeeExperienceDto.StartDate), members);
    }
}
