using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Features.Employees;
using smartHRMS.Application.Features.Employees.Dtos;
using smartHRMS.Domain.Entities;
using smartHRMS.Tests.Fakes;
using Xunit;

namespace smartHRMS.Tests.Features.Employees;

public class EmployeePhotoTests
{
    private static readonly byte[] JpegHeader = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
    private static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };

    private static (EmployeeService Service, FakeEmployeeRepository EmployeeRepository, FakeFileStorageService FileStorageService) CreateService()
    {
        var employeeRepository = new FakeEmployeeRepository();
        var departmentRepository = new FakeDepartmentRepository();
        var designationRepository = new FakeDesignationRepository();
        var fileStorageService = new FakeFileStorageService();
        var service = new EmployeeService(employeeRepository, departmentRepository, designationRepository, fileStorageService);

        return (service, employeeRepository, fileStorageService);
    }

    private static Employee SeedEmployee(FakeEmployeeRepository repository, string? photoUrl = null)
    {
        var employee = new Employee { EmployeeCode = "EMP-001", Email = "jane.doe@example.com", PhotoUrl = photoUrl };
        repository.Employees.Add(employee);
        return employee;
    }

    private static UploadEmployeePhotoDto ValidPhoto(string fileName = "photo.jpg", string contentType = "image/jpeg", long? length = null) => new()
    {
        Content = new MemoryStream(JpegHeader),
        FileName = fileName,
        ContentType = contentType,
        Length = length ?? JpegHeader.Length,
    };

    [Fact]
    public async Task UploadPhotoAsync_WithValidJpeg_SetsPhotoUrlAndSavesFile()
    {
        var (service, employeeRepository, fileStorageService) = CreateService();
        var employee = SeedEmployee(employeeRepository);

        var result = await service.UploadPhotoAsync(employee.Id, ValidPhoto(), CancellationToken.None);

        Assert.NotNull(result.PhotoUrl);
        Assert.Contains($"{employee.Id:N}", result.PhotoUrl);
        Assert.EndsWith(".jpg", result.PhotoUrl);
        Assert.Single(fileStorageService.SavedPaths);
        Assert.NotNull(employee.UpdatedAt);
    }

    [Fact]
    public async Task UploadPhotoAsync_WithValidPng_Succeeds()
    {
        var (service, employeeRepository, _) = CreateService();
        var employee = SeedEmployee(employeeRepository);

        var photo = new UploadEmployeePhotoDto
        {
            Content = new MemoryStream(PngHeader),
            FileName = "photo.png",
            ContentType = "image/png",
            Length = PngHeader.Length,
        };

        var result = await service.UploadPhotoAsync(employee.Id, photo, CancellationToken.None);

        Assert.EndsWith(".png", result.PhotoUrl);
    }

    [Fact]
    public async Task UploadPhotoAsync_WhenEmployeeDoesNotExist_ThrowsNotFoundException()
    {
        var (service, _, _) = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.UploadPhotoAsync(Guid.NewGuid(), ValidPhoto(), CancellationToken.None));
    }

    [Fact]
    public async Task UploadPhotoAsync_WithEmptyFile_ThrowsBadRequestException()
    {
        var (service, employeeRepository, _) = CreateService();
        var employee = SeedEmployee(employeeRepository);

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.UploadPhotoAsync(employee.Id, ValidPhoto(length: 0), CancellationToken.None));
    }

    [Fact]
    public async Task UploadPhotoAsync_WithOversizedFile_ThrowsBadRequestException()
    {
        var (service, employeeRepository, _) = CreateService();
        var employee = SeedEmployee(employeeRepository);

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.UploadPhotoAsync(employee.Id, ValidPhoto(length: 10 * 1024 * 1024), CancellationToken.None));
    }

    [Fact]
    public async Task UploadPhotoAsync_WithDisallowedExtension_ThrowsBadRequestException()
    {
        var (service, employeeRepository, _) = CreateService();
        var employee = SeedEmployee(employeeRepository);

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.UploadPhotoAsync(employee.Id, ValidPhoto(fileName: "malicious.exe", contentType: "application/octet-stream"), CancellationToken.None));
    }

    [Fact]
    public async Task UploadPhotoAsync_WithDisallowedContentType_ThrowsBadRequestException()
    {
        var (service, employeeRepository, _) = CreateService();
        var employee = SeedEmployee(employeeRepository);

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.UploadPhotoAsync(employee.Id, ValidPhoto(contentType: "text/plain"), CancellationToken.None));
    }

    [Fact]
    public async Task UploadPhotoAsync_WhenContentDoesNotMatchDeclaredExtension_ThrowsBadRequestException()
    {
        var (service, employeeRepository, _) = CreateService();
        var employee = SeedEmployee(employeeRepository);

        var disguisedExecutable = new UploadEmployeePhotoDto
        {
            Content = new MemoryStream(new byte[] { 0x4D, 0x5A, 0x90, 0x00 }), // "MZ" executable header, not a JPEG
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            Length = 4,
        };

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.UploadPhotoAsync(employee.Id, disguisedExecutable, CancellationToken.None));
    }

    [Fact]
    public async Task UploadPhotoAsync_ReplacingExistingPhoto_DeletesOldFileAfterSavingNewOne()
    {
        var (service, employeeRepository, fileStorageService) = CreateService();
        var employee = SeedEmployee(employeeRepository, photoUrl: "/uploads/employees/old.jpg");

        await service.UploadPhotoAsync(employee.Id, ValidPhoto(), CancellationToken.None);

        Assert.Single(fileStorageService.SavedPaths);
        Assert.Equal(new[] { "/uploads/employees/old.jpg" }, fileStorageService.DeletedPaths);
    }

    [Fact]
    public async Task RemovePhotoAsync_WhenPhotoExists_ClearsPhotoUrlAndDeletesFile()
    {
        var (service, employeeRepository, fileStorageService) = CreateService();
        var employee = SeedEmployee(employeeRepository, photoUrl: "/uploads/employees/existing.jpg");

        var result = await service.RemovePhotoAsync(employee.Id, CancellationToken.None);

        Assert.Null(result.PhotoUrl);
        Assert.Null(employee.PhotoUrl);
        Assert.Equal(new[] { "/uploads/employees/existing.jpg" }, fileStorageService.DeletedPaths);
    }

    [Fact]
    public async Task RemovePhotoAsync_WhenNoPhotoSet_IsIdempotentAndDoesNotDeleteAnything()
    {
        var (service, employeeRepository, fileStorageService) = CreateService();
        var employee = SeedEmployee(employeeRepository);

        var result = await service.RemovePhotoAsync(employee.Id, CancellationToken.None);

        Assert.Null(result.PhotoUrl);
        Assert.Empty(fileStorageService.DeletedPaths);
    }

    [Fact]
    public async Task RemovePhotoAsync_WhenEmployeeDoesNotExist_ThrowsNotFoundException()
    {
        var (service, _, _) = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.RemovePhotoAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
