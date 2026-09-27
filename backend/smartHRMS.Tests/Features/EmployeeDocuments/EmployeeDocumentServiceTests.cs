using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Features.EmployeeDocuments;
using smartHRMS.Application.Features.EmployeeDocuments.Dtos;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;
using smartHRMS.Tests.Fakes;
using Xunit;

namespace smartHRMS.Tests.Features.EmployeeDocuments;

public class EmployeeDocumentServiceTests
{
    private static readonly byte[] PdfBytes = "%PDF-1.4 test"u8.ToArray();

    private sealed record Setup(
        EmployeeDocumentService Service,
        FakeEmployeeDocumentRepository Documents,
        FakeDocumentStorageService Storage,
        Employee Employee);

    private static Setup CreateService(long maxFileSizeBytes = 1024)
    {
        var employees = new FakeEmployeeRepository();
        var employee = new Employee { EmployeeCode = "EMP-001", Email = "jane@example.com" };
        employees.Employees.Add(employee);

        var documents = new FakeEmployeeDocumentRepository();
        var storage = new FakeDocumentStorageService();
        var options = new EmployeeDocumentOptions
        {
            MaxFileSizeBytes = maxFileSizeBytes,
            AllowedExtensions = new[] { ".pdf", ".docx", ".jpg", ".png" },
        };

        return new Setup(new EmployeeDocumentService(documents, employees, storage, options), documents, storage, employee);
    }

    private static UploadEmployeeDocumentDto Upload(string fileName = "nid.pdf", byte[]? bytes = null, string? documentType = "Nid", string? description = null)
    {
        bytes ??= PdfBytes;
        return new UploadEmployeeDocumentDto
        {
            Content = new MemoryStream(bytes),
            FileName = fileName,
            Length = bytes.Length,
            DocumentType = documentType,
            Description = description,
        };
    }

    [Fact]
    public async Task UploadAsync_WithValidPdf_StoresFileAndMetadata()
    {
        var s = CreateService();

        var result = await s.Service.UploadAsync(s.Employee.Id, Upload(description: "  National ID  "), CancellationToken.None);

        var stored = Assert.Single(s.Documents.Documents);
        Assert.Equal(EmployeeDocumentType.Nid, stored.DocumentType);
        Assert.Equal("nid.pdf", stored.FileName);
        Assert.Equal("application/pdf", stored.ContentType);
        Assert.Equal("National ID", stored.Description);
        Assert.True(stored.IsActive);
        Assert.Equal(PdfBytes, s.Storage.Files[stored.FilePath]);
        Assert.StartsWith($"{s.Employee.Id:N}/", stored.FilePath);
        Assert.DoesNotContain("nid", stored.FilePath);
        Assert.Equal($"/api/employees/{s.Employee.Id}/documents/{stored.Id}/download", result.DownloadUrl);
    }

    [Fact]
    public async Task UploadAsync_WhenEmployeeMissing_ThrowsNotFound()
    {
        var s = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.UploadAsync(Guid.NewGuid(), Upload(), CancellationToken.None));
        Assert.Empty(s.Storage.Files);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Resume")]
    [InlineData("3")]
    public async Task UploadAsync_WithInvalidDocumentType_ThrowsBadRequest(string? documentType)
    {
        var s = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            s.Service.UploadAsync(s.Employee.Id, Upload(documentType: documentType), CancellationToken.None));
    }

    [Theory]
    [InlineData("nid")]
    [InlineData("NID")]
    [InlineData("educationalcertificate")]
    public async Task UploadAsync_AcceptsDocumentTypeNamesCaseInsensitively(string documentType)
    {
        var s = CreateService();

        await s.Service.UploadAsync(s.Employee.Id, Upload(documentType: documentType), CancellationToken.None);

        Assert.Single(s.Documents.Documents);
    }

    [Theory]
    [InlineData("virus.exe")]
    [InlineData("script.ps1")]
    [InlineData("run.bat")]
    [InlineData("lib.dll")]
    [InlineData("noextension")]
    [InlineData("old.doc")] // valid format, but not in this test's allowed list
    public async Task UploadAsync_WithDisallowedExtension_ThrowsBadRequest(string fileName)
    {
        var s = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            s.Service.UploadAsync(s.Employee.Id, Upload(fileName: fileName), CancellationToken.None));
        Assert.Empty(s.Storage.Files);
    }

    [Fact]
    public async Task UploadAsync_WhenContentDoesNotMatchExtension_ThrowsBadRequest()
    {
        var s = CreateService();
        var executableBytes = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 }; // "MZ" header

        await Assert.ThrowsAsync<BadRequestException>(() =>
            s.Service.UploadAsync(s.Employee.Id, Upload(fileName: "cv.pdf", bytes: executableBytes), CancellationToken.None));
    }

    [Fact]
    public async Task UploadAsync_WhenTooLarge_ThrowsBadRequest()
    {
        var s = CreateService(maxFileSizeBytes: 10);

        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.UploadAsync(s.Employee.Id, Upload(), CancellationToken.None));
    }

    [Fact]
    public async Task UploadAsync_WhenEmpty_ThrowsBadRequest()
    {
        var s = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            s.Service.UploadAsync(s.Employee.Id, Upload(bytes: Array.Empty<byte>()), CancellationToken.None));
    }

    [Theory]
    [InlineData("../../etc/passwd.pdf", "passwd.pdf")]
    [InlineData(@"C:\Windows\System32\evil.pdf", "evil.pdf")]
    [InlineData("..pdf", "document.pdf")]
    [InlineData("my cv.PDF", "my cv.pdf")]
    public async Task UploadAsync_SanitizesTheDisplayFileName(string fileName, string expected)
    {
        var s = CreateService();

        await s.Service.UploadAsync(s.Employee.Id, Upload(fileName: fileName), CancellationToken.None);

        Assert.Equal(expected, s.Documents.Documents.Single().FileName);
    }

    [Fact]
    public async Task UploadAsync_WhenSavingMetadataFails_DeletesTheStoredFile()
    {
        var s = CreateService();
        s.Documents.SaveChangesException = new InvalidOperationException("database down");

        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Service.UploadAsync(s.Employee.Id, Upload(), CancellationToken.None));

        Assert.Empty(s.Storage.Files);
        Assert.Single(s.Storage.DeletedKeys);
    }

    [Fact]
    public async Task GetByIdAsync_ThroughAnotherEmployee_ThrowsNotFound()
    {
        var s = CreateService();
        var document = await s.Service.UploadAsync(s.Employee.Id, Upload(), CancellationToken.None);
        var otherEmployeeId = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.GetByIdAsync(otherEmployeeId, document.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.DownloadAsync(otherEmployeeId, document.Id, CancellationToken.None));
    }

    [Fact]
    public async Task DownloadAsync_ReturnsTheStoredBytesWithServerContentType()
    {
        var s = CreateService();
        var document = await s.Service.UploadAsync(s.Employee.Id, Upload(), CancellationToken.None);

        var file = await s.Service.DownloadAsync(s.Employee.Id, document.Id, CancellationToken.None);

        using var copy = new MemoryStream();
        await file.Content.CopyToAsync(copy);
        Assert.Equal(PdfBytes, copy.ToArray());
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("nid.pdf", file.FileName);
    }

    [Fact]
    public async Task DeactivateAsync_SoftDeletesAndKeepsTheFile()
    {
        var s = CreateService();
        var document = await s.Service.UploadAsync(s.Employee.Id, Upload(), CancellationToken.None);

        await s.Service.DeactivateAsync(s.Employee.Id, document.Id, CancellationToken.None);
        await s.Service.DeactivateAsync(s.Employee.Id, document.Id, CancellationToken.None); // idempotent

        var stored = s.Documents.Documents.Single();
        Assert.False(stored.IsActive);
        Assert.NotNull(stored.UpdatedAt);
        Assert.Single(s.Storage.Files);
        Assert.Empty(await s.Service.GetByEmployeeAsync(s.Employee.Id, includeInactive: false, CancellationToken.None));
        Assert.Single(await s.Service.GetByEmployeeAsync(s.Employee.Id, includeInactive: true, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.DownloadAsync(s.Employee.Id, document.Id, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_ChangesTypeAndDescription()
    {
        var s = CreateService();
        var document = await s.Service.UploadAsync(s.Employee.Id, Upload(description: "old"), CancellationToken.None);

        var result = await s.Service.UpdateAsync(
            s.Employee.Id, document.Id, new UpdateEmployeeDocumentDto { DocumentType = EmployeeDocumentType.Passport, Description = " new " }, CancellationToken.None);

        Assert.Equal("Passport", result.DocumentType);
        Assert.Equal("new", result.Description);
    }

    [Fact]
    public async Task UpdateAsync_WithoutDocumentType_KeepsIt()
    {
        var s = CreateService();
        var document = await s.Service.UploadAsync(s.Employee.Id, Upload(documentType: "Cv"), CancellationToken.None);

        var result = await s.Service.UpdateAsync(s.Employee.Id, document.Id, new UpdateEmployeeDocumentDto(), CancellationToken.None);

        Assert.Equal("Cv", result.DocumentType);
        Assert.Null(result.Description);
    }

    [Fact]
    public async Task GetByEmployeeAsync_WhenEmployeeMissing_ThrowsNotFound()
    {
        var s = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.GetByEmployeeAsync(Guid.NewGuid(), false, CancellationToken.None));
    }
}
