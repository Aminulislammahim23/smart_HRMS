# Smart HRMS Backend Documentation

## 1. Overview

This project is the backend API for a Human Resource Management System, built with ASP.NET Core and Entity Framework Core using Clean Architecture. As of Day 1–11 it provides a stable foundation (project structure, database schema, domain layer, error handling) plus: Employees (CRUD, job information, profile photo), a complete Employee Profile (personal details, addresses, emergency contacts, education, experience, documents, one-call profile view), Departments and Designations.

- ASP.NET Core Web API (.NET 10)
- Entity Framework Core with SQL Server
- Clean Architecture: Domain → Application → Infrastructure → API
- DI-based service/repository registration per layer
- Code-first EF Core migrations
- Standard `ApiResponse<T>` envelope (`success` / `message` / `data` / `errors`) for every success and error response
- Centralized exception handling (`AppExceptionHandler`) that returns the standard envelope
- DTO validation with Data Annotations (plus a reusable `[NotDefault]` attribute for `Guid`/`DateTime`)
- Swagger UI (Development only) over the built-in OpenAPI document, including working file-upload testing
- Employee profile photo support: local disk storage behind a swappable `IFileStorageService` abstraction
- Department & Designation management with soft-delete deactivation and delete protection
- Employee document management: private storage outside `wwwroot`, configurable allowed types/size, content-signature checks, soft delete
- xUnit test project (150 unit tests) for the Employee/Profile/Document/Department/Designation services, DTO validation, `ApiResponse` and photo upload/removal

### Development history

| Day | Scope |
|-----|-------|
| 1 | Solution and project setup (.NET 10 Web API) |
| 2 | Database design: `Department`, `Designation`, `Employee`, `ApplicationUser` entities |
| 3 | Clean Architecture split into Domain / Application / Infrastructure / API |
| 4–5 | Backend foundation: DI per layer, EF Core configurations + migrations, `ApiResponse<T>`, exception handling, validation |
| 6 | Employee workflow end-to-end + profile photo storage |
| 7 | Employee CRUD audit and fixes: input trimming, blank phone accepted, UTC audit timestamps, dead DTOs removed |
| 8 | Department & Designation management (CRUD, delete safety, active-status rules for employee assignment) |
| 9 | Employee management completed: `Address`, `Gender`, `EmploymentType`; computed `fullName`/`isActive`; status changes and reactivation via PUT; enums as strings in JSON; unique-index races mapped to `409`; JSON errors no longer expose .NET type names |
| 10 | Employee profile & documents: blood group, permanent address, emergency contact, basic salary; `GET /api/employees/{id}/profile`; `EmployeeDocuments` module (upload, list, metadata, download, update, soft delete) with private storage; unique photo filename per upload |
| 11 | Complete employee profile: `EmployeePersonalDetails` (0..1), `EmployeeAddresses` (Present/Permanent), `EmployeeEmergencyContacts`, `EmployeeEducations`, `EmployeeExperiences` with nested CRUD APIs; the Day 9–10 flat address / emergency-contact / gender / blood-group columns moved into these tables; profile endpoint extended; `ExperienceCertificate` document type |

---

## 2. Technology Stack

### Backend Framework
- ASP.NET Core 10 / C#
- ASP.NET Core Web API (`Microsoft.NET.Sdk.Web`)

### Database
- Microsoft SQL Server (local instance: `MAHIM\SQLEXPRESS`)
- EF Core 10 + EF Core SQL Server provider

### Testing
- xUnit + `Microsoft.NET.Test.Sdk`

### Development Tools
- .NET SDK 10
- Entity Framework Core CLI (`dotnet ef`)

---

## 3. Project Structure

```text
backend/
  smartHRMS.slnx
  smartHRMS.Api/                        (Microsoft.NET.Sdk.Web)
    Program.cs
    appsettings.json
    appsettings.Development.json
    smartHRMS.Api.http                  (sample requests for the IDE HTTP client)
    Controllers/
      HealthController.cs
      EmployeesController.cs
      DepartmentsController.cs
      DesignationsController.cs
      EmployeeDocumentsController.cs    (/api/employees/{employeeId}/documents)
      EmployeePersonalDetailsController.cs, EmployeeAddressesController.cs, EmployeeEmergencyContactsController.cs,
      EmployeeEducationsController.cs, EmployeeExperiencesController.cs   (Day 11 profile records)
    Extensions/
      ApiServiceCollectionExtensions.cs (AddApiControllers: validation-error envelope, enums as strings)
    Middleware/
      AppExceptionHandler.cs
      StatusCodeResponseWriter.cs       (envelope for body-less 404/405/415)
    OpenApi/
      FormFileOperationTransformer.cs   (fixes IFormFile -> multipart/form-data in Swagger, incl. extra form fields)
    Properties/
      launchSettings.json
    wwwroot/
      uploads/.gitkeep                  (keeps the folder in git; uploaded files are gitignored)
      uploads/employees/                (photo storage root; served as static files)
    App_Data/employee-documents/       (PRIVATE document storage; not served, gitignored)

  SmartHRMS.Application/                (class library, no EF Core dependency)
    DependencyInjection.cs              (AddApplication)
    Common/
      Exceptions/
        NotFoundException.cs
        ConflictException.cs
        BadRequestException.cs
      Models/
        ApiResponse.cs                  (ApiResponse<T> + ApiResponse shortcuts)
      Text/
        InputText.cs                    (trim / blank-to-null helpers)
      Files/
        FileSignature.cs                (shared magic-byte check for photos and documents)
      Validation/
        NotDefaultAttribute.cs          ([NotDefault] for Guid / DateTime)
    Interfaces/
      IEmployeeRepository.cs
      IDepartmentRepository.cs
      IDesignationRepository.cs
      IFileStorageService.cs            (storage-agnostic file save/delete abstraction)
      IDocumentStorageService.cs        (private document storage: save/open/delete)
      IEmployeeDocumentRepository.cs
      IEmployeeOwnedRepository.cs       (generic, employee-scoped repository for profile records)
    Features/
      Employees/
        IEmployeeService.cs
        EmployeeService.cs
        EmployeePhotoPolicy.cs          (allowed types/size + magic-byte signature check)
        EmployeeStatusRules.cs          (which statuses count as "current" employees)
        IEmployeeProfileService.cs
        EmployeeProfileService.cs       (read-only profile: employee + every profile record)
        EmployeeOwnedRecordService.cs   (shared list/get/create/update/delete workflow for profile records)
        EmployeeRepositoryExtensions.cs (EnsureExistsAsync guard)
        Dtos/
          EmployeeDto.cs
          CreateEmployeeDto.cs
          UpdateEmployeeDto.cs
          UploadEmployeePhotoDto.cs     (Stream + metadata; no IFormFile in Application)
          EmployeeProfileDto.cs         (profile + PersonalInformation + JobInformation sections)
      EmployeePersonalDetails/  EmployeeAddresses/  EmployeeEmergencyContacts/  EmployeeEducations/  EmployeeExperiences/
                                        (Day 11: I<Name>Service.cs, <Name>Service.cs, Dtos/ Create…Dto, Update…Dto, …Dto)
      EmployeeDocuments/
        IEmployeeDocumentService.cs
        EmployeeDocumentService.cs
        EmployeeDocumentOptions.cs      (bound from appsettings "EmployeeDocuments")
        EmployeeDocumentPolicy.cs       (verifiable formats, content types, startup validation)
        Dtos/                           (EmployeeDocumentDto, UploadEmployeeDocumentDto, UpdateEmployeeDocumentDto, EmployeeDocumentFileDto)
      Departments/
        IDepartmentService.cs
        DepartmentService.cs
        Dtos/                           (DepartmentDto, CreateDepartmentDto, UpdateDepartmentDto)
      Designations/                    (same shape as Departments/)

  SmartHRMS.Domain/                      (class library, no dependencies)
    Common/
      BaseEntity.cs
      EmployeeOwnedEntity.cs            (BaseEntity + EmployeeId, for profile records)
    Entities/
      Department.cs
      Designation.cs
      Employee.cs
      ApplicationUser.cs
      EmployeeDocument.cs
      EmployeePersonalDetails.cs, EmployeeAddress.cs, EmployeeEmergencyContact.cs,
      EmployeeEducation.cs, EmployeeExperience.cs
    Enums/
      EmployeeStatus.cs
      BloodGroup.cs
      MaritalStatus.cs
      AddressType.cs
      EmployeeDocumentType.cs
      EmploymentType.cs
      Gender.cs

  SmartHRMS.Infrastructure/              (class library)
    DependencyInjection.cs              (AddInfrastructure)
    Persistence/
      SmartHRMSDbContext.cs
      Configurations/
        DepartmentConfiguration.cs
        DesignationConfiguration.cs
        EmployeeConfiguration.cs
        ApplicationUserConfiguration.cs
        EmployeeDocumentConfiguration.cs
        EmployeePersonalDetailsConfiguration.cs, EmployeeAddressConfiguration.cs, EmployeeEmergencyContactConfiguration.cs,
        EmployeeEducationConfiguration.cs, EmployeeExperienceConfiguration.cs
    Repositories/
      EmployeeRepository.cs
      DepartmentRepository.cs
      DesignationRepository.cs
      EmployeeDocumentRepository.cs
      EmployeeOwnedRepository.cs        (generic IEmployeeOwnedRepository<T> implementation)
    Storage/
      LocalFileStorageService.cs        (IFileStorageService impl: saves under wwwroot)
      LocalDocumentStorageService.cs    (IDocumentStorageService impl: saves under App_Data)
      SafeStoragePath.cs                (keeps every resolved path inside its storage root)
    Migrations/

  smartHRMS.Tests/                       (xUnit test project)
    Fakes/
      FakeEmployeeRepository.cs
      FakeDepartmentRepository.cs
      FakeDesignationRepository.cs
      FakeEmployeeDocumentRepository.cs
      FakeDocumentStorageService.cs
      FakeEmployeeOwnedRepository.cs
      FakeFileStorageService.cs
    Common/
      ApiResponseTests.cs
    Features/
      Employees/
        EmployeeServiceTests.cs
        EmployeeDtoValidationTests.cs
        EmployeePhotoTests.cs
        EmployeeProfileServiceTests.cs
      Departments/
        DepartmentServiceTests.cs
        DepartmentDtoValidationTests.cs (covers Department + Designation DTOs)
      Designations/
        DesignationServiceTests.cs
      EmployeeDocuments/
        EmployeeDocumentServiceTests.cs
      EmployeeProfile/
        EmployeeProfileRecordTests.cs   (personal details, addresses, contacts, education, experience)
```

> **Folder-name casing:** the folders on disk are `SmartHRMS.Application`, `SmartHRMS.Domain` and `SmartHRMS.Infrastructure` (capital S) while the project files inside are `smartHRMS.*.csproj`. `smartHRMS.slnx` and every `ProjectReference` use exactly these paths (fixed in the Day 1–10 audit; verified by building in a case-sensitive folder), so the solution also builds on Linux/macOS/Docker/CI. Keep the exact casing when adding references.

### Dependency direction (verified, no circular references)

```text
smartHRMS.Api
    -> SmartHRMS.Application
    -> SmartHRMS.Infrastructure

SmartHRMS.Application
    -> SmartHRMS.Domain

SmartHRMS.Infrastructure
    -> SmartHRMS.Domain
    -> SmartHRMS.Application   (implements Application's repository interfaces)

smartHRMS.Tests
    -> SmartHRMS.Application
    -> SmartHRMS.Domain
```

`Domain` has zero project dependencies. `Application` has no EF Core dependency — it only knows about repository *interfaces*, which `Infrastructure` implements. This keeps business logic (in `EmployeeService`) framework-agnostic.

---

## 4. Application Startup

Configured in `smartHRMS.Api/Program.cs`:

```csharp
builder.Logging.ClearProviders();
builder.Logging.AddConsole();              // console logging only

builder.Services.AddApiControllers();      // AddControllers + envelope for validation errors
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi(options => options.AddOperationTransformer<FormFileOperationTransformer>());

builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddApplication();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

// wwwroot is the only folder served as static content (below), so uploaded files never expose
// source, configuration, or any other part of the application.
var webRootPath = string.IsNullOrWhiteSpace(builder.Environment.WebRootPath)
    ? Path.Combine(builder.Environment.ContentRootPath, "wwwroot")
    : builder.Environment.WebRootPath;
builder.Services.AddInfrastructure(connectionString, webRootPath);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages(StatusCodeResponseWriter.WriteAsync);
app.UseStaticFiles();                       // serves wwwroot/uploads/... at /uploads/...
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                       // /openapi/v1.json
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "SmartHRMS API v1"));   // /swagger
}
if (!string.IsNullOrWhiteSpace(builder.Configuration["https_port"]) ||
    !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_HTTPS_PORT")))
{
    app.UseHttpsRedirection();
}
app.UseAuthorization();
app.MapControllers();
```

Swagger UI (`Swashbuckle.AspNetCore.SwaggerUI`) is served at `/swagger` in Development only; the OpenAPI document itself comes from the built-in `Microsoft.AspNetCore.OpenApi`. When JWT is added later, register a Bearer security scheme with an OpenAPI document transformer and the **Authorize** button will appear automatically.

Each layer registers its own services through a `DependencyInjection.cs` extension method (`AddApplication()`, `AddInfrastructure(connectionString, fileStorageRootPath)`), rather than wiring `DbContext`/repositories directly in the API project. `fileStorageRootPath` is resolved from `IWebHostEnvironment.WebRootPath` in `Program.cs` (falling back to `<ContentRoot>/wwwroot` if unset) and passed in — `Infrastructure` never reads it from configuration itself, keeping it swappable per environment.

### Database configuration

`appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=MAHIM\\SQLEXPRESS;Database=SmartHRMSDB;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

Uses Windows/Trusted authentication against the local `SQLEXPRESS` instance — no credentials are stored in configuration.

---

## 5. Database Context

`SmartHRMSDbContext` (namespace `smartHRMS.Infrastructure.Persistence`) is the EF Core context, registered via `SmartHRMS.Infrastructure/DependencyInjection.cs`.

### DbSets
- `Departments`
- `Designations`
- `Employees`
- `ApplicationUsers`
- `EmployeeDocuments`
- `EmployeePersonalDetails`, `EmployeeAddresses`, `EmployeeEmergencyContacts`, `EmployeeEducations`, `EmployeeExperiences`

### Configuration
Entity configurations live under `Persistence/Configurations/` as `IEntityTypeConfiguration<T>` classes, applied via:

```csharp
modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmartHRMSDbContext).Assembly);
```

`Employee.DepartmentId`, `Employee.DesignationId`, and `ApplicationUser.EmployeeId` all use `DeleteBehavior.Restrict` to avoid multiple cascade-path conflicts in SQL Server.

### Unique-constraint violations → 409
Services check uniqueness (employee code, email, department/designation name) before saving, but two simultaneous requests can both pass that check. `SmartHRMSDbContext.SaveChangesAsync` catches the resulting SQL Server unique-index error (`2601`/`2627`) and throws the Application layer's `ConflictException`, so the client gets a `409` with the standard envelope instead of a `500`. No database exception text ever reaches the client.

### UTC audit timestamps
`CreatedAt`/`UpdatedAt` are always written in UTC, but SQL Server's `datetime2` does not store a `DateTimeKind`, so EF would read them back as `Unspecified` and they would serialize without a trailing `Z` (clients would treat them as local time). `OnModelCreating` therefore applies a value converter to `CreatedAt`/`UpdatedAt` on every `BaseEntity` that marks them as UTC when read. It is deliberately **not** applied to date-only values such as `DateOfBirth`/`JoiningDate`, which stay unzoned. The converter does not change the schema, so it needed no migration.

---

## 6. Core Domain Models

### 6.1 BaseEntity
- `Id` (`Guid`, generated on creation)
- `CreatedAt` (`DateTime`, UTC, set on creation)
- `UpdatedAt` (`DateTime?`, UTC, set by the services on every update, soft delete and photo change)

### 6.2 Department
- `Name` (required, max 100, unique)
- `Description` (optional, max 500)
- `IsActive` (default `true`)
- `Employees` (collection navigation)

### 6.3 Designation
- `Name` (required, max 100, unique)
- `Description` (optional, max 500)
- `IsActive` (default `true`)
- `Employees` (collection navigation)

### 6.4 Employee
- `EmployeeCode` (required, max 50, unique)
- `FirstName`, `LastName` (required, max 100)
- `Email` (required, max 200, unique)
- `Phone` (optional, max 30)
- `BasicSalary` (optional `decimal(18,2)`, never negative)
- `DateOfBirth`, `JoiningDate`
- `EmploymentType` (`EmploymentType` enum, stored as string, required, default `FullTime`: `FullTime`, `PartTime`, `Contract`, `Intern`)
- `DepartmentId` / `Department` (required FK, `Restrict`)
- `DesignationId` / `Designation` (required FK, `Restrict`)
- `Status` (`EmployeeStatus` enum, stored as string, default `Active`)
- `PhotoUrl` (optional, max 500) — relative URL of the profile photo, e.g. `/uploads/employees/{id}.jpg`; `null` until a photo is uploaded. Stores a reference only, never image bytes and never a physical path, so the storage backend can change without touching this entity (see §9a).
- `ApplicationUser` (optional 1:1 navigation)
- Profile navigations (Day 11): `PersonalDetails` (0..1), `Addresses`, `EmergencyContacts`, `Educations`, `Experiences`, `Documents` — see §6.8

> `EmployeeCode` is set on create and cannot be changed afterwards (it is not part of `UpdateEmployeeDto`).

> **Day 11 change:** gender, blood group, present/permanent address and the single emergency contact are no longer columns on `Employee`. They were moved (all 10 existing employees had them empty, so nothing was lost) into the dedicated profile tables in §6.8, which are now the only place they are stored. `DateOfBirth` stays on `Employee` because it is required at hiring time and used by the joining-date rule.

> `Status` (the employment status) is the single source of truth for whether an employee is active — there is intentionally no separate `IsActive` column on `Employee` (unlike `Department`/`Designation`), avoiding duplicate/contradictory state. API responses include a computed `isActive` (`true` when `Status` is `Active` or `OnLeave`) and a computed `fullName`; neither is stored.

### 6.5 EmployeeStatus (enum)
`Active = 1`, `Inactive = 2`, `Resigned = 3`, `Terminated = 4`, `OnLeave = 5`

### 6.6 ApplicationUser
- `EmployeeId` (required FK, unique — one user per employee)
- `Username` (required, max 100, unique)
- `PasswordHash` (required — never a plain-text password)
- `IsActive` (default `true`)
- `Employee` (navigation)

> The `ApplicationUsers` table exists (from `InitialCreate`) but has no service or API yet — it is the foundation for the upcoming authentication work.

### 6.7 EmployeeDocument
- `EmployeeId` (required FK → `Employee`, `Restrict`)
- `DocumentType` (`EmployeeDocumentType` enum, stored as string: `Nid`, `Passport`, `EducationalCertificate`, `Cv`, `JoiningLetter`, `ContractPaper`, `Other`)
- `FileName` (required, max 255) — sanitized original name, used only as the download name
- `FilePath` (required, max 500) — server-generated storage key (`{employeeId:N}/{random}.pdf`) relative to the private document root; **never returned to clients**
- `ContentType` (required, max 100) — set by the server from the validated extension, not taken from the client
- `FileSizeBytes`, `Description` (optional, max 500)
- `IsActive` (default `true`) — soft delete flag
- `CreatedAt` is the upload time (returned as `uploadedAt`)
- Index on (`EmployeeId`, `IsActive`) for the per-employee document list

### 6.8 Employee profile records (Day 11)

All inherit `EmployeeOwnedEntity` (`BaseEntity` + required `EmployeeId` FK → `Employee`, `Restrict`). They are hard-deleted through their own endpoints (they are profile details, not HR history like documents).

| Entity / table | Cardinality | Fields | Database rules |
|---|---|---|---|
| `EmployeePersonalDetails` | 0..1 per employee | `Gender`, `MaritalStatus` (`Single`, `Married`, `Divorced`, `Widowed`, `Separated`), `BloodGroup`, `Nationality` (100), `NationalId` (17), `PassportNo` (20) — enums stored as strings | unique `EmployeeId`; unique `NationalId` and `PassportNo` where not null |
| `EmployeeAddresses` | many (max one per type) | `AddressType` (`Present`/`Permanent`), `Address` (500, required), `City` (100, required), `District` (100, required), `PostalCode` (20) | unique (`EmployeeId`, `AddressType`) |
| `EmployeeEmergencyContacts` | many | `Name` (150), `Relationship` (50), `Phone` (30) required; `Email` (200), `Address` (500) optional | index on `EmployeeId` |
| `EmployeeEducations` | many | `Degree` (100) and `Institution` (200) required, free text (no fixed degree list); `Major` (150), `Result` (50); `PassingYear` (int, required) | index on `EmployeeId` |
| `EmployeeExperiences` | many | `CompanyName` (200), `Designation` (150, free-text job title), `StartDate` required; `EndDate` nullable (null = current job); `Responsibilities` (2000) | index on `EmployeeId` |

`DateOfBirth` is deliberately not repeated in `EmployeePersonalDetails`; the personal-details API shows it read-only from `Employee`.

---

## 7. Entity Relationships

- `Department` 1 — * `Employee`
- `Designation` 1 — * `Employee`
- `Employee` 1 — 0..1 `ApplicationUser`
- `Employee` 1 — * `EmployeeDocument`
- `Employee` 1 — 0..1 `EmployeePersonalDetails`
- `Employee` 1 — * `EmployeeAddress` (at most one `Present` and one `Permanent`)
- `Employee` 1 — * `EmployeeEmergencyContact`, `EmployeeEducation`, `EmployeeExperience`

All foreign keys use `DeleteBehavior.Restrict`: deleting a Department/Designation/Employee that still has dependents is blocked at the database level rather than silently cascading.

---

## 8. Database Migration Workflow

Run these from the `backend/` directory (project/startup-project names differ from the folder defaults, so pass them explicitly):

### Generate a migration
```bash
dotnet ef migrations add <MigrationName> --project SmartHRMS.Infrastructure --startup-project smartHRMS.Api
```

### Apply migrations to the database
```bash
dotnet ef database update --project SmartHRMS.Infrastructure --startup-project smartHRMS.Api
```

### Remove the last migration
```bash
dotnet ef migrations remove --project SmartHRMS.Infrastructure --startup-project smartHRMS.Api
```

### Applied migrations
1. `InitialCreate` — Departments, Designations, Employees, ApplicationUsers tables, indexes, FKs.
2. `UpdateEmployeeStatusToEnum` — narrows `Employees.Status` to `nvarchar(20)` backed by `EmployeeStatus`, drops the redundant `IsActive` column from `Employees`.
3. `AddEmployeePhotoUrl` — adds nullable `Employees.PhotoUrl nvarchar(500)`. Purely additive; no data was touched or lost.

4. `AddEmployeeProfileFields` (Day 9) — adds `Employees.Address nvarchar(500) NULL`, `Employees.Gender nvarchar(20) NULL` and `Employees.EmploymentType nvarchar(20) NOT NULL DEFAULT 'FullTime'`. Purely additive: existing employees keep all data and become `FullTime`. A verified copy-only backup (`SmartHRMSDB_before_Day9_20260927.bak` in the SQL Server default backup folder) was taken before applying it.

5. `AddEmployeeProfileAndDocuments` (Day 10) — adds nullable `Employees` columns `BasicSalary decimal(18,2)`, `BloodGroup nvarchar(20)`, `EmergencyContactName nvarchar(150)`, `EmergencyContactPhone nvarchar(30)`, `EmergencyContactRelationship nvarchar(50)`, `PermanentAddress nvarchar(500)`, and creates `EmployeeDocuments` (FK to `Employees` with `NO ACTION`, index on `EmployeeId, IsActive`). Purely additive; a before/after checksum confirmed existing employee data was unchanged. Backup taken first: `SmartHRMSDB_before_Day10_20260927.bak`.

6. `AddEmployeeProfileRecords` (Day 11) — creates `EmployeePersonalDetails`, `EmployeeAddresses`, `EmployeeEmergencyContacts`, `EmployeeEducations`, `EmployeeExperiences` (all FKs `NO ACTION`, indexes as in §6.8) and **drops** `Employees.Address`, `PermanentAddress`, `Gender`, `BloodGroup`, `EmergencyContactName`, `EmergencyContactPhone`, `EmergencyContactRelationship`. The drops were reviewed and approved: every existing row had those columns empty (verified immediately before applying), and a checksum of all remaining employee data was identical before and after. Backup taken first: `SmartHRMSDB_before_Day11_20260928.bak`.

Day 7 and Day 8 required **no migration**: the `Departments`/`Designations` tables, their unique `Name` indexes and the `Employees` foreign keys already existed from `InitialCreate`, and the UTC converter does not change the schema. `dotnet ef migrations has-pending-model-changes` reports no changes.

---

## 9. API Routes and Usage

### Base URL
```text
http://localhost:5099
https://localhost:7074
```
(Ports come from `Properties/launchSettings.json`; may vary by environment.)

### Health check
```text
GET /api/health
```
```json
{
  "success": true,
  "message": "Service is healthy.",
  "data": { "status": "Healthy", "application": "smartHRMS", "version": "1.0.0" },
  "errors": null
}
```

### Standard response envelope

Every endpoint returns the same shape (`ApiResponse<T>` in `SmartHRMS.Application/Common/Models`):

```json
{ "success": true,  "message": "...", "data": { }, "errors": null }
{ "success": false, "message": "...", "data": null, "errors": ["..."] }
```

Controllers build it with `ApiResponse<T>.Ok(data, message)` / `ApiResponse.Ok(message)`; errors are produced centrally (see *Error responses*).

### Employees

| Method | Route                | Description                                   |
|--------|-----------------------|------------------------------------------------|
| GET    | `/api/employees`      | List all employees                             |
| GET    | `/api/employees/{id}` | Get one employee by id (404 if not found)      |
| GET    | `/api/employees/{id}/profile` | Full profile in one call: personal information, job information, addresses, emergency contacts, education, experience, photo and active documents — see §9c |
| POST   | `/api/employees`      | Create an employee                             |
| PUT    | `/api/employees/{id}` | Update an employee                             |
| DELETE | `/api/employees/{id}` | Deactivate (soft delete) — sets `Status=Inactive`, no row is removed. Returns `200` with the envelope (no `data`) |
| PUT    | `/api/employees/{id}/photo` | Upload or replace the employee's profile photo (`multipart/form-data`, field name `photo`) — see §9a |
| DELETE | `/api/employees/{id}/photo` | Remove the employee's profile photo, if any. Does **not** delete the employee — see §9a |

Required on create/update: `firstName`, `lastName`, `email` (valid), `dateOfBirth`, `joiningDate`, `departmentId`, `designationId` (an omitted/empty Guid or default date is rejected by `[NotDefault]`). `employeeCode` is required on create only and ignored on update.

Input handling rules:
- `employeeCode`, `email`, `firstName`, `lastName` are **trimmed** before the uniqueness checks and before saving, so `" EMP-001"` is treated as a duplicate of `"EMP-001"`.
- `employeeCode` and `email` must be unique; the comparison is case-insensitive (SQL Server's default collation), so `emp-001` also conflicts with `EMP-001` (`409`).
- `phone` is optional. An empty or whitespace value (e.g. `""` from a blank form field) is stored as `null`; any other value must be a valid phone number (`400`).
- `departmentId`/`designationId` must exist and be **active** (`400`). The active check only applies to a new assignment — see §9b.
- `employmentType` is optional: on create it defaults to `FullTime`; on update, omitting it keeps the current value.
- `status` can only be set on update and is optional (omitting it keeps the current value). This is how an employee is **reactivated**: moving from `Inactive`/`Resigned`/`Terminated` back to `Active`/`OnLeave` counts as a fresh assignment, so the department and designation must be active (`400` otherwise). `DELETE /api/employees/{id}` remains the shortcut for `status = Inactive`.
- Enum fields (`employmentType`, `status`, and the enums in the profile endpoints such as `gender`, `bloodGroup`, `addressType`) are sent and returned as **names** (`"PartTime"`, `"Female"`), case-insensitive on input. Numbers (`1`) and unknown names are rejected with `400` — `"The value for 'gender' is invalid."`.
- The client never chooses the photo here — see §9a.
- **Day 11:** `address`, `permanentAddress`, `gender`, `bloodGroup` and the `emergencyContact*` fields are no longer part of the employee JSON. They are managed through the profile endpoints in §9e (personal details, addresses, emergency contacts). If an older client still sends them, they are ignored (not stored).
- `basicSalary` is optional and must be between `0` and `9999999999999999.99`. On `PUT`, omitting it **keeps** the current salary, so a client that doesn't know about it can't wipe it.
- `dateOfBirth` must be in the past and `joiningDate` cannot be earlier than `dateOfBirth` (`400`).
- `GET /api/employees` returns all employees regardless of status; use the `status` field to tell them apart (there is no filter parameter yet).

`POST /api/employees` stays pure JSON — creating an employee never requires a photo. The photo is managed through the two dedicated sub-resource endpoints above instead of a `photoUrl` field on the create/update DTOs, so: (1) the common "create employee, upload photo later" flow needs no placeholder value, (2) `PUT /api/employees/{id}` (a JSON full-update) can never accidentally clear an existing photo by omission, and (3) Swagger can present a real file picker for the two endpoints that need one, instead of a JSON string field that would have to carry raw file bytes.

#### Create — example request
```bash
curl -X POST http://localhost:5099/api/employees \
  -H "Content-Type: application/json" \
  -d '{
    "employeeCode": "EMP-001",
    "firstName": "Jane",
    "lastName": "Doe",
    "email": "jane.doe@example.com",
    "phone": "123456",
    "dateOfBirth": "1990-01-01",
    "joiningDate": "2024-01-01",
    "departmentId": "<department-guid>",
    "designationId": "<designation-guid>",
    "employmentType": "FullTime"
  }'
```

#### Create — example response (`201 Created`)
```json
{
  "success": true,
  "message": "Employee created successfully.",
  "data": {
    "id": "c062424b-01c8-45af-b48a-d9d0311ea5bd",
    "employeeCode": "EMP-001",
    "firstName": "Jane",
    "lastName": "Doe",
    "fullName": "Jane Doe",
    "email": "jane.doe@example.com",
    "phone": "123456",
    "dateOfBirth": "1990-01-01T00:00:00",
    "joiningDate": "2024-01-01T00:00:00",
    "departmentId": "...",
    "departmentName": "Information Technology",
    "designationId": "...",
    "designationName": "Software Engineer",
    "employmentType": "FullTime",
    "basicSalary": null,
    "status": "Active",
    "isActive": true,
    "photoUrl": null,
    "createdAt": "2026-09-21T23:24:09.4852806Z",
    "updatedAt": null
  },
  "errors": null
}
```

### 9a. Employee profile photo

**Storage architecture.** `Application` depends only on `IFileStorageService` (`SaveAsync`/`DeleteAsync` over a plain `Stream`) — it never references `IFormFile`, `wwwroot`, or any physical path, so `Employee`/`EmployeeService` stay framework-agnostic. `Infrastructure/Storage/LocalFileStorageService` is the only implementation today: it writes under the API's `wwwroot` (passed in from `Program.cs`, not read from config inside Infrastructure) and returns a relative URL like `/uploads/employees/{id:N}.jpg`. Swapping in S3/Blob/Cloudinary later means adding a new `IFileStorageService` implementation and changing one DI registration — no change to `Employee`, `EmployeeService`, or any DTO.

`app.UseStaticFiles()` serves `wwwroot` as the site root, and `wwwroot` contains nothing but `uploads/` — so uploaded photos are reachable at a public URL without exposing source code, `appsettings.json`, or any other server file.

**Safe filenames.** The client's original filename is never trusted or persisted — only its extension is read (after validation), and the stored file is always named `{employeeId:N}_{random}{extension}` (a new random part on every upload). This makes path traversal and filename collisions impossible; since the previous file is deleted after a successful replacement, each employee still has at most one photo file at a time.

**Validation** (`EmployeePhotoPolicy`, enforced server-side in `EmployeeService.UploadPhotoAsync`, all failures return `400`):
- File must be non-empty and ≤ 5 MB.
- Extension must be one of `.jpg`, `.jpeg`, `.png`, `.webp`.
- `Content-Type` header must be one of `image/jpeg`, `image/png`, `image/webp`.
- The file's first bytes must match the real signature for that image format (JPEG/PNG/WEBP magic numbers) — catches a renamed/mislabeled non-image file even if the extension and `Content-Type` both lied.

**Replace-safely sequencing.** On upload: the new file is saved first, then `Employees.PhotoUrl` is updated, and only after that succeeds is the previous file deleted — so a failed upload can never leave an employee with no photo, and a crash mid-request never orphans two files pointing at the same employee for long. `DELETE .../photo` is idempotent: calling it when there's no photo returns `200` and does nothing.

Because every upload gets a new file name, every photo version has its own URL, so browsers never show a cached old photo after a replacement (before Day 10 a same-format replacement reused the URL).

Photos can also be uploaded or removed for inactive employees.

**Swagger.** `Microsoft.AspNetCore.OpenApi`'s document generator does not natively describe an MVC `IFormFile` parameter as a file upload (it reflects over `IFormFile`'s own properties and emits `application/x-www-form-urlencoded`, which breaks Swagger UI's "Try it out" file picker). `smartHRMS.Api/OpenApi/FormFileOperationTransformer.cs` (registered in `AddOpenApi(...)` in `Program.cs`) rewrites those operations to the correct `multipart/form-data` schema, so `PUT /api/employees/{id}/photo` renders a real file picker in `/swagger`.

#### Upload/replace — example
```bash
curl -X PUT http://localhost:5099/api/employees/<id>/photo \
  -F "photo=@profile.jpg;type=image/jpeg"
```
```json
{
  "success": true,
  "message": "Employee photo uploaded successfully.",
  "data": { "...": "...", "photoUrl": "/uploads/employees/<id-no-dashes>.jpg", "updatedAt": "..." },
  "errors": null
}
```
The photo itself is then reachable at `GET http://localhost:5099/uploads/employees/<id-no-dashes>.jpg`.

#### Remove
```bash
curl -X DELETE http://localhost:5099/api/employees/<id>/photo
```
Returns the employee with `photoUrl: null`.

### 9b. Departments & Designations

Both resources have the same routes, rules and response shape (shown for departments; replace with `designations`).

| Method | Route | Description |
|--------|-------|-------------|
| GET    | `/api/departments`      | List all departments (active and inactive), ordered by name, each with `employeeCount` |
| GET    | `/api/departments/{id}` | Get one department (404 if not found) |
| POST   | `/api/departments`      | Create — `201` + `Location` header. New departments are always active |
| PUT    | `/api/departments/{id}` | Update `name`, `description`, `isActive` (all three are sent; `isActive` is required so omitting it can't deactivate by accident). Setting `isActive: true` reactivates |
| DELETE | `/api/departments/{id}` | **Soft delete** — sets `IsActive=false`, never removes the row. Idempotent |

**Rules**
- `name`: required, not blank, max 100, trimmed, unique case-insensitively (`409` on duplicate). `description`: optional, max 500; blank becomes `null`.
- **Delete safety:** DELETE (and PUT with `isActive: false`) returns `409` while any *current* employee — `Active` or `OnLeave` — is assigned. Employees who are `Inactive`/`Resigned`/`Terminated` don't block it; they keep the reference for history. Rows are never physically deleted, and the database also refuses one (`Restrict` FKs), so employee records can never be orphaned or cascade-deleted.
- **Employee assignment:** on employee create/update, `departmentId`/`designationId` must exist (`400`) and must be active (`400`). The active check applies only to a *new* assignment — an employee already in a since-deactivated department can still have other details edited.
- `employeeCount` counts all assigned employees regardless of status.

```json
{ "success": true, "message": "Department created successfully.",
  "data": { "id": "…", "name": "Research", "description": "R&D team", "isActive": true,
            "employeeCount": 0, "createdAt": "2026-09-27T07:49:54.69Z", "updatedAt": null },
  "errors": null }
```

### 9c. Employee profile

`GET /api/employees/{id}/profile` returns everything the profile page needs in one call (`404` for an unknown employee). It loads the employee and all profile records with split queries (one per collection), read-only.

```json
{ "success": true, "message": "Employee profile retrieved successfully.",
  "data": {
    "employeeId": "…", "employeeCode": "EMP-001", "fullName": "Jane Doe", "firstName": "Jane", "lastName": "Doe",
    "photoUrl": "/uploads/employees/<id>_<random>.jpg", "isActive": true,
    "personalInformation": {
      "dateOfBirth": "1995-05-05T00:00:00", "phone": "+8801700000011", "email": "jane@example.com",
      "hasPersonalDetails": true, "gender": "Female", "maritalStatus": "Married", "bloodGroup": "OPositive",
      "nationality": "Bangladeshi", "nationalId": "*********0123", "passportNo": "******567" },
    "jobInformation": {
      "departmentId": "…", "departmentName": "Information Technology",
      "designationId": "…", "designationName": "Software Engineer",
      "joiningDate": "2026-02-01T00:00:00", "employmentType": "FullTime", "employmentStatus": "Active", "basicSalary": 50000 },
    "addresses": [ { "addressType": "Present", "address": "House 12", "city": "Dhaka", "district": "Dhaka", "postalCode": "1207", "...": "..." },
                   { "addressType": "Permanent", "address": "Village Rd", "city": "Cumilla", "district": "Cumilla", "...": "..." } ],
    "emergencyContacts": [ { "name": "John Doe", "relationship": "Spouse", "phone": "+8801800000011", "email": null, "...": "..." } ],
    "educations": [ { "degree": "BSc", "institution": "…", "major": "CSE", "result": "CGPA 3.80", "passingYear": 2017, "...": "..." } ],
    "experiences": [ { "companyName": "…", "designation": "Developer", "startDate": "…", "endDate": null, "isCurrent": true, "...": "..." } ],
    "documents": [ { "id": "…", "documentType": "Nid", "fileName": "nid.pdf", "uploadedAt": "…", "isActive": true,
                     "downloadUrl": "/api/employees/<id>/documents/<docId>/download", "...": "..." } ],
    "createdAt": "…", "updatedAt": "…" },
  "errors": null }
```

- Every section is always present; with no records it is an empty list (`[]`), and `hasPersonalDetails` is `false` with the personal-detail fields `null` — so a UI can show empty states without extra calls.
- **Sensitive values are masked here:** `nationalId` and `passportNo` show only their last 4 / 3 characters. The full values are returned only by `GET /api/employees/{id}/personal-details` (the edit source).
- Order: addresses Present → Permanent; education by passing year (newest first); experience by start date (newest first); documents: **active** only, newest first.

### 9d. Employee documents

All routes are nested under the employee. A document is looked up by **both** the employee id and the document id, so employee A's document requested through employee B's route is `404` — ids can't be swapped to reach someone else's file.

| Method | Route | Description |
|--------|-------|-------------|
| GET    | `/api/employees/{employeeId}/documents` | List documents, newest first. `?includeInactive=true` also returns deactivated ones |
| GET    | `/api/employees/{employeeId}/documents/{documentId}` | Metadata of one document (active or deactivated) |
| POST   | `/api/employees/{employeeId}/documents` | Upload (`multipart/form-data`: `file`, `documentType`, optional `description`) — `201` + `Location` |
| GET    | `/api/employees/{employeeId}/documents/{documentId}/download` | Streams the file as an attachment with the server-validated `Content-Type` and `X-Content-Type-Options: nosniff`. Deactivated documents → `404` |
| PUT    | `/api/employees/{employeeId}/documents/{documentId}` | JSON `{ "documentType": "...", "description": "..." }` — `documentType` optional (omitted = kept), `description` full update (omitted = cleared). Active documents only |
| DELETE | `/api/employees/{employeeId}/documents/{documentId}` | **Soft delete** (`IsActive=false`). The row and the file are kept for HR history. Idempotent |

**Document types:** `Nid`, `Passport`, `EducationalCertificate`, `Cv`, `JoiningLetter`, `ContractPaper`, `Other`, `ExperienceCertificate` (added Day 11) (case-insensitive on upload, so `NID` works; numbers are rejected). An invalid type returns `400` listing the allowed values.

**Upload validation** (`EmployeeDocumentService` + `EmployeeDocumentPolicy`, all failures `400`, nothing is stored):
- The employee must exist (`404`).
- File present and non-empty; size ≤ `MaxFileSizeBytes`.
- Extension in `AllowedExtensions` (configured, see below).
- The file's first bytes must match the real format for that extension (`%PDF-`, JPEG, PNG, OLE for `.doc`, ZIP for `.docx`) — a renamed executable is rejected.
- The stored name is `{random}{extension}` inside a folder named after the employee id; the client file name is only sanitized (directories, invalid characters removed) and kept as the download name.
- If saving the metadata fails after the file was written, the file is deleted again.

**Private storage.** Documents are written under `App_Data/employee-documents/` in the API's content root — **outside `wwwroot`**, so no static URL reaches them; the only way to read one is the download endpoint. The API refuses to start if `StoragePath` points inside `wwwroot`. `FilePath` (the storage key) is never included in responses.

**Configuration** (`appsettings.json`):

```json
"EmployeeDocuments": {
  "StoragePath": "App_Data/employee-documents",
  "MaxFileSizeBytes": 10485760,
  "AllowedExtensions": [ ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" ]
}
```

`AllowedExtensions` can only choose from the formats the server knows how to verify (`.pdf`, `.doc`, `.docx`, `.jpg`, `.jpeg`, `.png`). Listing anything else (for example `.exe`) makes the API **fail at startup** with a clear message, so executables can never be allowed by a configuration mistake. Kestrel's default request limit (~28.6 MB) caps uploads at transport level, so keep `MaxFileSizeBytes` below that.

### 9e. Employee profile records (Day 11)

All routes are nested under the employee (`/api/employees/{employeeId}/...`). Every request first checks that the employee exists (`404`), and a record is only ever found through the employee it belongs to — using another employee's id in the route returns `404`, so ids can't be swapped to read or change someone else's data. `PUT` is a full update (omitted optional fields are cleared).

| Resource | Routes | Rules |
|---|---|---|
| Personal details (0..1) | `GET` / `POST` / `PUT` / `DELETE` `/personal-details` | `POST` when one already exists → `409` (use `PUT`); `GET`/`PUT`/`DELETE` when none → `404`. `gender`, `maritalStatus`, `bloodGroup` are enum names. `nationalId`: 10, 13 or 17 digits (Bangladesh NID). `passportNo`: 6–20 letters/digits, stored upper-case. Both optional but **unique across employees** (`409`). `dateOfBirth` is returned read-only from the employee record |
| Addresses | `GET` `/addresses`, `GET` `/addresses/{id}`, `POST`, `PUT /{id}`, `DELETE /{id}` | `addressType` (`Present`/`Permanent`), `address`, `city`, `district` required; `postalCode` optional (3–20 letters, digits, spaces or dashes). At most one address of each type → a second one (or changing a Permanent into a second Present) is `409` |
| Emergency contacts | same pattern at `/emergency-contacts` | `name`, `relationship`, `phone` (valid phone) required; `email` optional (blank = none, must be valid), `address` optional. Any number per employee, but the same phone twice for one employee → `409` |
| Education | same pattern at `/educations` | `degree` and `institution` required (free text, e.g. SSC/HSC/Bachelor/Master/Diploma), `major`/`result` optional, `passingYear` required: from the employee's birth year up to 5 years ahead (`400`). Exact duplicate (same degree + institution + year) → `409`. Listed newest first |
| Experience | same pattern at `/experiences` | `companyName`, `designation`, `startDate` required. `startDate` after the date of birth and not in the future; `endDate` optional (`null` = current job, `isCurrent: true`), not before `startDate` and not in the future (`400`). Listed newest first |
| Documents | existing Day 10 endpoints (§9d) | Day 11 added the `ExperienceCertificate` document type |

Example — add a present address:

```bash
curl -X POST http://localhost:5099/api/employees/<id>/addresses \
  -H "Content-Type: application/json" \
  -d '{ "addressType": "Present", "address": "House 12, Road 2", "city": "Dhaka", "district": "Dhaka", "postalCode": "1207" }'
```

### Error responses
All errors use the standard envelope (`success: false`, `data: null`). Domain exceptions are mapped centrally by `AppExceptionHandler`; model-validation failures by `AddApiControllers()`; body-less 404/405/415 by `StatusCodeResponseWriter`. `message` is a short summary and `errors` carries the details.

| Scenario                              | Status | `message`               | `errors` |
|----------------------------------------|--------|--------------------------|----------|
| Employee not found (also a malformed id such as `/api/employees/abc`, which doesn't match the `{id:guid}` route) | 404 | `Resource not found.` | the not-found detail (`[]` for a malformed id) |
| Duplicate `EmployeeCode` or `Email`    | 409    | `Conflict.`              | the conflict detail |
| Duplicate lost in a simultaneous-request race (caught by the unique index) | 409 | `Conflict.` | `A record with the same unique value already exists.` |
| Reactivating an employee (`status` → `Active`/`OnLeave`) whose department/designation is inactive | 400 | `Bad request.` | the detail |
| Duplicate Department/Designation name | 409 | `Conflict.` | the conflict detail |
| Deactivating a Department/Designation with active or on-leave employees | 409 | `Conflict.` | how many employees block it |
| Non-existent `DepartmentId`/`DesignationId` | 400 | `Bad request.`        | the detail |
| Assigning an inactive Department/Designation to an employee | 400 | `Bad request.` | the detail |
| Department/Designation not found | 404 | `Resource not found.` | the not-found detail |
| DTO validation failure (`[Required]`, `[EmailAddress]`, `[Phone]`, `[NotDefault]`, ...) | 400 | `One or more validation errors occurred.` | one entry per failed rule |
| Invalid photo upload (missing/empty file, disallowed extension/type, signature mismatch, > 5 MB) | 400 | `Bad request.` | the specific reason |
| Photo request body over the 6 MB `[RequestSizeLimit]` | 400 | `One or more validation errors occurred.` | the framework's "Request body too large" message |
| Invalid document upload (missing/empty file, disallowed extension, content not matching the extension, too large, invalid `documentType`) | 400 | `Bad request.` | the specific reason (lists allowed types/values where relevant) |
| Document not found, belongs to another employee, or deactivated (download/update) | 404 | `Resource not found.` | the detail |
| Date of birth not in the past / joining date before date of birth | 400 | `Bad request.` | the detail |
| Profile record rule broken (second Present/Permanent address, duplicate contact phone, duplicate education, NID/passport used by another employee, personal details already exist) | 409 | `Conflict.` | the detail |
| Profile record date/year rule broken (passing year outside birth year…+5, experience start/end dates) | 400 | `Bad request.` | the detail |
| Profile record not found, or belongs to another employee | 404 | `Resource not found.` | the detail |
| Malformed JSON body | 400 | `One or more validation errors occurred.` | `The request body is not valid JSON.` |
| Unreadable JSON value (unknown/numeric enum, wrong type) | 400 | `One or more validation errors occurred.` | `The value for '<field>' is invalid.` (internal .NET type names are never returned) |
| Unknown route / wrong method / wrong content type | 404 / 405 / 415 | `Resource not found.` / `Method not allowed.` / `Unsupported media type.` | `[]` |
| Unhandled exception                    | 500    | `An unexpected error occurred.` | `[]` (exception details are never sent to the client, only logged) |
| Client disconnects mid-request         | 499    | (no body; logged at Debug, not as an error) | |

Example (`404`):
```json
{ "success": false, "message": "Resource not found.", "data": null,
  "errors": ["Employee with id '...' was not found."] }
```

---

## 10. Running the Backend

```bash
cd backend
dotnet restore
dotnet build smartHRMS.slnx
dotnet ef database update --project SmartHRMS.Infrastructure --startup-project smartHRMS.Api
cd smartHRMS.Api
dotnet run
```

Swagger UI: `http://localhost:5099/swagger` (Development only).

> **Stop the running API before building.** While `dotnet run` is active, its process locks the DLLs in `smartHRMS.Api/bin`, so a second `dotnet build` fails with `MSB3027`/`MSB3021` ("The file is locked by: smartHRMS.Api"). This is not a code error — stop the API (Ctrl+C), then build. Likewise, a running instance keeps serving the code it was started with; restart it after pulling or changing code.

### Running tests
```bash
cd backend
dotnet test smartHRMS.slnx
```

---

## 11. Current Backend State (Day 1–11 complete)

- Solution builds cleanly (`dotnet build` → 0 warnings, 0 errors, with no API instance running — see §10).
- Database schema created and migrated on the local SQL Server instance; all 6 migrations applied, no pending model changes.
- Domain layer complete: `BaseEntity`, `Department`, `Designation`, `Employee`, `ApplicationUser`, `EmployeeStatus`.
- Application/Infrastructure/API layers wired end-to-end for Employees, Departments and Designations. The Day 1–8 audit verified every endpoint live against the database (valid and invalid cases, photo upload/replace/remove, delete protection, the end-to-end Department → Designation → Employee workflow), with no regressions to earlier days. Day 9 was verified the same way (profile fields, employment type, status changes and reactivation, enum validation) plus a Day 1–8 regression pass. Day 10 was verified live too (all personal/job fields, profile endpoint, document upload/list/download/update/soft delete, cross-employee access, invalid/oversized/renamed files, private storage not publicly reachable, startup config validation) with a Day 1–9 regression pass.
- Centralized exception handling in place; no stack traces or secrets leak to API responses.
- Standard `ApiResponse<T>` envelope on every endpoint, including errors, validation failures and body-less 404/405/415.
- Swagger UI available at `/swagger` (Development); success and error responses are documented with `[ProducesResponseType]` on the Employees/Departments/Designations endpoints (not on `/api/health`), including a working file picker for the photo upload endpoint.
- Employee profile photo support: `PhotoUrl` on `Employee`, local disk storage behind `IFileStorageService`, upload/replace/remove endpoints, server-side validation (type/size/signature), and static-file serving scoped to `wwwroot/uploads` only.
- `.gitignore` added; `.vs/`, `bin/`, `obj/` no longer tracked in git; uploaded photos under `wwwroot/uploads/` are gitignored too (folder kept via `.gitkeep`).
- Department & Designation management (Day 8): full CRUD with soft-delete deactivation, duplicate-name protection, delete safety, and active-status checks when assigning employees (see §9b).
- Employee profile & document management (Day 10): see §9c/§9d.
- Complete employee profile (Day 11): personal details, addresses, emergency contacts, education, experience — see §6.8 and §9e. Verified live with 167 checks plus a 192-check Day 1–10 regression run.
- 150 unit tests passing (Employee, Profile, Profile records, Document, Department, Designation services, DTO validation, `ApiResponse`, photo upload/removal) using in-memory fake repositories.

### Known issues (from the Day 1–8 audit)

| Severity | Issue | Impact |
|----------|-------|--------|
| High | No authentication/authorization | Every endpoint is callable anonymously, including salary data, profile, and **employee documents (NID, passport, ...)**. Document ownership is enforced (a document is only reachable through its own employee), but there is no user/role check yet. Must be addressed before any shared or deployed use |
| Low | Photo over 6 MB returns the framework's message | `400` under "One or more validation errors occurred." rather than a `413` or the photo-specific message |
| Low | Department and Designation code is duplicated | The two features are parallel copies (service, repository, controller), including the deactivation rule |
| Low | Redundant EF Core package references in `smartHRMS.Api.csproj` | `Microsoft.EntityFrameworkCore.SqlServer`/`.Tools` already come through Infrastructure (`.Design` is still needed for `dotnet ef`) |
| Low | Tests are unit-level only | EF queries and the HTTP pipeline have no automated integration tests |
| Low | Photo upload limits are constants | `EmployeePhotoPolicy` (5 MB, jpg/png/webp) is not configurable like the document rules |
| Low | `BasicSalary` is returned by the list endpoint | Salary is sensitive; once auth exists, restrict it (e.g. HR role only) |
| Low | No frontend | Day 10 profile/form UI (phases 11–12) is not implemented because the repository has no frontend yet |
| Low | Removed employee JSON fields are ignored silently | Since Day 11, `address`, `permanentAddress`, `gender`, `bloodGroup` and `emergencyContact*` sent to `POST/PUT /api/employees` are ignored (not stored, no error). Use the profile endpoints (§9e) |

**Resolved in Day 9:** employee reactivation (via `status` on `PUT`), duplicate race → `500` (now `409`, see §5), `.http` file now covers Departments/Designations, JSON error messages no longer expose .NET type names.

**Resolved in Day 10:** same-format photo replacement now gets a new URL (no stale browser cache).

**Resolved in the Day 1–10 audit:** solution/project reference paths now match the folder casing (previously 3 build errors on a case-sensitive file system, now 0); `smartHRMS.Api.csproj.user` is no longer tracked in git (it stays local and is covered by `*.user` in `.gitignore`).

### Not yet implemented
- Authentication/authorization (JWT, roles/permissions) — only `ApplicationUser` (with `PasswordHash`) exists; there is no `Role`/`Permission` entity and no auth DTOs yet
- Attendance, Leave, Payroll, Recruitment, Performance modules
- Frontend/dashboard UI
- Cloud object storage (S3/Blob/Cloudinary) for photos — local disk only today, but `IFileStorageService` was designed so this is a new Infrastructure implementation, not a redesign

---

## 12. Recommended Next Steps (Day 12+)

1. Commit the Day 10–11 work and the Day 1–10 audit fixes before starting new features.
2. **Authentication (highest priority now that the API holds identity documents and salaries):** JWT issuance tied to `ApplicationUser`, password hashing (e.g. `Microsoft.AspNetCore.Identity` password hasher).
3. Role-based authorization: e.g. HR can manage all employees/documents/salaries; an employee can read only their own profile and documents.
4. Employee list filtering/paging (by status, department, designation) as the data grows.
5. CORS configuration once a frontend is introduced. Then the frontend profile page and sectioned create/edit form (Day 10 phases 11–12).
6. Integration tests against a real/in-memory database for the API layer (current tests are unit-level against fake repositories).

---

## 13. Security Considerations

Current state:
- **There is no authentication or authorization yet — all endpoints are anonymous.** `app.UseAuthorization()` is in the pipeline but no scheme or policy is configured, so it has no effect. Do not expose this API outside a local development machine until auth is implemented.
- Connection string uses Windows/Trusted authentication — no stored credentials.
- `ApplicationUser.PasswordHash` — no plain-text passwords anywhere in the model.
- Centralized exception handling prevents stack traces/internal details from reaching API responses.
- No secrets or connection strings with credentials are committed to git.
- Photo uploads: extension + `Content-Type` + magic-byte signature are all checked server-side (never trusting the client alone), size is capped at 5 MB (with a 6 MB `[RequestSizeLimit]` on the action as a transport-level backstop), stored filenames are always server-generated from the employee id (the original filename is only ever read for its extension, never used for storage or trusted for path construction), and `wwwroot` — the only folder served as static content — contains nothing but the `uploads/` directory, so no source, config, or other server file is reachable through it.
- Employee documents: stored outside `wwwroot` (no static URL), only streamed through the download endpoint with the server-validated `Content-Type`, `Content-Disposition: attachment` and `X-Content-Type-Options: nosniff`; allowed types are a configurable subset of formats the server can verify by signature (executables can never be enabled); server-generated storage names; every resolved path is checked to stay inside its storage root (`SafeStoragePath`); a document is only reachable through its own employee id; uploaded files are never executed.

Recommended for future days:
- JWT authentication + refresh tokens
- Role-based authorization policies
- Rate limiting
- CORS policy scoped to the frontend origin once it exists
- Antivirus/malware scanning of uploaded photos and documents before they are served, if this ever runs somewhere untrusted users can reach

---

## 14. Useful Commands

```bash
dotnet restore
dotnet build smartHRMS.slnx
dotnet test smartHRMS.slnx
dotnet ef migrations add <Name> --project SmartHRMS.Infrastructure --startup-project smartHRMS.Api
dotnet ef database update --project SmartHRMS.Infrastructure --startup-project smartHRMS.Api
dotnet ef migrations remove --project SmartHRMS.Infrastructure --startup-project smartHRMS.Api
```

---

## 15. Developer Notes

- Keep `Application` free of EF Core / infrastructure dependencies — it should only reference `Domain` plus repository *interfaces*. Business logic and validation belong in `Application` services; data access belongs in `Infrastructure` repositories.
- Employees, Departments and Designations are soft-deleted only (`Status=Inactive` / `IsActive=false`). HR records are never physically removed through the API, and the `Restrict` foreign keys block it at the database level too.
- When adding a new entity feature, follow the `Employees`/`Departments` folder pattern: `Interfaces/I<Entity>Repository`, `Features/<Entity>/I<Entity>Service` + `<Entity>Service`, `Features/<Entity>/Dtos/*`, then an `Infrastructure/Repositories/<Entity>Repository`, and finally the controller in the API project.
- Always generate EF Core migrations after a Domain entity change — don't hand-edit an already-applied migration; add a new one.
- Run `dotnet build` and `dotnet test` from `backend/` (solution level) before committing, since the solution file (`smartHRMS.slnx`) is the source of truth for what's included in a build.
