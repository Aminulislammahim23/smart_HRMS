using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Features.Employees;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;
using smartHRMS.Tests.Fakes;
using Xunit;

namespace smartHRMS.Tests.Features.Employees;

public class EmployeeProfileServiceTests
{
    [Fact]
    public async Task GetProfileAsync_CombinesEveryProfileSection()
    {
        var employees = new FakeEmployeeRepository();
        var employee = new Employee
        {
            EmployeeCode = "EMP-001",
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com",
            BasicSalary = 50000m,
            Status = EmployeeStatus.OnLeave,
            EmploymentType = EmploymentType.Contract,
            PhotoUrl = "/uploads/employees/x.jpg",
            Department = new Department { Name = "IT" },
            Designation = new Designation { Name = "Engineer" },
        };
        employee.PersonalDetails = new EmployeePersonalDetails
        {
            EmployeeId = employee.Id,
            Gender = Gender.Female,
            MaritalStatus = MaritalStatus.Married,
            BloodGroup = BloodGroup.OPositive,
            NationalId = "1234567890123",
            PassportNo = "AB1234567",
        };
        employee.Addresses.Add(new EmployeeAddress { EmployeeId = employee.Id, AddressType = AddressType.Permanent, Address = "Home", City = "Cumilla", District = "Cumilla" });
        employee.Addresses.Add(new EmployeeAddress { EmployeeId = employee.Id, AddressType = AddressType.Present, Address = "Road 1", City = "Dhaka", District = "Dhaka" });
        employee.EmergencyContacts.Add(new EmployeeEmergencyContact { EmployeeId = employee.Id, Name = "John", Relationship = "Spouse", Phone = "+8801700000000" });
        employee.Educations.Add(new EmployeeEducation { EmployeeId = employee.Id, Degree = "SSC", Institution = "School", PassingYear = 2010 });
        employee.Educations.Add(new EmployeeEducation { EmployeeId = employee.Id, Degree = "BSc", Institution = "University", PassingYear = 2016 });
        employee.Experiences.Add(new EmployeeExperience { EmployeeId = employee.Id, CompanyName = "Acme", Designation = "Dev", StartDate = new DateTime(2017, 1, 1) });
        employee.Documents.Add(new EmployeeDocument { EmployeeId = employee.Id, DocumentType = EmployeeDocumentType.Cv, FileName = "cv.pdf" });
        employee.Documents.Add(new EmployeeDocument { EmployeeId = employee.Id, DocumentType = EmployeeDocumentType.Nid, FileName = "old.pdf", IsActive = false });
        employees.Employees.Add(employee);

        var profile = await new EmployeeProfileService(employees).GetProfileAsync(employee.Id, CancellationToken.None);

        Assert.Equal("Jane Doe", profile.FullName);
        Assert.Equal("/uploads/employees/x.jpg", profile.PhotoUrl);
        Assert.True(profile.IsActive);
        Assert.True(profile.PersonalInformation.HasPersonalDetails);
        Assert.Equal("Female", profile.PersonalInformation.Gender);
        Assert.Equal("Married", profile.PersonalInformation.MaritalStatus);
        Assert.Equal("*********0123", profile.PersonalInformation.NationalId);
        Assert.Equal("******567", profile.PersonalInformation.PassportNo);
        Assert.Equal("IT", profile.JobInformation.DepartmentName);
        Assert.Equal("OnLeave", profile.JobInformation.EmploymentStatus);
        Assert.Equal(new[] { "Present", "Permanent" }, profile.Addresses.Select(a => a.AddressType));
        Assert.Single(profile.EmergencyContacts);
        Assert.Equal(new[] { 2016, 2010 }, profile.Educations.Select(e => e.PassingYear));
        Assert.True(Assert.Single(profile.Experiences).IsCurrent);
        Assert.Equal("cv.pdf", Assert.Single(profile.Documents).FileName);
    }

    [Fact]
    public async Task GetProfileAsync_WithoutProfileRecords_ReturnsEmptySections()
    {
        var employees = new FakeEmployeeRepository();
        var employee = new Employee { EmployeeCode = "EMP-002", FirstName = "Sam", LastName = "Lee", Email = "sam@example.com" };
        employees.Employees.Add(employee);

        var profile = await new EmployeeProfileService(employees).GetProfileAsync(employee.Id, CancellationToken.None);

        Assert.False(profile.PersonalInformation.HasPersonalDetails);
        Assert.Null(profile.PersonalInformation.NationalId);
        Assert.Empty(profile.Addresses);
        Assert.Empty(profile.EmergencyContacts);
        Assert.Empty(profile.Educations);
        Assert.Empty(profile.Experiences);
        Assert.Empty(profile.Documents);
    }

    [Fact]
    public async Task GetProfileAsync_WhenEmployeeMissing_ThrowsNotFound()
    {
        var service = new EmployeeProfileService(new FakeEmployeeRepository());

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetProfileAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
