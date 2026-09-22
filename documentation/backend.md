# Smart HRMS Backend Documentation

## 1. Overview

This project is the backend API for a Human Resource Management System, built with ASP.NET Core and Entity Framework Core using Clean Architecture. As of Day 1–6, the foundation is complete and stable: project structure, database schema, domain layer, and a fully working Employee CRUD workflow through the Application/API layers.

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
- xUnit test project with unit tests for the Employee service, DTO validation, `ApiResponse` and photo upload/removal

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
    Controllers/
      HealthController.cs
      EmployeesController.cs
    Extensions/
      ApiServiceCollectionExtensions.cs (AddApiControllers: validation-error envelope)
    Middleware/
      AppExceptionHandler.cs
      StatusCodeResponseWriter.cs       (envelope for body-less 404/405/415)
    OpenApi/
      FormFileOperationTransformer.cs   (fixes IFormFile -> multipart/form-data in Swagger)
    Properties/
      launchSettings.json
    wwwroot/
      uploads/employees/                (photo storage root; served as static files)

  SmartHRMS.Application/                (class library, no EF Core dependency)
    DependencyInjection.cs              (AddApplication)
    Common/
      Exceptions/
        NotFoundException.cs
        ConflictException.cs
        BadRequestException.cs
      Models/
        ApiResponse.cs                  (ApiResponse<T> + ApiResponse shortcuts)
      Validation/
        NotDefaultAttribute.cs          ([NotDefault] for Guid / DateTime)
    Interfaces/
      IEmployeeRepository.cs
      IDepartmentRepository.cs
      IDesignationRepository.cs
      IFileStorageService.cs            (storage-agnostic file save/delete abstraction)
    Features/
      Employees/
        IEmployeeService.cs
        EmployeeService.cs
        EmployeePhotoPolicy.cs          (allowed types/size + magic-byte signature check)
        Dtos/
          EmployeeDto.cs
          CreateEmployeeDto.cs
          UpdateEmployeeDto.cs
          UploadEmployeePhotoDto.cs     (Stream + metadata; no IFormFile in Application)

  SmartHRMS.Domain/                      (class library, no dependencies)
    Common/
      BaseEntity.cs
    Entities/
      Department.cs
      Designation.cs
      Employee.cs
      ApplicationUser.cs
    Enums/
      EmployeeStatus.cs

  SmartHRMS.Infrastructure/              (class library)
    DependencyInjection.cs              (AddInfrastructure)
    Persistence/
      SmartHRMSDbContext.cs
      Configurations/
        DepartmentConfiguration.cs
        DesignationConfiguration.cs
        EmployeeConfiguration.cs
        ApplicationUserConfiguration.cs
    Repositories/
      EmployeeRepository.cs
      DepartmentRepository.cs
      DesignationRepository.cs
    Storage/
      LocalFileStorageService.cs        (IFileStorageService impl: saves under wwwroot)
    Migrations/

  smartHRMS.Tests/                       (xUnit test project)
    Fakes/
      FakeEmployeeRepository.cs
      FakeExistsRepository.cs
      FakeFileStorageService.cs
    Common/
      ApiResponseTests.cs
    Features/
      Employees/
        EmployeeServiceTests.cs
        EmployeeDtoValidationTests.cs
        EmployeePhotoTests.cs
```

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

### Configuration
Entity configurations live under `Persistence/Configurations/` as `IEntityTypeConfiguration<T>` classes, applied via:

```csharp
modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmartHRMSDbContext).Assembly);
```

`Employee.DepartmentId`, `Employee.DesignationId`, and `ApplicationUser.EmployeeId` all use `DeleteBehavior.Restrict` to avoid multiple cascade-path conflicts in SQL Server.

---

## 6. Core Domain Models

### 6.1 BaseEntity
- `Id` (`Guid`, generated on creation)
- `CreatedAt` (`DateTime`, UTC, set on creation)
- `UpdatedAt` (`DateTime?`, set on update)

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
- `DateOfBirth`, `JoiningDate`
- `DepartmentId` / `Department` (required FK, `Restrict`)
- `DesignationId` / `Designation` (required FK, `Restrict`)
- `Status` (`EmployeeStatus` enum, stored as string, default `Active`)
- `PhotoUrl` (optional, max 500) — relative URL of the profile photo, e.g. `/uploads/employees/{id}.jpg`; `null` until a photo is uploaded. Stores a reference only, never image bytes and never a physical path, so the storage backend can change without touching this entity (see §9a).
- `ApplicationUser` (optional 1:1 navigation)

> `Status` is the single source of truth for whether an employee is active — there is intentionally no separate `IsActive` flag on `Employee` (unlike `Department`/`Designation`), avoiding duplicate/contradictory state.

### 6.5 EmployeeStatus (enum)
`Active = 1`, `Inactive = 2`, `Resigned = 3`, `Terminated = 4`, `OnLeave = 5`

### 6.6 ApplicationUser
- `EmployeeId` (required FK, unique — one user per employee)
- `Username` (required, max 100, unique)
- `PasswordHash` (required — never a plain-text password)
- `IsActive` (default `true`)
- `Employee` (navigation)

---

## 7. Entity Relationships

- `Department` 1 — * `Employee`
- `Designation` 1 — * `Employee`
- `Employee` 1 — 0..1 `ApplicationUser`

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
| POST   | `/api/employees`      | Create an employee                             |
| PUT    | `/api/employees/{id}` | Update an employee                             |
| DELETE | `/api/employees/{id}` | Deactivate (soft delete) — sets `Status=Inactive`, no row is removed. Returns `200` with the envelope (no `data`) |
| PUT    | `/api/employees/{id}/photo` | Upload or replace the employee's profile photo (`multipart/form-data`, field name `photo`) — see §9a |
| DELETE | `/api/employees/{id}/photo` | Remove the employee's profile photo, if any. Does **not** delete the employee — see §9a |

Required on create/update: `firstName`, `lastName`, `email` (valid), `dateOfBirth`, `joiningDate`, `departmentId`, `designationId` (an omitted/empty Guid or default date is rejected by `[NotDefault]`). `phone` is optional but must be a valid phone number when present. `employeeCode` is required on create only.

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
    "designationId": "<designation-guid>"
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
    "email": "jane.doe@example.com",
    "phone": "123456",
    "dateOfBirth": "1990-01-01T00:00:00",
    "joiningDate": "2024-01-01T00:00:00",
    "departmentId": "...",
    "departmentName": "Engineering",
    "designationId": "...",
    "designationName": "Software Engineer",
    "status": "Active",
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

**Safe filenames.** The client's original filename is never trusted or persisted — only its extension is read (after validation), and the stored file is always named `{employeeId:N}{extension}`. This makes path traversal and filename collisions impossible and means each employee has at most one photo file at a time.

**Validation** (`EmployeePhotoPolicy`, enforced server-side in `EmployeeService.UploadPhotoAsync`, all failures return `400`):
- File must be non-empty and ≤ 5 MB.
- Extension must be one of `.jpg`, `.jpeg`, `.png`, `.webp`.
- `Content-Type` header must be one of `image/jpeg`, `image/png`, `image/webp`.
- The file's first bytes must match the real signature for that image format (JPEG/PNG/WEBP magic numbers) — catches a renamed/mislabeled non-image file even if the extension and `Content-Type` both lied.

**Replace-safely sequencing.** On upload: the new file is saved first, then `Employees.PhotoUrl` is updated, and only after that succeeds is the previous file deleted — so a failed upload can never leave an employee with no photo, and a crash mid-request never orphans two files pointing at the same employee for long. `DELETE .../photo` is idempotent: calling it when there's no photo returns `200` and does nothing.

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

### Error responses
All errors use the standard envelope (`success: false`, `data: null`). Domain exceptions are mapped centrally by `AppExceptionHandler`; model-validation failures by `AddApiControllers()`; body-less 404/405/415 by `StatusCodeResponseWriter`. `message` is a short summary and `errors` carries the details.

| Scenario                              | Status | `message`               | `errors` |
|----------------------------------------|--------|--------------------------|----------|
| Employee not found                     | 404    | `Resource not found.`    | the not-found detail |
| Duplicate `EmployeeCode` or `Email`    | 409    | `Conflict.`              | the conflict detail |
| Non-existent `DepartmentId`/`DesignationId` | 400 | `Bad request.`        | the detail |
| DTO validation failure (`[Required]`, `[EmailAddress]`, `[Phone]`, `[NotDefault]`, ...) | 400 | `One or more validation errors occurred.` | one entry per failed rule |
| Invalid photo upload (missing/empty file, disallowed extension/type, signature mismatch, > 5 MB) | 400 | `Bad request.` | the specific reason |
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

### Running tests
```bash
cd backend
dotnet test smartHRMS.slnx
```

---

## 11. Current Backend State (Day 1–6 complete)

- Solution builds cleanly (`dotnet build` → 0 warnings, 0 errors).
- Database schema created and migrated on the local SQL Server instance.
- Domain layer complete: `BaseEntity`, `Department`, `Designation`, `Employee`, `ApplicationUser`, `EmployeeStatus`.
- Application/Infrastructure/API layers wired end-to-end for Employee CRUD, verified live (create, duplicate checks, FK validation, not-found, update, soft-delete deactivate, malformed input).
- Centralized exception handling in place; no stack traces or secrets leak to API responses.
- Standard `ApiResponse<T>` envelope on every endpoint, including errors, validation failures and body-less 404/405/415.
- Swagger UI available at `/swagger` (Development); all success and error responses are documented per endpoint, including a working file picker for the photo upload endpoint.
- Employee profile photo support: `PhotoUrl` on `Employee`, local disk storage behind `IFileStorageService`, upload/replace/remove endpoints, server-side validation (type/size/signature), and static-file serving scoped to `wwwroot/uploads` only.
- `.gitignore` added; `.vs/`, `bin/`, `obj/` no longer tracked in git; uploaded photos under `wwwroot/uploads/` are gitignored too (folder kept via `.gitkeep`).
- 34 unit tests passing (Employee service, DTO validation, `ApiResponse`, photo upload/removal) using in-memory fake repositories.

### Not yet implemented (intentionally out of Day 1–6 scope)
- Department/Designation CRUD endpoints (only existence checks exist so far, used by Employee validation)
- Authentication/authorization (JWT, roles/permissions) — only `ApplicationUser` (with `PasswordHash`) exists; there is no `Role`/`Permission` entity and no auth DTOs yet
- Attendance, Leave, Payroll, Recruitment, Performance modules
- Frontend/dashboard UI
- Cloud object storage (S3/Blob/Cloudinary) for photos — local disk only today, but `IFileStorageService` was designed so this is a new Infrastructure implementation, not a redesign

---

## 12. Recommended Next Steps (Day 7+)

1. `DepartmentsController` / `DesignationsController` for full CRUD (repositories already exist for existence checks — extend them).
2. Authentication: JWT issuance tied to `ApplicationUser`, password hashing (e.g. `Microsoft.AspNetCore.Identity` password hasher).
3. Role-based authorization once an auth foundation exists.
4. CORS configuration once a frontend is introduced.
5. Integration tests against a real/in-memory database for the API layer (current tests are unit-level against fake repositories).

---

## 13. Security Considerations

Current state:
- Connection string uses Windows/Trusted authentication — no stored credentials.
- `ApplicationUser.PasswordHash` — no plain-text passwords anywhere in the model.
- Centralized exception handling prevents stack traces/internal details from reaching API responses.
- No secrets or connection strings with credentials are committed to git.
- Photo uploads: extension + `Content-Type` + magic-byte signature are all checked server-side (never trusting the client alone), size is capped at 5 MB (with a 6 MB `[RequestSizeLimit]` on the action as a transport-level backstop), stored filenames are always server-generated from the employee id (the original filename is only ever read for its extension, never used for storage or trusted for path construction), and `wwwroot` — the only folder served as static content — contains nothing but the `uploads/` directory, so no source, config, or other server file is reachable through it.

Recommended for future days:
- JWT authentication + refresh tokens
- Role-based authorization policies
- Rate limiting
- CORS policy scoped to the frontend origin once it exists
- Antivirus/malware scanning of uploaded photos before they're served, if this ever runs somewhere untrusted users can reach

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
- When adding a new entity feature, follow the `Employees` folder pattern: `Interfaces/I<Entity>Repository`, `Features/<Entity>/I<Entity>Service` + `<Entity>Service`, `Features/<Entity>/Dtos/*`, then an `Infrastructure/Repositories/<Entity>Repository`, and finally the controller in the API project.
- Always generate EF Core migrations after a Domain entity change — don't hand-edit an already-applied migration; add a new one.
- Run `dotnet build` and `dotnet test` from `backend/` (solution level) before committing, since the solution file (`smartHRMS.slnx`) is the source of truth for what's included in a build.
