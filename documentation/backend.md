# SmartHRMS Backend Documentation

> Describes the code, database and API **as they are after the Day 1–12 audit (2026-09-28)**. Everything marked as
> implemented was verified at runtime against the SQL Server database (see §22 and §23). Planned work is listed only in
> §24 and is clearly marked as not implemented.

---

## 1. Project Overview

**SmartHRMS** is a Human Resource Management System. This repository contains its backend: an ASP.NET Core Web API
that stores HR data in SQL Server and serves it as JSON to the React frontend in `frontend/smarthrms-web`.

**Current scope (Day 1–12):**

| Area | What exists |
|---|---|
| Organization | Departments and Designations: CRUD, soft-delete (deactivation), delete protection |
| Employees | CRUD with job information (department, designation, joining date, employment type, salary, status), deactivation and reactivation, profile photo |
| Employee profile | Personal details (gender, marital status, blood group, nationality, NID, passport), addresses, emergency contacts, education, work experience, and a one-call profile view |
| Employee documents | Upload, list, view metadata, download, edit metadata and soft-delete HR documents (NID, passport, certificates, CV, ...) kept in private storage |
| Platform | Standard response envelope, centralized error handling, validation, OpenAPI/Swagger, CORS for the frontend |

**Not implemented:** authentication and authorization (every endpoint is anonymous), attendance, leave, payroll,
recruitment, performance (see §17 and §24).

### Development history

| Day | Scope |
|-----|-------|
| 1 | Solution and project setup (.NET 10 Web API) |
| 2 | Database design: `Department`, `Designation`, `Employee`, `ApplicationUser` |
| 3 | Clean Architecture split: Domain / Application / Infrastructure / API |
| 4–5 | DI per layer, EF Core configurations + migrations, `ApiResponse<T>`, exception handling, validation |
| 6 | Employee workflow end-to-end + profile photo storage |
| 7 | Employee CRUD audit and fixes (input trimming, blank phone, UTC timestamps) |
| 8 | Department & Designation management (CRUD, delete safety, active-status rules) |
| 9 | Employee management completed (employment type, computed `fullName`/`isActive`, status changes and reactivation, enums as strings, unique-index races → 409) |
| 10 | Employee profile endpoint and the `EmployeeDocuments` module (private storage, soft delete) |
| 11 | Complete employee profile: personal details, addresses, emergency contacts, education, experience (flat Day 9–10 columns moved into these tables) |
| 12 | Employee document management: document name, issue/expiry dates, Birth/TIN certificate types, Content-Type cross-check; delete remains a soft delete |
| Audit (Day 1–12) | Cancelled requests no longer logged as server errors; employee list ordered by employee code; documentation rewritten |

---

## 2. Backend Architecture

Clean Architecture with four projects. Requests flow through the layers like this (verified for every feature):

```text
HTTP request
   ↓
Controller (smartHRMS.Api)            thin: binds the request, calls one service method, wraps the result
   ↓
Application service                   business rules and validation, maps entities ↔ DTOs
   ↓
Repository interface (Application)  ← implemented by → Repository (Infrastructure)
   ↓
EF Core SmartHRMSDbContext
   ↓
SQL Server (SmartHRMSDB)
```

File storage follows the same idea: services depend on `IFileStorageService` (photos) and `IDocumentStorageService`
(documents), implemented in Infrastructure by local-disk classes.

**Dependency direction** (no circular references; checked in the `.csproj` files):

```text
smartHRMS.Api            → SmartHRMS.Application, SmartHRMS.Infrastructure
SmartHRMS.Infrastructure → SmartHRMS.Domain, SmartHRMS.Application (implements its interfaces)
SmartHRMS.Application    → SmartHRMS.Domain
SmartHRMS.Domain         → (nothing)
smartHRMS.Tests          → SmartHRMS.Application, SmartHRMS.Domain
```

`Application` has no EF Core or ASP.NET Core dependency: uploads reach it as plain `Stream` + metadata DTOs, never as
`IFormFile`.

---

## 3. Project Structure

```text
backend/
  smartHRMS.slnx
  smartHRMS.Api/                        ASP.NET Core Web API (Microsoft.NET.Sdk.Web)
    Program.cs                          startup and middleware pipeline (§4)
    appsettings.json / appsettings.Development.json
    smartHRMS.Api.http                  sample requests for the IDE HTTP client
    Controllers/                        Health, Employees, Departments, Designations, EmployeeDocuments,
                                        EmployeePersonalDetails, EmployeeAddresses, EmployeeEmergencyContacts,
                                        EmployeeEducations, EmployeeExperiences
    Extensions/ApiServiceCollectionExtensions.cs   controllers, JSON enum handling, validation-error envelope
    Middleware/AppExceptionHandler.cs              exception → status code + envelope
    Middleware/StatusCodeResponseWriter.cs         envelope for body-less 404/405/415
    OpenApi/FormFileOperationTransformer.cs        correct multipart/form-data schema for file uploads
    wwwroot/uploads/employees/          PUBLIC photo storage (served as static files, gitignored)
    App_Data/employee-documents/        PRIVATE document storage (never served, gitignored)

  SmartHRMS.Application/                business logic (class library)
    DependencyInjection.cs              AddApplication(documentOptions)
    Common/Exceptions/                  NotFoundException, ConflictException, BadRequestException
    Common/Models/ApiResponse.cs        response envelope
    Common/Files/FileSignature.cs       magic-byte checks shared by photos and documents
    Common/Text/InputText.cs            trim / blank-to-null helpers
    Common/Validation/NotDefaultAttribute.cs   [NotDefault] for Guid / DateTime
    Interfaces/                         repository and storage interfaces
    Features/Employees/                 EmployeeService, EmployeeProfileService, EmployeePhotoPolicy,
                                        EmployeeStatusRules, EmployeeOwnedRecordService (shared base), Dtos/
    Features/Departments/, Features/Designations/
    Features/EmployeePersonalDetails/, EmployeeAddresses/, EmployeeEmergencyContacts/,
             EmployeeEducations/, EmployeeExperiences/
    Features/EmployeeDocuments/         EmployeeDocumentService, EmployeeDocumentPolicy, EmployeeDocumentOptions, Dtos/

  SmartHRMS.Domain/                     entities and enums (no dependencies)
    Common/BaseEntity.cs, Common/EmployeeOwnedEntity.cs
    Entities/                           Department, Designation, Employee, ApplicationUser, EmployeeDocument,
                                        EmployeePersonalDetails, EmployeeAddress, EmployeeEmergencyContact,
                                        EmployeeEducation, EmployeeExperience
    Enums/                              EmployeeStatus, EmploymentType, Gender, MaritalStatus, BloodGroup,
                                        AddressType, EmployeeDocumentType

  SmartHRMS.Infrastructure/             data access and storage
    DependencyInjection.cs              AddInfrastructure(connectionString, photoRoot, documentRoot)
    Persistence/SmartHRMSDbContext.cs
    Persistence/Configurations/         one IEntityTypeConfiguration<T> per entity
    Repositories/                       Employee, Department, Designation, EmployeeDocument, EmployeeOwned<T>
    Storage/                            LocalFileStorageService, LocalDocumentStorageService, SafeStoragePath
    Migrations/                         7 migrations (§20)

  smartHRMS.Tests/                      xUnit unit tests with in-memory fakes (169 tests)
```

> **Folder-name casing:** the folders `SmartHRMS.Application`, `SmartHRMS.Domain`, `SmartHRMS.Infrastructure` start with
> a capital S while the project files inside are `smartHRMS.*.csproj`. The solution file and every `ProjectReference` use
> exactly these paths, so the solution also builds on case-sensitive file systems. Keep that casing when adding references.

---

## 4. Technologies

Only packages that are actually referenced:

| Purpose | Technology |
|---|---|
| Runtime / language | .NET 10, C# |
| Web API | ASP.NET Core (`Microsoft.NET.Sdk.Web`) |
| API description | `Microsoft.AspNetCore.OpenApi` 10.0.12 (document at `/openapi/v1.json`) + `Swashbuckle.AspNetCore.SwaggerUI` 10.2.3 (UI at `/swagger`), Development only |
| Data access | Entity Framework Core 10.0.12 with the SQL Server provider; `Microsoft.EntityFrameworkCore.Design`/`.Tools` for migrations |
| Database | Microsoft SQL Server (local instance `MAHIM\SQLEXPRESS`, database `SmartHRMSDB`, Windows authentication) |
| Validation | Data Annotations + custom `[NotDefault]`, plus rules in the services (no FluentValidation) |
| Tests | xUnit 2.9.2, `Microsoft.NET.Test.Sdk` |

No authentication packages (JWT, Identity) are installed.

### Startup pipeline (`Program.cs`)

Services:
1. Console logging; controllers with JSON string enums and the validation-error envelope (`AddApiControllers`).
2. OpenAPI with `FormFileOperationTransformer`.
3. CORS policy `Frontend` from `Cors:AllowedOrigins` (§17).
4. `AppExceptionHandler` + problem details.
5. `AddApplication(documentOptions)`: document rules from the `EmployeeDocuments` section are validated here, so the
   API **refuses to start** with an unusable configuration.
6. `AddInfrastructure(connectionString, webRootPath, documentStoragePath)`. The API refuses to start if the document
   storage path is inside `wwwroot`.

Middleware order: `UseCors` (first, so error responses carry CORS headers) → `UseExceptionHandler` →
`UseStatusCodePages` → `UseStaticFiles` (wwwroot) → OpenAPI + Swagger UI (Development) → HTTPS redirection (only when an
HTTPS port is configured) → `UseAuthorization` (no policies exist) → `MapControllers`.

---

## 5. Database

- **Database:** `SmartHRMSDB` on `MAHIM\SQLEXPRESS`, Windows (trusted) authentication; the connection string holds no
  credentials. Read-committed snapshot isolation is enabled on the database.
- **DbContext:** `SmartHRMSDbContext` (`smartHRMS.Infrastructure.Persistence`), registered with `UseSqlServer`.
  Configurations are applied with `ApplyConfigurationsFromAssembly`.
- **Tables:** `Departments`, `Designations`, `Employees`, `ApplicationUsers`, `EmployeeDocuments`,
  `EmployeePersonalDetails`, `EmployeeAddresses`, `EmployeeEmergencyContacts`, `EmployeeEducations`,
  `EmployeeExperiences` (+ `__EFMigrationsHistory`).
- **Primary keys:** every table has a `uniqueidentifier` `Id` generated by the application.

### Foreign keys (all `ON DELETE NO ACTION`)

| Column | References |
|---|---|
| `Employees.DepartmentId` | `Departments.Id` |
| `Employees.DesignationId` | `Designations.Id` |
| `ApplicationUsers.EmployeeId` | `Employees.Id` |
| `EmployeeDocuments.EmployeeId`, `EmployeePersonalDetails.EmployeeId`, `EmployeeAddresses.EmployeeId`, `EmployeeEmergencyContacts.EmployeeId`, `EmployeeEducations.EmployeeId`, `EmployeeExperiences.EmployeeId` | `Employees.Id` |

Nothing cascades: the database refuses to delete a department, designation or employee that still has dependents.

### Unique and other indexes

| Table | Index |
|---|---|
| `Employees` | unique `EmployeeCode`, unique `Email`; `DepartmentId`, `DesignationId` |
| `Departments`, `Designations` | unique `Name` |
| `ApplicationUsers` | unique `Username`, unique `EmployeeId` |
| `EmployeePersonalDetails` | unique `EmployeeId`; unique `NationalId` and unique `PassportNo` (both filtered `IS NOT NULL`) |
| `EmployeeAddresses` | unique (`EmployeeId`, `AddressType`) |
| `EmployeeDocuments` | (`EmployeeId`, `IsActive`) |
| `EmployeeEmergencyContacts`, `EmployeeEducations`, `EmployeeExperiences` | `EmployeeId` |

Uniqueness comparisons use SQL Server's default case-insensitive collation.

### Soft delete

| Record | "Delete" means | Flag |
|---|---|---|
| Employee | `Status = Inactive` | `Status` (no separate `IsActive` column) |
| Department / Designation | deactivate | `IsActive = false` |
| Employee document | deactivate; row and file kept for HR history | `IsActive = false` |
| Profile records (personal details, addresses, contacts, education, experience) | permanent delete | none — hard delete |

### Behavior built into the DbContext

- **Unique-index races → 409:** `SaveChangesAsync` converts SQL errors 2601/2627 into `ConflictException`. Verified in
  the audit with 10 parallel identical creates: 1 × `201`, 9 × `409`, exactly one row stored.
- **UTC timestamps:** a value converter marks `CreatedAt`/`UpdatedAt` as UTC when read, so they serialize with `Z`.
  Date-only values (`DateOfBirth`, `JoiningDate`, document dates, ...) are left unzoned.

---

## 6. Entity Documentation

### BaseEntity
`Id` (`Guid`), `CreatedAt` (UTC, set on creation), `UpdatedAt` (UTC, nullable; set by services on every change).
`EmployeeOwnedEntity` = `BaseEntity` + required `EmployeeId` (used by the Day 11 profile records).

### Department / Designation
| Field | Rule |
|---|---|
| `Name` | required, max 100, unique |
| `Description` | optional, max 500 |
| `IsActive` | default `true`; `false` = deactivated |
| `Employees` | navigation |

Designations are independent of departments (no department link).

### Employee
| Field | Rule |
|---|---|
| `EmployeeCode` | required, max 50, unique, cannot change after creation |
| `FirstName`, `LastName` | required, max 100 |
| `Email` | required, max 200, unique |
| `Phone` | optional, max 30 |
| `DateOfBirth` | required, in the past |
| `JoiningDate` | required, not before `DateOfBirth` |
| `DepartmentId`, `DesignationId` | required FKs |
| `EmploymentType` | `FullTime` (default), `PartTime`, `Contract`, `Intern` — stored as string |
| `BasicSalary` | optional `decimal(18,2)`, 0 or more |
| `Status` | `Active` (default), `Inactive`, `Resigned`, `Terminated`, `OnLeave` — stored as string |
| `PhotoUrl` | optional, max 500: relative URL `/uploads/employees/{id:N}_{random}.{ext}` |
| navigations | `Department`, `Designation`, `ApplicationUser` (0..1), `PersonalDetails` (0..1), `Addresses`, `EmergencyContacts`, `Educations`, `Experiences`, `Documents` |

`Status` is the single source of truth for activity; responses add a computed `isActive` (`Active` or `OnLeave`) and
`fullName`.

### ApplicationUser
`EmployeeId` (unique FK), `Username` (max 100, unique), `PasswordHash`, `IsActive`. The table exists since
`InitialCreate` but **no code uses it** — it is reserved for future authentication.

### EmployeePersonalDetails (0..1 per employee)
`Gender` (`Male`/`Female`/`Other`), `MaritalStatus` (`Single`/`Married`/`Divorced`/`Widowed`/`Separated`),
`BloodGroup` (`APositive` … `ONegative`), `Nationality` (100), `NationalId` (17, unique when set),
`PassportNo` (20, unique when set, stored upper-case). All optional. Date of birth is not duplicated here.

### EmployeeAddress
`AddressType` (`Present`/`Permanent`, at most one of each per employee), `Address` (500), `City` (100), `District`
(100) required; `PostalCode` (20) optional.

### EmployeeEmergencyContact
`Name` (150), `Relationship` (50), `Phone` (30) required; `Email` (200), `Address` (500) optional.

### EmployeeEducation
`Degree` (100), `Institution` (200), `PassingYear` (int) required; `Major` (150), `Result` (50) optional.

### EmployeeExperience
`CompanyName` (200), `Designation` (150, free-text job title), `StartDate` required; `EndDate` optional (`null` =
current job); `Responsibilities` (2000) optional.

### EmployeeDocument
| Field | Rule |
|---|---|
| `EmployeeId` | required FK |
| `DocumentType` | enum stored as string (§15) |
| `DocumentName` | required, max 200 (Day 12) |
| `IssueDate`, `ExpiryDate` | optional calendar dates; expiry not before issue (Day 12) |
| `FileName` | required, max 255: sanitized original name, used only as the download name |
| `FilePath` | required, max 500: server-generated storage key `{employeeId:N}/{random}{ext}`; **never returned by the API** |
| `ContentType` | required, max 100: set by the server from the validated extension |
| `FileSizeBytes` | size in bytes |
| `Description` | optional, max 500 |
| `IsActive` | default `true`; `false` = deactivated (soft delete) |
| `CreatedAt` | upload time (returned as `uploadedAt`) |

---

## 7. DTO Documentation

All request DTOs are validated with Data Annotations before the service runs; enums travel as their **names**
(case-insensitive on input; numbers rejected). Responses are always wrapped in `ApiResponse<T>` (§18).

| DTO | Used by | Fields |
|---|---|---|
| `EmployeeDto` | employee responses | `id`, `employeeCode`, `firstName`, `lastName`, `fullName`, `email`, `phone`, `dateOfBirth`, `joiningDate`, `departmentId`, `departmentName`, `designationId`, `designationName`, `employmentType`, `basicSalary`, `status`, `isActive`, `photoUrl`, `createdAt`, `updatedAt` |
| `CreateEmployeeDto` | `POST /api/employees` | `employeeCode`*, `firstName`*, `lastName`*, `email`*, `phone`, `dateOfBirth`*, `joiningDate`*, `departmentId`*, `designationId`*, `employmentType`, `basicSalary` |
| `UpdateEmployeeDto` | `PUT /api/employees/{id}` | as create without `employeeCode`, plus `status`. Omitted `employmentType`/`basicSalary`/`status` keep their current value |
| `UploadEmployeePhotoDto` | internal (controller → service) | `Content` (stream), `FileName`, `ContentType`, `Length` |
| `EmployeeProfileDto` | `GET /api/employees/{id}/profile` | `employeeId`, `employeeCode`, `fullName`, `firstName`, `lastName`, `photoUrl`, `isActive`, `personalInformation`, `jobInformation`, `addresses`, `emergencyContacts`, `educations`, `experiences`, `documents` (active only), `createdAt`, `updatedAt` |
| `EmployeePersonalInformationDto` | profile section | `dateOfBirth`, `phone`, `email`, `hasPersonalDetails`, `gender`, `maritalStatus`, `bloodGroup`, `nationality`, `nationalId` (masked), `passportNo` (masked) |
| `EmployeeJobInformationDto` | profile section | `departmentId`, `departmentName`, `designationId`, `designationName`, `joiningDate`, `employmentType`, `employmentStatus`, `basicSalary` |
| `DepartmentDto` / `DesignationDto` | responses | `id`, `name`, `description`, `isActive`, `employeeCount`, `createdAt`, `updatedAt` |
| `CreateDepartmentDto` / `CreateDesignationDto` | POST | `name`* (max 100), `description` (max 500) |
| `UpdateDepartmentDto` / `UpdateDesignationDto` | PUT | `name`*, `description`, `isActive`* (required so omitting it can't deactivate by accident) |
| `EmployeePersonalDetailsDto` | personal-details responses | `id`, `employeeId`, `dateOfBirth` (read-only), `gender`, `maritalStatus`, `bloodGroup`, `nationality`, `nationalId`, `passportNo` (unmasked), `createdAt`, `updatedAt` |
| `Create/UpdateEmployeePersonalDetailsDto` | POST/PUT (full replace) | `gender`, `maritalStatus`, `bloodGroup`, `nationality` (100), `nationalId` (10/13/17 digits), `passportNo` (6–20 letters/digits) |
| `EmployeeAddressDto` + `Create/UpdateEmployeeAddressDto` | addresses | `addressType`*, `address`* (500), `city`* (100), `district`* (100), `postalCode` (3–20 letters/digits/spaces/dashes) |
| `EmployeeEmergencyContactDto` + create/update | contacts | `name`* (150), `relationship`* (50), `phone`* ([Phone], 30), `email` ([EmailAddress], blank = none), `address` (500) |
| `EmployeeEducationDto` + create/update | education | `degree`* (100), `institution`* (200), `major` (150), `result` (50), `passingYear`* (1900–2200, plus service rule) |
| `EmployeeExperienceDto` + create/update | experience | `companyName`* (200), `designation`* (150), `startDate`*, `endDate`, `responsibilities` (2000); response adds `isCurrent` |
| `EmployeeDocumentDto` | document responses | `id`, `employeeId`, `documentType`, `documentName`, `issueDate`, `expiryDate`, `fileName`, `contentType`, `fileSizeBytes`, `description`, `isActive`, `uploadedAt`, `updatedAt`, `downloadUrl` (never a storage path) |
| `UploadEmployeeDocumentDto` | internal (controller → service) | `Content`, `FileName`, `ContentType`, `Length`, `DocumentType`, `DocumentName`, `IssueDate`, `ExpiryDate`, `Description` |
| `UpdateEmployeeDocumentDto` | `PUT .../documents/{id}` | `documentType`, `documentName` (omitted = kept), `issueDate`, `expiryDate`, `description` (omitted = cleared) |
| `EmployeeDocumentFileDto` | internal (download) | `Content`, `FileName`, `ContentType` |

`*` = required. Update DTOs of profile records inherit their create DTO and replace every field.

---

## 8. Service Layer

All services are registered **scoped** in `AddApplication`.

| Service | Responsibilities |
|---|---|
| `EmployeeService` | `GetAllAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeactivateAsync`, `UploadPhotoAsync`, `RemovePhotoAsync`. Trims input, enforces unique code/email, date rules, department/designation existence and active state, reactivation rules, photo validation and replace sequencing |
| `EmployeeProfileService` | `GetProfileAsync`: builds the profile from one split query; masks NID (last 4) and passport (last 3); orders addresses Present→Permanent, education and experience newest first, active documents newest first |
| `DepartmentService`, `DesignationService` | `GetAllAsync` (with employee counts), `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeactivateAsync`. Unique names; deactivation refused (409) while `Active`/`OnLeave` employees are assigned |
| `EmployeePersonalDetailsService` | `GetAsync`, `CreateAsync` (409 if present), `UpdateAsync`, `DeleteAsync`; NID/passport uniqueness |
| `EmployeeOwnedRecordService<TEntity, TSaveDto, TDto>` (base) → `EmployeeAddressService`, `EmployeeEmergencyContactService`, `EmployeeEducationService`, `EmployeeExperienceService` | `GetAllAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`. Every call first checks the employee exists and looks records up by employee **and** record id. Subclasses add their rules (one address per type, unique contact phone, passing-year range and duplicate check, experience date rules) |
| `EmployeeDocumentService` | `UploadAsync`, `GetByEmployeeAsync(includeInactive)`, `GetByIdAsync`, `DownloadAsync`, `UpdateAsync`, `DeactivateAsync` (§15) |

Helpers: `EmployeePhotoPolicy` (photo types/size), `EmployeeDocumentPolicy` (verifiable document formats, content
types, startup validation), `EmployeeStatusRules` (`Active` and `OnLeave` count as current), `FileSignature`,
`InputText`.

---

## 9. Repository Layer

Interfaces live in `SmartHRMS.Application/Interfaces`, implementations in `SmartHRMS.Infrastructure/Repositories`,
all registered **scoped**. Every method is async and takes a `CancellationToken`. Read-only list queries use
`AsNoTracking`.

| Repository | Methods |
|---|---|
| `EmployeeRepository` | `GetByIdAsync` (with department/designation), `GetAllAsync` (ordered by employee code), `GetProfileAsync` (split query with every profile collection, active documents only), `ExistsAsync`, `EmployeeCodeExistsAsync`, `EmailExistsAsync`, `AddAsync`, `SaveChangesAsync` |
| `DepartmentRepository` / `DesignationRepository` | `GetByIdAsync`, `GetAllAsync` (ordered by name), `NameExistsAsync`, `CountEmployeesAsync(statuses)`, `GetEmployeeCountsAsync`, `AddAsync`, `SaveChangesAsync` |
| `EmployeeDocumentRepository` | `GetByIdAsync(employeeId, documentId)`, `GetByEmployeeIdAsync(employeeId, includeInactive)` (newest first), `AddAsync`, `SaveChangesAsync` |
| `EmployeeOwnedRepository<T>` (generic) | `GetByIdAsync(employeeId, id)`, `GetByEmployeeIdAsync`, `AnyAsync`, `AddAsync`, `Remove`, `SaveChangesAsync` |

Storage services (registered **singleton**): `LocalFileStorageService` (`SaveAsync`, `DeleteAsync`) under `wwwroot`,
and `LocalDocumentStorageService` (`SaveAsync`, `OpenReadAsync`, `DeleteAsync`) under `App_Data/employee-documents`.
Both resolve every path through `SafeStoragePath`, which drops `..`, drive letters and separators and refuses any path
outside its root.

---

## 10. Controllers

Controllers are thin: bind the request, call one service method, return `ApiResponse<T>`. All routes use the
`{id:guid}` constraint, so a malformed id is a `404`. **Authorization: none on any endpoint** (§17).

| Controller | Base route | Endpoints |
|---|---|---|
| `HealthController` | `/api/health` | `GET` |
| `EmployeesController` | `/api/employees` | `GET`, `GET {id}`, `GET {id}/profile`, `POST`, `PUT {id}`, `DELETE {id}`, `PUT {id}/photo`, `DELETE {id}/photo` |
| `DepartmentsController` | `/api/departments` | `GET`, `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}` |
| `DesignationsController` | `/api/designations` | `GET`, `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}` |
| `EmployeePersonalDetailsController` | `/api/employees/{employeeId}/personal-details` | `GET`, `POST`, `PUT`, `DELETE` |
| `EmployeeAddressesController` | `/api/employees/{employeeId}/addresses` | `GET`, `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}` |
| `EmployeeEmergencyContactsController` | `/api/employees/{employeeId}/emergency-contacts` | same as addresses |
| `EmployeeEducationsController` | `/api/employees/{employeeId}/educations` | same as addresses |
| `EmployeeExperiencesController` | `/api/employees/{employeeId}/experiences` | same as addresses |
| `EmployeeDocumentsController` | `/api/employees/{employeeId}/documents` | `GET`, `GET {documentId}`, `POST`, `GET {documentId}/download`, `PUT {documentId}`, `DELETE {documentId}` |

The OpenAPI document lists **21 distinct route templates with no duplicates** (checked in the audit). Every endpoint
with its request, response and status codes is in §21.

---

## 11. Employee Management

- **Create** (`POST /api/employees`, `201` + `Location`): JSON only (the photo is uploaded separately). Code, email and
  names are trimmed before the uniqueness checks. Code and email must be unique (`409`, case-insensitive). Department
  and designation must exist and be active (`400`). Date of birth must be in the past; joining date not before it
  (`400`). `employmentType` defaults to `FullTime`; new employees are `Active`.
- **Read:** `GET /api/employees` returns every employee (any status), **ordered by employee code**, with department and
  designation names. There is no server-side search, filter or paging. `GET /api/employees/{id}` returns one (`404`).
- **Update** (`PUT /api/employees/{id}`): full update of the editable fields; the code can't change. Omitted
  `employmentType`, `basicSalary` and `status` keep their values. A department/designation that has since been
  deactivated may be kept, but not newly assigned. Setting `Active`/`OnLeave` on a former employee is a
  **reactivation**, which requires an active department and designation.
- **Deactivate** (`DELETE /api/employees/{id}`): sets `Status = Inactive`; the row and all related records stay.
- **Details:** `GET /api/employees/{id}/profile` (§11a).
- **Photo:** §14.
- Since Day 11, `address`, `permanentAddress`, `gender`, `bloodGroup` and `emergencyContact*` are not part of the
  employee JSON; if sent they are ignored (use the profile endpoints).

### 11a. Employee details / profile

`GET /api/employees/{id}/profile` returns every section in one call (`404` for an unknown or malformed id):
personal information, job information, addresses, emergency contacts, education, experience and **active** documents.
Every section is always present (`[]` when empty; `hasPersonalDetails: false` with `null` fields when no personal
details exist). NID and passport numbers are **masked** here; the full values come only from
`GET .../personal-details`. No internal fields (storage paths, `ApplicationUser`, password hash) are exposed.

### 11b. Profile records (Day 11)

| Resource | Rules |
|---|---|
| Personal details (0..1) | `POST` when present → `409`; `GET`/`PUT`/`DELETE` when absent → `404`. NID 10/13/17 digits and passport 6–20 letters/digits, each unique across employees (`409`) |
| Addresses | one `Present` and one `Permanent` at most (`409`) |
| Emergency contacts | any number; the same phone twice for one employee → `409` |
| Education | `passingYear` from the employee's birth year to current year + 5 (`400`); exact duplicate → `409`; newest first |
| Experience | `startDate` after date of birth and not in the future; `endDate` optional, not before `startDate`, not in the future (`400`); newest first |

A record requested through another employee's route is `404`. `PUT` replaces every field.

---

## 12. Department Management

- **Create** `POST /api/departments` → `201`; name required (max 100), trimmed, unique case-insensitively (`409`);
  description optional (max 500, blank → `null`). New departments are active.
- **Read** `GET /api/departments` (all, active and inactive, ordered by name, each with `employeeCount` = all assigned
  employees) and `GET /api/departments/{id}` (`404`).
- **Update** `PUT /api/departments/{id}`: `name`, `description`, `isActive` (required). `isActive: true` reactivates.
- **Deactivate** `DELETE /api/departments/{id}` (or `PUT` with `isActive: false`): soft delete, idempotent. Refused
  with `409` while any `Active`/`OnLeave` employee is assigned. Rows are never physically deleted.
- An inactive department can't be assigned to employees (`400`).

## 13. Designation Management

Identical rules and routes to departments, at `/api/designations`. Designations are not linked to departments.

---

## 14. Employee Photo

| Aspect | Implementation |
|---|---|
| Upload / replace | `PUT /api/employees/{id}/photo`, `multipart/form-data`, field `photo`; returns the updated employee |
| Remove | `DELETE /api/employees/{id}/photo`; idempotent (no photo → `200`) |
| Validation (`400`) | file present and non-empty; ≤ 5 MB; extension `.jpg`, `.jpeg`, `.png`, `.webp`; `Content-Type` `image/jpeg`, `image/png` or `image/webp`; first bytes must match the image format. Requests over the 6 MB `[RequestSizeLimit]` are also `400` |
| Storage | `wwwroot/uploads/employees/{employeeId:N}_{random}{ext}`; the client file name is never used (only its extension, after validation) |
| Database | `Employees.PhotoUrl` stores the relative URL only |
| Access | served as a static file at the `photoUrl` path (e.g. `http://localhost:5099/uploads/employees/...`) |
| Replacement | new file saved → `PhotoUrl` updated and saved → old file deleted. Each version has its own URL (no stale browser cache) |
| Security | path traversal impossible (generated names + `SafeStoragePath`); `wwwroot` holds nothing but `uploads/` |
| Not protected | anyone who can reach the API can upload/remove photos (no authentication) |

Verified in the audit on both the database and the file system: the stored `PhotoUrl` equals the response, the file
is byte-identical to the upload, the old file disappears on replace, and remove deletes the file and clears the column.

---

## 15. Employee Document Management

All routes are nested under the employee. A document is always looked up by **employee id and document id
together**, so another employee's document through the wrong route is `404`. (Day 12's spec suggested flat
`/api/employee-documents/{id}` routes; the nested convention was kept because it is what enforces this isolation.)

| Operation | Endpoint | Behavior |
|---|---|---|
| Upload | `POST /api/employees/{employeeId}/documents` | `multipart/form-data`: `file`, `documentType`, `documentName`, optional `issueDate`, `expiryDate` (yyyy-MM-dd), `description` → `201` + `Location` |
| List | `GET .../documents` | active documents, newest first; `?includeInactive=true` adds deactivated ones ("show deactivated") |
| Single | `GET .../documents/{documentId}` | metadata of an active **or deactivated** document (HR history) |
| Download | `GET .../documents/{documentId}/download` | streams the file as `attachment` with the original (sanitized) file name, the server-validated `Content-Type` and `X-Content-Type-Options: nosniff`. Deactivated document or missing physical file → `404` |
| Update metadata | `PUT .../documents/{documentId}` | JSON; the file is never changed or re-uploaded. `documentType`/`documentName` omitted = kept (a sent name can't be blank); `issueDate`, `expiryDate`, `description` omitted = cleared. Deactivated documents → `404` |
| Delete | `DELETE .../documents/{documentId}` | **SOFT DELETE / DEACTIVATION**: sets `IsActive = false`. The database row **and the physical file are kept** for HR history; the document disappears from the normal list and can no longer be downloaded or edited. Idempotent. There is no permanent-delete endpoint |

**Document types:** `Nid`, `Passport`, `BirthCertificate`, `EducationalCertificate`, `ExperienceCertificate`,
`JoiningLetter`, `Cv` (Resume/CV), `TinCertificate`, `ContractPaper`, `Other`. Case-insensitive names; numbers and
unknown names → `400` listing the allowed values.

**Allowed files:** `.pdf`, `.doc`, `.docx`, `.jpg`, `.jpeg`, `.png`, maximum **10 MB** (`EmployeeDocuments` section in
`appsettings.json`). Configuration can only choose from formats the server can verify by signature; anything else (e.g.
`.exe`) makes the API fail at startup.

**Upload validation** (all `400`, nothing stored): employee exists (`404`); `documentName` required, trimmed, max 200;
`description` max 500; expiry not before issue; unparsable date; file present and non-empty; size ≤ 10 MB; extension
allowed; client `Content-Type` must match the extension (missing or `application/octet-stream` accepted); first bytes
must match the real format (`%PDF-`, JPEG, PNG, OLE for `.doc`, ZIP for `.docx`).

**Storage strategy:** `App_Data/employee-documents/{employeeId:N}/{random}{ext}` in the API's content root — outside
`wwwroot`, so no URL serves it directly; the download endpoint is the only way in. The file is written first, then the
row; if saving the row fails, the file is deleted again (no orphan files). The storage key is never returned.

---

## 16. Validation Rules

| Area | Rules | Where |
|---|---|---|
| Required fields | `[Required]` / `[NotDefault]` (empty `Guid`, default date) | DTOs |
| String lengths | `[MaxLength]` matching the column sizes above | DTOs + EF configuration |
| Email | `[EmailAddress]` | employee, emergency contact |
| Phone | `[Phone]`; blank employee phone stored as `null` | employee, emergency contact |
| Numbers | `basicSalary` 0 … 9999999999999999.99; `passingYear` 1900–2200 + birth-year rule | DTOs + service |
| Dates | invalid JSON/form dates → `400`; employee DOB past, joining ≥ DOB; experience and education rules; document expiry ≥ issue | services |
| Enums | names only, case-insensitive; numbers/unknown → `400` | JSON options |
| Foreign keys | department/designation must exist and be active for new assignments | `EmployeeService` |
| Duplicates | employee code/email, department/designation names, NID/passport, address type, contact phone, education → `409` | services + unique indexes |
| Files | photo and document rules in §14/§15 | policies + services |
| IDs | route `{id:guid}` constraint: malformed id → `404`; unknown id → `404` | routing + services |
| Malformed body | invalid JSON → `400` "The request body is not valid JSON." or "The value for '<field>' is invalid." | `AddApiControllers` |

Validation always happens on the server; the frontend repeats the rules only for faster feedback.

---

## 17. Authentication & Authorization

**Not implemented.** No authentication scheme, no login endpoint, no tokens, no roles or policies.
`app.UseAuthorization()` is in the pipeline but has nothing to enforce, so **every endpoint is anonymous**, including
salaries, identity numbers and HR documents. `ApplicationUser` exists in the database but is unused. Consequently no
endpoint returns `401` or `403` (the envelope writer has messages for them, ready for when auth is added).

**CORS** is the only access restriction, and it applies to browsers only: `Cors:AllowedOrigins` in
`appsettings.Development.json` lists `http://localhost:5173` and `http://localhost:4173`; `appsettings.json` lists none.

Do not expose this API beyond a local development machine until authentication exists.

---

## 18. Error Handling

Every response uses the envelope (`ApiResponse<T>`):

```json
{ "success": true,  "message": "…", "data": { }, "errors": null }
{ "success": false, "message": "…", "data": null, "errors": ["…"] }
```

| Source | Status | `message` | `errors` |
|---|---|---|---|
| Model validation (`[Required]`, lengths, formats, invalid JSON/values) | 400 | `One or more validation errors occurred.` | one entry per problem |
| `BadRequestException` (business rules, file validation) | 400 | `Bad request.` | the specific reason |
| `NotFoundException` | 404 | `Resource not found.` | the detail |
| `ConflictException` (duplicates, delete protection, unique-index race) | 409 | `Conflict.` | the detail |
| Unknown route / malformed id / wrong method / wrong content type | 404 / 405 / 415 | `Resource not found.` / `Method not allowed.` / `Unsupported media type.` | `[]` |
| Any other exception | 500 | `An unexpected error occurred.` | `[]` — logged, never sent to the client |
| Client disconnected mid-request (any exception type) | 499 | no body; logged at Debug, not as an error (audit fix) | |

Success codes: `200` (reads, updates, deletes, photo operations), `201` + `Location` (creates and uploads). The API
does not use `204`.

Known log behavior: when two identical creates race, EF Core logs the refused insert at error level
(`Microsoft.EntityFrameworkCore.Update[10000]`) before it is converted to `409`; the client never sees a `500`.

---

## 19. File Storage

| Kind | Location | Public? | Naming |
|---|---|---|---|
| Employee photos | `smartHRMS.Api/wwwroot/uploads/employees/` | yes — static files under `/uploads/...` | `{employeeId:N}_{random}.{ext}` |
| Employee documents | `smartHRMS.Api/App_Data/employee-documents/{employeeId:N}/` (configurable `EmployeeDocuments:StoragePath`) | **no** — only via the download endpoint | `{random}.{ext}` |

Both folders are gitignored. Photos of replaced/removed versions are deleted; documents are never deleted (soft
delete). Paths are always resolved through `SafeStoragePath`; client file names never decide where a file is written.

---

## 20. Migration & Database Setup

Run from `backend/`:

```bash
dotnet ef migrations add <Name> --project SmartHRMS.Infrastructure --startup-project smartHRMS.Api
dotnet ef database update --project SmartHRMS.Infrastructure --startup-project smartHRMS.Api
dotnet ef migrations has-pending-model-changes --project SmartHRMS.Infrastructure --startup-project smartHRMS.Api
dotnet ef migrations remove --project SmartHRMS.Infrastructure --startup-project smartHRMS.Api
```

Applied migrations (all 7 applied; no pending model changes):

| # | Migration | Change |
|---|---|---|
| 1 | `InitialCreate` | Departments, Designations, Employees, ApplicationUsers, indexes, FKs |
| 2 | `UpdateEmployeeStatusToEnum` | `Employees.Status` as `nvarchar(20)`; redundant `IsActive` dropped |
| 3 | `AddEmployeePhotoUrl` | `Employees.PhotoUrl` |
| 4 | `AddEmployeeProfileFields` (Day 9) | `Address`, `Gender`, `EmploymentType` (default `FullTime`) |
| 5 | `AddEmployeeProfileAndDocuments` (Day 10) | salary, blood group, permanent address, emergency contact columns; `EmployeeDocuments` table |
| 6 | `AddEmployeeProfileRecords` (Day 11) | five profile tables; drops the flat address/gender/blood-group/emergency-contact columns (all empty when applied) |
| 7 | `AddEmployeeDocumentDetails` (Day 12) | `DocumentName` (backfilled from `FileName`), `IssueDate`, `ExpiryDate` |

Before each schema change since Day 9 a verified copy-only backup was taken
(`SmartHRMSDB_before_Day9/10/11/12_*.bak` in the SQL Server backup folder). Migrations are only generated after a
domain change; applied migrations are never edited.

> **Windows Smart App Control:** on this machine a freshly built, unsigned `smartHRMS.*.dll` can be blocked
> ("An Application Control policy has blocked this file") by `dotnet run` and `dotnet ef`. Rebuilding with
> `dotnet build smartHRMS.slnx -p:Deterministic=false` gives the DLL a new hash; so far it has loaded on the first retry.
> Also stop a running API before building, or its locked DLLs make the build fail.

---

## 21. API Endpoint Reference

Base URL (Development): `http://localhost:5099`. **Auth: none for every endpoint.** Common errors on every endpoint
with a body: `400` (validation), `500` (unexpected). `{id}` values are GUIDs.

| Method | Endpoint | Purpose | Request | Response | Status codes |
|---|---|---|---|---|---|
| GET | `/api/health` | health check | — | status, application, version | 200 |
| GET | `/api/employees` | list employees | — | `EmployeeDto[]` (by code) | 200 |
| GET | `/api/employees/{id}` | one employee | — | `EmployeeDto` | 200, 404 |
| GET | `/api/employees/{id}/profile` | full profile | — | `EmployeeProfileDto` | 200, 404 |
| POST | `/api/employees` | create | `CreateEmployeeDto` | `EmployeeDto` | 201, 400, 409 |
| PUT | `/api/employees/{id}` | update / reactivate | `UpdateEmployeeDto` | `EmployeeDto` | 200, 400, 404, 409 |
| DELETE | `/api/employees/{id}` | deactivate (soft) | — | message | 200, 404 |
| PUT | `/api/employees/{id}/photo` | upload/replace photo | multipart `photo` | `EmployeeDto` | 200, 400, 404 |
| DELETE | `/api/employees/{id}/photo` | remove photo | — | `EmployeeDto` | 200, 404 |
| GET | `/api/departments` | list | — | `DepartmentDto[]` | 200 |
| GET | `/api/departments/{id}` | one | — | `DepartmentDto` | 200, 404 |
| POST | `/api/departments` | create | `CreateDepartmentDto` | `DepartmentDto` | 201, 400, 409 |
| PUT | `/api/departments/{id}` | update / (de)activate | `UpdateDepartmentDto` | `DepartmentDto` | 200, 400, 404, 409 |
| DELETE | `/api/departments/{id}` | deactivate (soft) | — | message | 200, 404, 409 |
| GET/POST/PUT/DELETE | `/api/designations[/{id}]` | same as departments | `Create/UpdateDesignationDto` | `DesignationDto` | same as departments |
| GET | `/api/employees/{employeeId}/personal-details` | full personal details | — | `EmployeePersonalDetailsDto` | 200, 404 |
| POST | `/api/employees/{employeeId}/personal-details` | add | `CreateEmployeePersonalDetailsDto` | same | 201, 400, 404, 409 |
| PUT | `/api/employees/{employeeId}/personal-details` | replace | `UpdateEmployeePersonalDetailsDto` | same | 200, 400, 404, 409 |
| DELETE | `/api/employees/{employeeId}/personal-details` | delete (hard) | — | message | 200, 404 |
| GET | `/api/employees/{employeeId}/addresses` | list | — | `EmployeeAddressDto[]` | 200, 404 |
| GET | `/api/employees/{employeeId}/addresses/{id}` | one | — | `EmployeeAddressDto` | 200, 404 |
| POST | `/api/employees/{employeeId}/addresses` | add | `CreateEmployeeAddressDto` | same | 201, 400, 404, 409 |
| PUT | `/api/employees/{employeeId}/addresses/{id}` | replace | `UpdateEmployeeAddressDto` | same | 200, 400, 404, 409 |
| DELETE | `/api/employees/{employeeId}/addresses/{id}` | delete (hard) | — | message | 200, 404 |
| (same 5) | `/api/employees/{employeeId}/emergency-contacts[/{id}]` | emergency contacts | contact DTOs | `EmployeeEmergencyContactDto` | as addresses |
| (same 5) | `/api/employees/{employeeId}/educations[/{id}]` | education | education DTOs | `EmployeeEducationDto` | as addresses |
| (same 5) | `/api/employees/{employeeId}/experiences[/{id}]` | experience | experience DTOs | `EmployeeExperienceDto` | as addresses |
| GET | `/api/employees/{employeeId}/documents[?includeInactive=true]` | list documents | — | `EmployeeDocumentDto[]` | 200, 404 |
| GET | `/api/employees/{employeeId}/documents/{documentId}` | one document | — | `EmployeeDocumentDto` | 200, 404 |
| POST | `/api/employees/{employeeId}/documents` | upload | multipart (§15) | `EmployeeDocumentDto` | 201, 400, 404 |
| GET | `/api/employees/{employeeId}/documents/{documentId}/download` | download file | — | file stream | 200, 404 |
| PUT | `/api/employees/{employeeId}/documents/{documentId}` | edit metadata | `UpdateEmployeeDocumentDto` | `EmployeeDocumentDto` | 200, 400, 404 |
| DELETE | `/api/employees/{employeeId}/documents/{documentId}` | **deactivate (soft)** | — | message | 200, 404 |

Development-only: `GET /openapi/v1.json` (OpenAPI document) and `/swagger` (Swagger UI).

---

## 22. Testing

```bash
cd backend
dotnet restore
dotnet build smartHRMS.slnx          # add -p:Deterministic=false if Smart App Control blocks the DLL
dotnet test smartHRMS.slnx
dotnet ef migrations has-pending-model-changes --project SmartHRMS.Infrastructure --startup-project smartHRMS.Api
```

| Kind | What | Result in the Day 1–12 audit |
|---|---|---|
| Build | `dotnet build` | 0 warnings, 0 errors |
| Unit tests | 169 xUnit tests (services, DTO validation, envelope, photo, documents, profile records) with in-memory fakes | 169 / 169 passed |
| Migrations | `migrations list`, `has-pending-model-changes`, live schema query | 7 / 7 applied, no drift, schema matches configuration |
| Runtime API (Day 1–10 regression) | CRUD, relationships, photo, documents, envelope, errors | 193 / 193 |
| Runtime API (Day 11) | personal details, addresses, contacts, education, experience, profile | 167 / 167 |
| Runtime API (Day 12) | document upload/list/get/download/update/soft delete, validation, cross-employee access, storage checks | 108 / 108 |
| Runtime API (audit gaps) | Swagger/OpenAPI, 404/405/415, negative inputs, photo DB+file consistency, document soft-delete on disk | 57 / 57 |
| Browser (React frontend) | every page and flow, simulated 401/403/500/502/network errors, mobile/tablet layouts | 59 / 59, no JS errors |
| Concurrency | 10 parallel identical creates | 1 × 201, 9 × 409, one row |
| Cancellation | client disconnect during a blocked update | no error logged, update not applied |
| Data integrity | orphans, invalid enums, current employees in inactive departments, photo files vs database | none found |

The runtime suites are shell/Node scripts kept outside the repository and run against a local instance with
`TEST-*` records only, which are removed afterwards; a checksum of all real data was identical before and after.
The repository itself contains only unit tests (see §24).

---

## 23. Day 1–12 Completion Matrix

| Day | Feature | Status | Evidence |
|-----|---------|--------|----------|
| 1 | Solution builds | PASS | 0 warnings, 0 errors |
| 1 | Project structure, references, no circular dependency | PASS | `.csproj` references (§2) |
| 1 | Dependency injection | PASS | all services resolve; every endpoint answered at runtime |
| 1 | Configuration / environments | PASS | Development CORS origins and document options applied at runtime |
| 1 | API startup | PASS | `/api/health` 200 |
| 1 | Swagger / OpenAPI | PASS | `/swagger` and `/openapi/v1.json` 200; 21 routes, no duplicates |
| 1 | Health endpoint | PASS | 200 with envelope |
| 2 | Database and connection | PASS | live queries against `SmartHRMSDB` |
| 2 | Entities mapped: PKs, FKs, lengths, required fields | PASS | `sys.tables`, `sys.foreign_keys`, `INFORMATION_SCHEMA` |
| 2 | Unique indexes | PASS | `sys.indexes` (§5) |
| 2 | Safe delete behavior | PASS | every FK `NO_ACTION` |
| 2 | No unexpected tables | PASS | 10 tables + history; `ApplicationUsers` intentional (unused) |
| 3 | Layer separation | PASS | Domain/Application/Infrastructure/Api (§2) |
| 3 | Thin controllers, logic in services | PASS | controllers only bind and delegate |
| 3 | DTOs separate from entities | PASS | no entity is returned by any endpoint |
| 4 | Exception handling and envelope | PASS | 400/404/409/500 envelopes verified; 500 hides details |
| 4 | 404/405/415 envelopes | PASS | audit gap suite |
| 4 | Cancelled requests handled quietly | PASS (fixed) | was PARTIAL: logged as errors; forced-disconnect test now logs nothing |
| 4 | CORS | PASS | allowed origin headers on success and errors; other origins refused |
| 5 | Migrations applied, no drift | PASS | 7/7, `has-pending-model-changes` clean |
| 5 | Async data access with cancellation tokens | PASS | code review; cancellation test |
| 5 | UTC timestamps | PASS | responses end in `Z` |
| 5 | Unique-index race → 409 | PASS | 10 parallel creates → 1 × 201, 9 × 409 |
| 6 | CRUD workflow end-to-end | PASS | regression suite |
| 6 | Validation, not-found, duplicates | PASS | regression + audit suites |
| 7 | Employee create/read/update/deactivate | PASS | regression suite + database checks |
| 7 | Code and email uniqueness (case-insensitive) | PASS | 409 on duplicates |
| 7 | Invalid / unknown id | PASS | 404 |
| 7 | Deterministic list order | PASS (fixed) | was PARTIAL: no ORDER BY; now ordered by employee code, verified at runtime |
| 8 | Department CRUD + soft delete | PASS | regression suite |
| 8 | Designation CRUD + soft delete | PASS | regression suite |
| 8 | Unique names | PASS | 409 |
| 8 | Delete protection | PASS | 409 while current employees assigned |
| 8 | Employee ↔ department/designation validation | PASS | unknown → 400, inactive → 400 |
| 9 | All employee fields persisted | PASS | response + database comparison |
| 9 | Email/phone/date/enum/salary validation | PASS | audit + regression negatives |
| 9 | Status changes and reactivation | PASS | regression suite |
| 9 | Server-side search/filter/pagination | N/A | not implemented in the API (the frontend filters client-side) |
| Photo | Upload and validation (type, content type, signature, size, empty) | PASS | regression + audit |
| Photo | Unique stored name, path traversal prevented | PASS | traversal file name stored safely |
| Photo | Database reference matches physical file | PASS | byte-identical file, `PhotoUrl` equals response |
| Photo | Replacement deletes old file | PASS | old file gone, one file per employee |
| Photo | Remove | PASS | file deleted, column cleared |
| Photo | Unauthorized upload blocked | MISSING | no authentication exists |
| 10–11 | Profile endpoint (all sections, empty states, masking) | PASS | Day 11 suite |
| 10–11 | Invalid / unknown employee | PASS | 404 |
| 10–11 | No sensitive internal fields | PASS | no storage path, password hash or user data in responses |
| 11 | Personal details | PASS | Day 11 suite |
| 11 | Addresses | PASS | Day 11 suite |
| 11 | Emergency contacts | PASS | Day 11 suite |
| 11 | Education | PASS | Day 11 suite |
| 11 | Experience | PASS | Day 11 suite |
| 11 | Cross-employee isolation | PASS | 404 through another employee's route |
| 12 | Schema and migration | PASS | columns verified in the database |
| 12 | Metadata fields (name, dates, type, size, content type, description, timestamps) | PASS | Day 12 suite |
| 12 | Document types (incl. Birth and TIN certificates) | PASS | uploads of every type; unknown type → 400 |
| 12 | Upload validation (extension, content type, signature, size, empty, name, dates) | PASS | Day 12 suite |
| 12 | Safe storage, no path exposure, traversal prevented | PASS | Day 12 suite + file system checks |
| 12 | Failure cleanup (storage failure, database failure) | PASS | forced storage failure → 500, no orphan row; unit test for database failure |
| 12 | List, show deactivated, cross-employee | PASS | Day 12 suite |
| 12 | Single document | PASS | Day 12 suite |
| 12 | Download (bytes, type, name, nosniff, missing file → 404) | PASS | Day 12 + regression suites |
| 12 | Update metadata without touching the file | PASS | file bytes identical after update |
| 12 | Soft delete keeps row and file, hides document, idempotent | PASS | database + file system checks |
| 12 | Authentication/authorization on documents | MISSING | no authentication exists |
| Auth | Authentication | MISSING | no scheme, login or tokens |
| Auth | Role-based authorization | MISSING | no roles or policies |
| Auth | 401/403 responses | N/A | cannot occur without authentication |
| HTTP | 204 No Content | N/A | the API returns 200 with an envelope by convention |
| Quality | Pagination on list endpoints | PARTIAL | not needed at current data size; all lists return every row |
| Quality | Automated integration tests in the repository | PARTIAL | unit tests only; runtime suites live outside the repository |
| Quality | Package health | PASS | no vulnerable or deprecated runtime packages (xUnit 2 is marked legacy in favor of xUnit v3) |
| Quality | Frontend build, lint and UI flows | PASS | `npm run build`, `npm run lint`, 59/59 browser checks |
| Data | Database integrity | PASS | no orphans, invalid values or file mismatches |

---

## 24. Known Limitations

| Severity | Limitation | Impact |
|---|---|---|
| High | **No authentication or authorization** | Every endpoint, including salaries, NID/passport numbers and HR documents, is callable anonymously. Document isolation per employee exists, but no user or role check. Not safe beyond a local machine |
| Low | No server-side search, filter or pagination | Lists return every row; fine at current size, slower as data grows |
| Low | Unit tests only in the repository | EF queries and the HTTP pipeline are covered by external runtime scripts, not by automated tests in the solution |
| Low | EF Core logs refused duplicate inserts at error level | Only during genuine concurrent duplicates; clients correctly get 409 |
| Low | Photo limits are constants | `EmployeePhotoPolicy` (5 MB; jpg/png/webp) is not configurable, unlike documents |
| Low | Photo request over 6 MB | Returns 400 with the framework's "request body too large" message rather than the photo-specific one |
| Low | Removed employee JSON fields are ignored silently | Old clients sending `address`, `gender`, ... get no error |
| Low | Department and Designation code is duplicated | Two parallel copies of service/repository/controller |
| Low | Redundant EF package references in `smartHRMS.Api.csproj` | `SqlServer`/`Tools` also come through Infrastructure (`Design` is needed) |
| Low | xUnit 2 is marked legacy | Tests work; migration to xUnit v3 is optional |
| Info | Smart App Control on the development machine | May block freshly built DLLs; rebuild with `-p:Deterministic=false` |

**Not implemented (future work, not part of Day 1–12):** authentication (JWT tied to `ApplicationUser`) and role-based
authorization; attendance, leave, payroll, recruitment, performance modules; cloud storage for photos and documents
(the storage interfaces allow adding it without changing the Application layer).
