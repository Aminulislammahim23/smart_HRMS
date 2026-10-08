# SmartHRMS Backend Documentation

> Describes the code, database and API **as they are after Day 19 (2026-10-08)**. Everything marked as implemented was
> verified at runtime against the SQL Server database (see §22, §23 and §25–§29). Planned work is listed only in §24
> and is clearly marked as not implemented. Day 16 also delivered the authentication, leave and audit groundwork that
> payroll depends on (§26.1–§26.4), because no earlier day had built it.

---

## 1. Project Overview

**SmartHRMS** is a Human Resource Management System. This repository contains its backend: an ASP.NET Core Web API
that stores HR data in SQL Server and serves it as JSON to the React frontend in `frontend/smarthrms-web`.

**Current scope (Day 1–19):**

| Area | What exists |
|---|---|
| Organization | Departments and Designations: CRUD, soft-delete (deactivation), delete protection |
| Employees | CRUD with job information (department, designation, joining date, employment type, salary, status), deactivation and reactivation, profile photo |
| Employee profile | Personal details (gender, marital status, blood group, nationality, NID, passport), addresses, emergency contacts, education, work experience, and a one-call profile view |
| Employee documents | Upload, list, view metadata, download, edit metadata and soft-delete HR documents (NID, passport, certificates, CV, ...) kept in private storage |
| Attendance (Day 13) | Check-in/check-out at office time, working-minute calculation, Present/Late/Absent/HalfDay/Leave status, HR-maintained records (CRUD), history and filtering — see §25 |
| Security (Day 16) | JWT sign-in with roles (Employee, Manager, HR, Admin), server-side authorization on every endpoint, audit log — see §17 and §26 |
| Leave (Day 16) | Apply, manager/HR review, cancel — see §26.3 |
| Payroll (Days 16–19) | Salary structures, payroll calculation and approval (§26), payslips and payroll history (§27), finalization and payment batches (§28), reporting, exports and the payroll audit trail (§29) |
| Platform | Standard response envelope, centralized error handling, validation, OpenAPI/Swagger, CORS for the frontend |

**Not implemented:** recruitment, performance, leave balances, and the items in §24.

*(Days 1–13 originally ran without authentication; that was added on Day 16.)*

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
| 13 | Employee attendance management: `Attendances` table, check-in/check-out, working minutes, status rules, CRUD, history and filters; office time zone centralized in `AttendanceClock` |
| 16 | Authentication and roles, manager relationship, leave, audit log, salary structures and payroll processing (§26) |
| 17 | Payslips issued at approval, payroll history, employee self-service payroll, provident fund (§27) |
| 18 | Payroll finalization and lock; payment batches, transactions and status history replace direct "mark paid" (§28) |
| 19 | Payroll reporting (period history, summary, departments, employees, deductions, allowances), CSV/Excel export, payroll details and payslip history filters, payroll audit trail (§29) |

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
    DependencyInjection.cs              AddApplication(documentOptions, attendanceOptions)
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
    Features/Attendances/               AttendanceService, AttendanceClock, AttendanceOptions, AttendanceFilter, Dtos/

  SmartHRMS.Domain/                     entities and enums (no dependencies)
    Common/BaseEntity.cs, Common/EmployeeOwnedEntity.cs
    Entities/                           Department, Designation, Employee, ApplicationUser, EmployeeDocument,
                                        EmployeePersonalDetails, EmployeeAddress, EmployeeEmergencyContact,
                                        EmployeeEducation, EmployeeExperience, Attendance
    Enums/                              EmployeeStatus, EmploymentType, Gender, MaritalStatus, BloodGroup,
                                        AddressType, EmployeeDocumentType, AttendanceStatus

  SmartHRMS.Infrastructure/             data access and storage
    DependencyInjection.cs              AddInfrastructure(connectionString, photoRoot, documentRoot)
    Persistence/SmartHRMSDbContext.cs
    Persistence/Configurations/         one IEntityTypeConfiguration<T> per entity
    Repositories/                       Employee, Department, Designation, EmployeeDocument, EmployeeOwned<T>, Attendance
    Storage/                            LocalFileStorageService, LocalDocumentStorageService, SafeStoragePath
    Migrations/                         8 migrations (§20)

  smartHRMS.Tests/                      xUnit unit tests with in-memory fakes (215 tests)
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
5. `AddApplication(documentOptions, attendanceOptions)`: document rules (`EmployeeDocuments` section) and attendance
   rules (`Attendance` section: time zone, workday start, grace minutes) are validated here, so the API **refuses to
   start** with an unusable configuration. Also registers `TimeProvider.System` and the singleton `AttendanceClock`.
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
  `EmployeeExperiences`, `Attendances` (+ `__EFMigrationsHistory`).
- **Primary keys:** every table has a `uniqueidentifier` `Id` generated by the application.

### Foreign keys (all `ON DELETE NO ACTION`)

| Column | References |
|---|---|
| `Employees.DepartmentId` | `Departments.Id` |
| `Employees.DesignationId` | `Designations.Id` |
| `ApplicationUsers.EmployeeId` | `Employees.Id` |
| `EmployeeDocuments.EmployeeId`, `EmployeePersonalDetails.EmployeeId`, `EmployeeAddresses.EmployeeId`, `EmployeeEmergencyContacts.EmployeeId`, `EmployeeEducations.EmployeeId`, `EmployeeExperiences.EmployeeId`, `Attendances.EmployeeId` | `Employees.Id` |

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
| `Attendances` | unique (`EmployeeId`, `AttendanceDate`); `AttendanceDate` |

Uniqueness comparisons use SQL Server's default case-insensitive collation.

### Soft delete

| Record | "Delete" means | Flag |
|---|---|---|
| Employee | `Status = Inactive` | `Status` (no separate `IsActive` column) |
| Department / Designation | deactivate | `IsActive = false` |
| Employee document | deactivate; row and file kept for HR history | `IsActive = false` |
| Profile records (personal details, addresses, contacts, education, experience) | permanent delete | none — hard delete |
| Attendance record | permanent delete (HR correction) | none — hard delete |

### Behavior built into the DbContext

- **Unique-index races → 409:** `SaveChangesAsync` converts SQL errors 2601/2627 into `ConflictException`. Verified in
  the audit with 10 parallel identical creates: 1 × `201`, 9 × `409`, exactly one row stored.
- **UTC timestamps:** a value converter marks `CreatedAt`/`UpdatedAt` as UTC when read, so they serialize with `Z`.
  Date-only values (`DateOfBirth`, `JoiningDate`, document dates, ...) are left unzoned. Attendance dates and times
  are office-local `date`/`time(0)` values (§25), never converted by the DbContext.

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

### Attendance (Day 13)
| Field | Rule |
|---|---|
| `EmployeeId` | required FK → `Employees` (NO ACTION); navigation `Employee`, and `Employee.Attendances` |
| `AttendanceDate` | required `date` (calendar date in the office time zone); unique together with `EmployeeId` |
| `CheckInTime`, `CheckOutTime` | optional `time(0)` (office wall-clock time, whole seconds); check-out never earlier than check-in |
| `WorkingMinutes` | optional `int`, **always calculated by the server** (check-out − check-in, whole minutes) |
| `Status` | `AttendanceStatus` stored as string (20): `Present`, `Late`, `Absent`, `HalfDay`, `Leave` |
| `Remarks` | optional, max 500 |

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
| `AttendanceDto` | attendance responses | `id`, `employeeId`, `employeeCode`, `employeeName`, `attendanceDate` ("yyyy-MM-dd"), `checkInTime`, `checkOutTime` ("HH:mm:ss"), `workingMinutes`, `status`, `remarks`, `createdAt`, `updatedAt` |
| `CreateAttendanceDto` | `POST /api/attendance` | `employeeId`*, `attendanceDate`*, `status`*, `checkInTime`, `checkOutTime`, `remarks` (500). No `workingMinutes` field — any sent value is ignored |
| `UpdateAttendanceDto` | `PUT /api/attendance/{id}` | `status`*, `checkInTime`, `checkOutTime`, `remarks` — full update; employee and date can't change |
| `CheckInDto` / `CheckOutDto` | check-in / check-out | `employeeId`*, `attendanceDate` (optional; must be today), `remarks` (500) |
| `AttendanceQueryDto` / `AttendanceListQueryDto` | query string | `date` or `startDate`/`endDate`, `status`; the list version adds `employeeId` |

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
| `AttendanceService` | `GetAllAsync(filters)`, `GetByEmployeeAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `CheckInAsync`, `CheckOutAsync` (§25) |

Helpers: `EmployeePhotoPolicy` (photo types/size), `EmployeeDocumentPolicy` (verifiable document formats, content
types, startup validation), `EmployeeStatusRules` (`Active` and `OnLeave` count as current), `FileSignature`,
`InputText`, and `AttendanceClock` (singleton: today's date and the current time in the office time zone, and the
Late rule — the only place attendance reads the clock).

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
| `AttendanceRepository` | `GetByIdAsync`, `GetByEmployeeAndDateAsync` (both with the employee), `SearchAsync(filter)` (newest date first, then employee code), `AddAsync`, `Remove`, `SaveChangesAsync` |

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
| `AttendanceController` | `/api/attendance` | `GET`, `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}`, `POST check-in`, `POST check-out`; plus `GET /api/employees/{employeeId}/attendance` |

The OpenAPI document lists **26 distinct route templates with no duplicates** (21 before Day 13 + 5 attendance; checked
at runtime). Every endpoint
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
| Dates | invalid JSON/form/query dates → `400`; employee DOB past, joining ≥ DOB; experience and education rules; document expiry ≥ issue; attendance rules in §25 | services |
| Times | `"HH:mm"` or `"HH:mm:ss"`; invalid (e.g. `25:00`) → `400`; attendance check-out ≥ check-in | JSON + `AttendanceService` |
| Enums | names only, case-insensitive; numbers/unknown → `400` | JSON options |
| Foreign keys | department/designation must exist and be active for new assignments | `EmployeeService` |
| Duplicates | employee code/email, department/designation names, NID/passport, address type, contact phone, education, attendance per employee and date, second check-in/check-out → `409` | services + unique indexes |
| Files | photo and document rules in §14/§15 | policies + services |
| IDs | route `{id:guid}` constraint: malformed id → `404`; unknown id → `404` | routing + services |
| Malformed body | invalid JSON → `400` "The request body is not valid JSON." or "The value for '<field>' is invalid." | `AddApiControllers` |

Validation always happens on the server; the frontend repeats the rules only for faster feedback.

---

## 17. Authentication & Authorization

**Implemented on Day 16** (details in §26.1). JWT bearer tokens issued by `POST /api/auth/login` for the existing
`ApplicationUsers` table, with four roles: **Employee, Manager, HR, Admin**.

- **Every endpoint requires a signed-in user** (fallback authorization policy) except `GET /api/health`,
  `POST /api/auth/login` and, in Development, `/openapi/v1.json` and `/swagger`.
- Role policies on controllers (`HrOrAdmin`, `Admin`), and **the services check roles and ownership again**, so a
  missing attribute cannot open data up. Request bodies can never change who the caller is (`ICurrentUser` reads the
  validated token only).
- Every request re-validates the token's account in the database: deactivating a user, changing their role or
  password ends their existing sessions immediately (security stamp).
- Passwords: PBKDF2-HMAC-SHA256, 210 000 iterations, per-password salt; 5 failed sign-ins lock the account for 15
  minutes; unknown user, wrong password and inactive account all get the same message.
- `401` and `403` use the standard envelope.

| Area | Employee | Manager | HR | Admin |
|---|---|---|---|---|
| Own profile, documents, attendance, leave, payslips | read | read | read/write | read/write |
| Direct reports: basic record (no salary), attendance, leave | — | read; approve/reject leave | all | all |
| Employees, departments, designations, documents, attendance (write) | — | — | ✓ | ✓ |
| Leave of anyone (approve/reject/cancel) | — | — | ✓ | ✓ |
| Salary structures, payroll periods, calculation, records, submit | — | — | ✓ | ✓ |
| Payment batches and payment history (view) | — | — | ✓ | ✓ |
| Payroll reports and report/payslip-list exports (Day 19) | own payslips only (export of own list) | own payslips only | ✓ | ✓ |
| Payroll audit trail (`/api/audit-logs?category=payroll`) | — | — | — | ✓ |
| Approve and finalize payroll; create, process, pay, fail, retry and cancel payments (never one's own salary) | — | — | — | ✓ |
| Users & roles, audit log | — | — | — | ✓ |

**CORS** still applies to browsers: `Cors:AllowedOrigins` in `appsettings.Development.json` lists
`http://localhost:5173` and `http://localhost:4173`; `appsettings.json` lists none.

**Configuration secrets** (never in `appsettings.json`): `Jwt:SigningKey` (≥ 32 characters; required outside
Development; in Development a temporary key is generated and sign-ins end on restart) and the one-time
`Auth:BootstrapAdmin:Username` / `:Password`. Use user-secrets (the API project has a `UserSecretsId`) or environment
variables (`Jwt__SigningKey`, `Auth__BootstrapAdmin__Username`, `Auth__BootstrapAdmin__Password`).

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

Applied migrations (all 11 applied; no pending model changes):

| # | Migration | Change |
|---|---|---|
| 1 | `InitialCreate` | Departments, Designations, Employees, ApplicationUsers, indexes, FKs |
| 2 | `UpdateEmployeeStatusToEnum` | `Employees.Status` as `nvarchar(20)`; redundant `IsActive` dropped |
| 3 | `AddEmployeePhotoUrl` | `Employees.PhotoUrl` |
| 4 | `AddEmployeeProfileFields` (Day 9) | `Address`, `Gender`, `EmploymentType` (default `FullTime`) |
| 5 | `AddEmployeeProfileAndDocuments` (Day 10) | salary, blood group, permanent address, emergency contact columns; `EmployeeDocuments` table |
| 6 | `AddEmployeeProfileRecords` (Day 11) | five profile tables; drops the flat address/gender/blood-group/emergency-contact columns (all empty when applied) |
| 7 | `AddEmployeeDocumentDetails` (Day 12) | `DocumentName` (backfilled from `FileName`), `IssueDate`, `ExpiryDate` |
| 8 | `AddAttendance` (Day 13) | creates `Attendances` (FK to `Employees`, NO ACTION), unique (`EmployeeId`, `AttendanceDate`), index on `AttendanceDate`. Purely additive; no existing table touched |
| 9 | `AddAuthLeaveAuditPayroll` (Day 16) | `ApplicationUsers`: `Role`, `SecurityStamp`, lockout and last-login columns, `EmployeeId` nullable with a filtered unique index, `PasswordHash` → `nvarchar(256)` (table was empty); `Employees.ManagerId` (self FK + check); new tables `LeaveRequests`, `AuditLogs`, `EmployeeSalaryStructures`, `PayrollPeriods`, `PayrollRecords` (see §26.5). Additive for every table that held data |
| 10 | `AddPayslipsAndProvidentFund` (Day 17) | new table `Payslips` (unique `PayslipNumber` and `PayrollRecordId`, FKs NO ACTION, check "paid ⇔ payment date"); `PayrollRecords.ProvidentFund` and `EmployeeSalaryStructures.MonthlyProvidentFund` (`decimal(18,2)`, default 0); the two non-negative checks re-created to include them. Purely additive (both payroll tables were empty) |
| 11 | `AddPayrollFinalizationAndPayments` (Day 18) | `PayrollPeriods`: `FinalizedAt`, `FinalizedByUserId` (FK), `IsLocked`; `Payslips`: `PaymentMethod`, `PaymentReference`; new tables `PaymentBatches`, `PaymentBatchItems`, `PaymentTransactions`, `PaymentStatusHistories` with filtered unique indexes against duplicate payment (see §28.15). Purely additive |

Day 19 needed no migration: reporting, exports and the audit trail read the existing payroll, payslip, payment and
`AuditLogs` tables.

Before each schema change since Day 9 a verified copy-only backup was taken
(`SmartHRMSDB_before_Day9/10/11/12/13/16/17/18_*.bak` in the SQL Server backup folder). Migrations are only generated after a
domain change; applied migrations are never edited.

> **Windows Smart App Control:** on this machine a freshly built, unsigned `smartHRMS.*.dll` can be blocked
> ("An Application Control policy has blocked this file") by `dotnet run` and `dotnet ef`. Windows remembers the
> verdict per file hash, and deterministic builds reproduce the same hash, so `backend/Directory.Build.props` turns
> deterministic builds off for non-Release builds: every rebuild now gets a new hash (about 3 in 4 fresh builds
> loaded when tested). Start the API with **`.\run-api.ps1`** (from `backend`), which runs `dotnet run` and, if the
> build is blocked, rebuilds (`dotnet build smartHRMS.Api --no-incremental`) and retries. Before this file existed,
> `dotnet run` silently rebuilt a `-p:Deterministic=false` build back to the blocked deterministic hash; on Day 13 the
> `smartHRMS.Api/bin/Debug/.../smartHRMS.Api.dll` stayed blocked through 10 rebuilds while an identical build
> in another folder (`dotnet build smartHRMS.Api -p:Deterministic=false -o <folder>`) ran fine. The migration was
> generated and applied from that build by running EF's `ef.dll` with `--startup-assembly <folder>\smartHRMS.Api.dll`
> and `-- --contentRoot <path to smartHRMS.Api>` (the same command `dotnet ef ... --verbose` prints).
> Also stop a running API before building, or its locked DLLs make the build fail.

---

## 21. API Endpoint Reference

Base URL (Development): `http://localhost:5099`. **Auth: `Authorization: Bearer <token>` on every endpoint except
health and login** (role rules in §17; a missing or expired token gives `401`, a forbidden action `403`). Common
errors on every endpoint with a body: `400` (validation), `500` (unexpected). `{id}` values are GUIDs. Since Day 16,
reads of one employee's data (`/api/employees/{id}`, profile, profile records, documents, attendance) are allowed for
the employee themself and HR/Admin (managers: basic record without salary, and attendance, of direct reports);
every write of the Day 1–13 modules is HR/Admin.

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
| GET | `/api/attendance[?employeeId&date&startDate&endDate&status]` | list / filter attendance | query | `AttendanceDto[]` (newest date first) | 200, 400 |
| GET | `/api/attendance/{id}` | one record | — | `AttendanceDto` | 200, 404 |
| POST | `/api/attendance` | manual record (HR) | `CreateAttendanceDto` | `AttendanceDto` | 201, 400, 404, 409 |
| PUT | `/api/attendance/{id}` | correct a record (HR) | `UpdateAttendanceDto` | `AttendanceDto` | 200, 400, 404 |
| DELETE | `/api/attendance/{id}` | delete (hard) | — | message | 200, 404 |
| POST | `/api/attendance/check-in` | check in now | `CheckInDto` | `AttendanceDto` | 200, 400, 404, 409 |
| POST | `/api/attendance/check-out` | check out now | `CheckOutDto` | `AttendanceDto` | 200, 400, 404, 409 |
| GET | `/api/employees/{employeeId}/attendance[?date&startDate&endDate&status]` | employee history | query | `AttendanceDto[]` | 200, 400, 404 |
| POST | `/api/auth/login` | sign in (anonymous) | `LoginDto` | `LoginResultDto` (token, expiry, user) | 200, 400, 401 |
| GET | `/api/auth/me` | signed-in user | — | `CurrentUserDto` | 200, 401 |
| POST | `/api/auth/change-password` | own password (ends sessions) | `ChangePasswordDto` | message | 200, 400 |
| GET/POST | `/api/users` | list / create accounts (Admin) | `CreateUserDto` | `UserDto` | 200, 201, 400, 409 |
| GET/PUT | `/api/users/{id}` | one account / role, active, employee (Admin) | `UpdateUserDto` | `UserDto` | 200, 400, 404, 409 |
| POST | `/api/users/{id}/reset-password` | reset + unlock (Admin) | `ResetPasswordDto` | message | 200, 400, 404 |
| PUT | `/api/employees/{id}/manager` | set/clear line manager (HR/Admin) | `AssignManagerDto` | `EmployeeDto` | 200, 400, 404 |
| GET | `/api/leave/types` | leave types and paid flag | — | `LeaveTypeDto[]` | 200 |
| GET | `/api/leave/requests[?scope&employeeId&status&leaveType&startDate&endDate]` | visible requests | query | `LeaveRequestDto[]` | 200, 400, 403 |
| GET | `/api/leave/requests/{id}` | one request | — | `LeaveRequestDto` | 200, 404 |
| POST | `/api/leave/requests` | apply | `CreateLeaveRequestDto` | `LeaveRequestDto` | 201, 400, 403, 404, 409 |
| POST | `/api/leave/requests/{id}/approve` \| `reject` \| `cancel` | decide | `ReviewLeaveRequestDto` (optional) | `LeaveRequestDto` | 200, 403, 404, 409 |
| GET | `/api/payroll/salary-structures` | all salaries (HR/Admin) | — | `SalaryStructureDto[]` | 200 |
| GET/PUT | `/api/employees/{employeeId}/salary-structure` | one salary (self or HR) / replace (HR/Admin) | `UpdateSalaryStructureDto` | `SalaryStructureDto` | 200, 400, 403, 404 |
| GET/POST | `/api/payroll/periods[?year&status]` | list / create period (HR/Admin) | `CreatePayrollPeriodDto` | `PayrollPeriodDto` | 200, 201, 400, 409 |
| GET/PUT/DELETE | `/api/payroll/periods/{id}` | one / edit / delete (Draft only) | `UpdatePayrollPeriodDto` | `PayrollPeriodDto` | 200, 400, 404, 409 |
| POST | `/api/payroll/calculate/{periodId}` | calculate or recalculate (HR/Admin) | — | `PayrollCalculationResultDto` | 200, 400, 404, 409 |
| GET | `/api/payroll/periods/{periodId}/records[?search&departmentId&designationId&status]` | records (HR/Admin) | query | `PayrollRecordDto[]` | 200, 400, 404 |
| GET/PUT | `/api/payroll/records/{id}` | one record (owner after approval, HR/Admin) / manual amounts (HR/Admin) | `UpdatePayrollRecordDto` | `PayrollRecordDto` | 200, 400, 404, 409 |
| POST | `/api/payroll/{periodId}/submit` | Calculated → PendingApproval (HR/Admin) | — | `PayrollPeriodDto` | 200, 400, 404, 409 |
| POST | `/api/payroll/{periodId}/approve` | → Approved (Admin) | — | `PayrollPeriodDto` | 200, 403, 404, 409 |
| POST | `/api/payroll/{periodId}/finalize` | Approved → Finalized and locked (Admin; Day 18 — replaces `mark-paid`) | — | `PayrollPeriodDto` | 200, 400, 403, 404, 409 |
| POST | `/api/payroll/{periodId}/cancel` | → Cancelled (HR/Admin) | `CancelPayrollDto` (optional) | `PayrollPeriodDto` | 200, 404, 409 |
| GET | `/api/payroll/employee/{employeeId}` | payroll history (own: Approved/Paid only) | — | `PayrollRecordDto[]` | 200, 403, 404 |
| GET | `/api/payroll/payslip/{recordId}` | payslip (own after approval, HR/Admin) | — | `PayslipDto` | 200, 404 |
| GET | `/api/audit-logs[?category&entityType&entityId&action&username&from&to&take]` | audit log (Admin); `category=payroll` = payroll trail (Day 19) | query | `AuditLogDto[]` | 200, 400 |
| GET | `/api/payroll/history[?employeeId&departmentId&designationId&payrollPeriodId&recordStatus&month&year&status&paymentStatus&search&page&pageSize&sortBy&sortDirection]` | payroll history, every record (HR/Admin); with `payrollPeriodId` = payroll details (Day 19) | query | `PagedResult<PayrollRecordDto>` | 200, 400 |
| GET | `/api/payroll/payslips[?same filters]` | issued payslips (HR/Admin) | query | `PagedResult<PayrollRecordDto>` | 200, 400 |
| GET | `/api/payroll/payslips/export[?same filters&format]` | issued payslips as CSV or XLSX (HR/Admin; Day 19) | query | file | 200, 400, 403 |
| GET | `/api/payroll/me/payslips/export[?format]` | own payslips as CSV or XLSX (employee from the token; Day 19) | query | file | 200, 400 |
| GET | `/api/payroll/reports/periods[?search&month&year&status&from&to&page&pageSize]` | payroll history by period with totals (HR/Admin; Day 19) | query | `PagedResult<PayrollPeriodHistoryDto>` | 200, 400 |
| GET | `/api/payroll/reports/summary`, `/departments`, `/deductions`, `/allowances` `[?month&year&payrollPeriodId&employeeId&departmentId&designationId&status&from&to&groupBy]` | grouped reports (HR/Admin; Day 19) | query | `PayrollReportDto` | 200, 400 |
| GET | `/api/payroll/reports/employees[?same filters&search&page&pageSize]` | employee payroll report (HR/Admin; Day 19) | query | `PagedResult<PayrollRecordDto>` | 200, 400 |
| GET | `/api/payroll/reports/{report}/export[?same filters&format]` | report as CSV or XLSX: periods, summary, departments, employees, deductions, allowances (HR/Admin; Day 19) | query | file | 200, 400, 404 |
| GET | `/api/payroll/payslips/{id}` | one payslip (owner or HR/Admin) | — | `PayslipDto` | 200, 404 |
| POST | `/api/payroll/{periodId}/payslips` | issue missing payslips of approved payroll (HR/Admin) | — | `PayslipGenerationResultDto` | 200, 404, 409 |
| GET | `/api/payroll/employee/{employeeId}/payslips[?filters]` | one employee's payslips (self or HR/Admin) | query | `PagedResult<PayrollRecordDto>` | 200, 400, 403, 404 |
| GET | `/api/payroll/me/payslips[?filters]` | own payslips (employee from the token) | query | `PagedResult<PayrollRecordDto>` | 200, 400 |
| GET | `/api/payroll/me/payslips/current` | own latest payslip | — | `PayslipDto` | 200, 404 |
| GET | `/api/payroll/me/payments[?page&pageSize]` | own paid payslips | query | `PagedResult<PayrollRecordDto>` | 200, 400 |
| POST | `/api/payments/batches` | create a batch for a finalized payroll (Admin) | `CreatePaymentBatchDto` | `PaymentBatchDto` | 201, 400, 403, 404, 409 |
| GET | `/api/payments/batches[?payrollPeriodId&status&page&pageSize]` | batches (HR/Admin) | query | `PagedResult<PaymentBatchDto>` | 200, 400 |
| GET | `/api/payments/batches/{id}` | one batch (HR/Admin) | — | `PaymentBatchDto` | 200, 404 |
| POST | `/api/payments/batches/{id}/process` | Pending → Processing for the batch (Admin) | — | `PaymentBatchDto` | 200, 403, 404, 409 |
| POST | `/api/payments/batches/{id}/cancel` | cancel while all payments are Pending (Admin) | `PaymentReasonDto` (reason required) | `PaymentBatchDto` | 200, 400, 403, 404, 409 |
| GET | `/api/payments[?batchId&employeeId&payrollPeriodId&status&paymentMethod&from&to&search&page&pageSize]` | payment history (HR/Admin) | query | `PagedResult<PaymentDto>` | 200, 400 |
| GET | `/api/payments/me[?same filters]` | own payments (employee from the token) | query | `PagedResult<PaymentDto>` | 200, 400 |
| GET | `/api/payments/{id}` | one payment with status history (owner or HR/Admin) | — | `PaymentDto` | 200, 404 |
| POST | `/api/payments/{id}/paid` | Processing → Paid (Admin) | `MarkPaymentPaidDto` (optional) | `PaymentDto` | 200, 400, 403, 404, 409 |
| POST | `/api/payments/{id}/failed` | Processing → Failed (Admin) | `PaymentReasonDto` (reason required) | `PaymentDto` | 200, 400, 403, 404, 409 |
| POST | `/api/payments/{id}/retry` | Failed → Processing (Admin) | `PaymentReasonDto` (optional) | `PaymentDto` | 200, 403, 404, 409 |
| POST | `/api/payments/{id}/cancel` | Pending → Cancelled (Admin) | `PaymentReasonDto` (reason required) | `PaymentDto` | 200, 400, 403, 404, 409 |

Development-only: `GET /openapi/v1.json` (OpenAPI document) and `/swagger` (Swagger UI).

---

## 22. Testing

```bash
cd backend
dotnet restore
dotnet build smartHRMS.slnx          # if Smart App Control blocks the DLL: add --no-incremental (new hash, see §20)
dotnet test smartHRMS.slnx
dotnet ef migrations has-pending-model-changes --project SmartHRMS.Infrastructure --startup-project smartHRMS.Api
```

| Kind | What | Result (latest run: Day 19, 2026-10-08) |
|---|---|---|
| Build | `dotnet build` | 0 warnings, 0 errors |
| Unit tests | 371 xUnit tests (services, DTO validation, envelope, photo, documents, profile records, attendance; Day 16: payroll calculator, payroll workflow and access, leave, sign-in, users, managers; Day 17: payslip issuing, isolation, history filters and paging, provident fund; Day 18: finalization and lock, payment batches, transitions, duplicates, authorization, employee isolation; Day 19: reports, exports, audit trail) with in-memory fakes and a fake `TimeProvider` | 371 / 371 passed |
| Runtime API (Day 19) | period history, payroll details, payslip history and access, reports (totals compared with SQL), exports, every role + unauthenticated, payroll audit trail | 274 / 274 |
| Browser (Day 19 frontend) | payroll periods history, payroll details, payslips, reports dashboard and tabs, CSV/Excel downloads, audit trail, role visibility, mobile/tablet | 14 / 14, no JS exceptions or console errors |
| Runtime API (Day 18) | finalization rules and lock, batch creation and totals, concurrent duplicate batches, processing, paid/failed/retry/cancel, invalid transitions, own-salary rule, every role + unauthenticated, employee isolation, history filters, DB constraints, audit (no amounts) | 250 / 250 |
| Browser (Day 18 frontend) | finalize dialog and lock, create batch, batch list/details (search, filters, paging), processing, paid/failed/retry with history, payment history filters, employee payment view, role visibility, mobile/tablet | 20 / 20, no JS exceptions or console errors |
| Migrations (Day 18) | `has-pending-model-changes`, filtered indexes queried in SQL Server | 11 / 11 applied, no drift |
| Runtime API (Day 17) | payslip issuing at approval, payslip content, employee isolation, 401/403, history filters, sorting and paging, invalid input, payments, DB constraints, audit | 184 / 184 |
| Browser (Day 17 frontend) | payroll history, payslip list and details, payment recording, My Payroll tabs, print, isolation, empty states, mobile/tablet | 13 / 13 (one expected count corrected after checking the database), no JS exceptions or console errors |
| Migrations | `migrations list`, live schema query | 10 / 10 applied, schema matches configuration |
| Runtime API (Day 16) | sign-in, lockout, sessions, roles on every module, users, managers, leave, salary, payroll workflow, payslips, audit, DB constraints | 249 / 249 |
| Browser (Day 16 frontend) | sign-in, role guards, leave, salary, payroll workflow, payslip print, session end, mobile/tablet layout | 28 / 28, no JS exceptions or console errors |
| Day 1–13 regression with authentication on | the suites below re-run as Admin against the Day 16 build | all passed except the OpenAPI route count (53 routes now, 0 duplicates; the check expected 26) |
| Runtime API (Day 13) | attendance check-in/out, CRUD, filters, history, validation, DB constraints, parallel check-ins, Swagger | 137 / 137 |
| Runtime API (Day 1–10 regression) | CRUD, relationships, photo, documents, envelope, errors | 193 / 193 |
| Runtime API (Day 11) | personal details, addresses, contacts, education, experience, profile | 167 / 167 |
| Runtime API (Day 12) | document upload/list/get/download/update/soft delete, validation, cross-employee access, storage checks | 108 / 108 |
| Runtime API (audit gaps) | Swagger/OpenAPI (26 routes), 404/405/415, negative inputs, photo DB+file consistency, document soft-delete on disk | 57 / 57 |
| Browser (React frontend) | every page and flow, simulated 401/403/500/502/network errors, mobile/tablet layouts | 59 / 59, no JS errors |
| Concurrency | 10 parallel identical creates | 1 × 201, 9 × 409, one row |
| Cancellation | client disconnect during a blocked update | no error logged, update not applied |
| Data integrity | orphans, invalid enums, current employees in inactive departments, photo files vs database | none found |

The runtime suites are shell/Node scripts kept outside the repository and run against a local instance with
`TEST-*` records only, which are removed afterwards; a checksum of all real data was identical before and after.
The repository itself contains only unit tests (see §24).

---

## 23. Day 1–19 Completion Matrix

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
| 13 | Attendances table, FK, unique (employee, date), migration | PASS | schema queried in SQL Server; duplicate and orphan inserts rejected by the database |
| 13 | Check-in (office time, Present/Late rule, duplicate → 409) | PASS | Day 13 suite + unit tests |
| 13 | Check-out (working minutes, no check-in → 400, duplicate → 409, earlier than check-in → 400) | PASS | Day 13 suite + unit tests |
| 13 | Manual create / update / delete (HR) | PASS | Day 13 suite |
| 13 | Employee validation (exists, current status) | PASS | 404 unknown, 400 inactive |
| 13 | Filters (employee, date, range, status) and employee history | PASS | Day 13 suite |
| 13 | Time zone handling (office date differs from UTC date) | PASS | live run at 21:32 UTC recorded the next Dhaka date; unit test for the same case |
| 13 | Parallel check-ins | PASS | 5 parallel → 1 × 200, 4 × 409, one row |
| 13 | Authentication / authorization for attendance | PASS (Day 16) | HR/Admin for list and CRUD; own or HR for check-in/out; own, manager or HR for history (Day 16 suite) |
| 16 | Authentication: login, JWT, lockout, session revocation, bootstrap Admin | PASS | Day 16 suite + unit tests |
| 16 | Roles and server-side authorization on every module | PASS | Day 16 suite (403/404 for every forbidden case) |
| 16 | Employee–manager relationship (no self, no loops) | PASS | Day 16 suite + unit tests |
| 16 | Leave: apply, overlap, working days, manager/HR review, no self-approval, cancel, payroll lock | PASS | Day 16 suite + unit tests |
| 16 | Audit log (sign-in, users, leave, salary change, payroll actions, payslip views; no amounts) | PASS | Day 16 suite |
| 16 | Salary structure (basic + allowances + tax) | PASS | Day 16 suite |
| 16 | Payroll periods (no overlap, no duplicate, ≤ 31 days, edit/delete Draft only) | PASS | Day 16 suite + unit tests |
| 16 | Payroll calculation (formulas, proration, approved unpaid leave only, attendance read-only, transactional) | PASS | Day 16 suite + unit tests |
| 16 | Workflow Draft → Calculated → PendingApproval → Approved → Paid; Cancelled | PASS | Day 16 suite + unit tests |
| 16 | Approved/Paid payroll locked; Admin approves; nobody approves own salary | PASS | Day 16 suite + unit tests |
| 16 | Payslip and employee history (own, after approval only) | PASS | Day 16 suite + browser suite |
| 16 | DB constraints: unique (period, employee), non-negative amounts, filtered unique period dates, rowversion | PASS | direct SQL insert/update refused in the Day 16 suite |
| 17 | Payslips issued only at approval (never for Draft/Calculated/PendingApproval/Cancelled) | PASS | Day 17 suite + unit tests |
| 17 | Payslip = approved snapshot (locked record amounts, no recalculation), provident fund | PASS | Day 17 suite + unit tests |
| 17 | Payment per payslip and per period; payment date validated; period Paid when all are paid | PASS | Day 17 suite + unit tests |
| 17 | Payroll history / payslip list: filters, sorting, server paging | PASS | Day 17 suite + browser suite |
| 17 | Employee My Payroll (current, history, payments) from the token; isolation; manager no access | PASS | Day 17 suite + browser suite |
| 17 | 401 without token, 403 for wrong role, 404 for other employees' payslips | PASS | Day 17 suite |
| 17 | Print-ready payslip page (`?print=1`) | PASS | browser suite (print() called once, sidebar hidden in print media) |
| 17 | Payment per payslip / per period (`mark-paid`) | REPLACED (Day 18) | removed; salaries are paid through payment batches (§28) |
| 18 | Finalization (Approved → Finalized, Admin only, validation) and lock | PASS | Day 18 suite + unit tests |
| 18 | Locked payroll can't be edited, recalculated, cancelled or deleted | PASS | Day 18 suite (409 "Payroll is locked and cannot be modified.") |
| 18 | Payment batches: unique number, server totals, items, transactions, history | PASS | Day 18 suite + unit tests |
| 18 | Payment transitions and invalid-transition rejection | PASS | Day 18 suite + unit tests |
| 18 | Duplicate-payment protection (application + filtered unique indexes; 5 concurrent creates → 1 × 201) | PASS | Day 18 suite |
| 18 | Payroll becomes Paid when every payable payslip is paid | PASS | Day 18 suite + unit tests |
| 18 | Authorization per role, unauthenticated 401, Admin can't pay own salary | PASS | Day 18 suite + unit tests |
| 18 | Employee payment view (own only) | PASS | Day 18 suite + browser suite |
| 18 | Frontend: finalize dialog, batches, batch details, payment history, My Payroll payments | PASS | browser suite (20 / 20) |
| 19 | Payroll history by period (totals, processed/finalized dates, filters, paging) | PASS | Day 19 suite + unit tests |
| 19 | Payroll details (paged records, breakdown, filters, sort; finalized read-only) | PASS | Day 19 suites |
| 19 | Payslip history (period filter, generated date) and payslip access rules | PASS | Day 19 suites |
| 19 | Reports: summary/salary expense, departments, employees, deductions, allowances/bonus; totals equal SQL sums | PASS | Day 19 suite + unit tests |
| 19 | CSV/XLSX export of reports and payslips, authorization and audit | PASS | Day 19 suites + unit tests |
| 19 | Payroll audit trail (category, user, date filters; status transitions; no amounts) | PASS | Day 19 suites + unit tests |

---

## 24. Known Limitations

| Severity | Limitation | Impact |
|---|---|---|
| Medium | No refresh tokens | Access tokens last 60 minutes (configurable); users sign in again afterwards. Tokens live in the browser's sessionStorage |
| Medium | Profile photos are public static files | `/uploads/...` is served without a token (an `<img>` can't send one); photo URLs are unguessable but not protected |
| Medium | No leave balances or entitlements | Any number of paid leave days can be requested; HR/managers decide. No carry-over or accrual |
| Medium | Weekends only, no public holidays | `WorkCalendar:WeekendDays` (default Friday + Saturday) decides working days for leave and payroll; holidays count as working days |
| Medium | Payroll: fixed monthly tax, no tax slabs; no overtime module | Tax is a monthly amount on the salary structure; overtime, bonus, advance and loan are entered per record by HR |
| Medium | Payroll: leavers are not paid in a final settlement | Only Active/OnLeave employees are calculated; an employee who left mid-month needs a manual adjustment |
| Low | Payroll approval needs a second Admin when the only Admin is also an employee in the payroll | The self-approval rule refuses approval of a payroll that contains your own salary; likewise an Admin can't change the status of their own salary payment |
| Medium | Payments are recorded, not executed | No bank, mobile-banking or payment-gateway integration: an Admin records the outcome of each transfer (Day 18 scope) |
| Low | No payment reversal | A Paid payment is final; correcting it needs a future reversal workflow |
| Low | Finalized payroll has no unlock | A finalized payroll can only be paid; corrections need a future correction workflow |
| Low | Absences are not deducted by default | `Payroll:DeductRecordedAbsences` turns on deduction of explicit Absent days; days without any attendance record are never deducted |
| Medium | Pagination only on payroll history, payslip, payment and report lists | Other lists return every matching row. Attendance grows by one row per employee per day, so clients should always pass a date or range |
| Low | Attendance: no overnight shifts | Check-in/out apply to one office date; a shift past midnight must be corrected by HR |
| Low | Attendance: no weekends, holidays or leave integration | Absent/Leave are recorded explicitly; nothing is generated automatically for missing days |
| Low | Attendance: Late rule only | Any check-in up to workday start + grace is Present, including very early ones; half days are not detected automatically |
| Low | Attendance deletes are permanent | No history of corrections is kept (only `updatedAt`) |
| Low | Unit tests only in the repository | EF queries and the HTTP pipeline are covered by external runtime scripts, not by automated tests in the solution |
| Low | EF Core logs refused duplicate inserts at error level | Only during genuine concurrent duplicates; clients correctly get 409 |
| Low | Photo limits are constants | `EmployeePhotoPolicy` (5 MB; jpg/png/webp) is not configurable, unlike documents |
| Low | Photo request over 6 MB | Returns 400 with the framework's "request body too large" message rather than the photo-specific one |
| Low | Removed employee JSON fields are ignored silently | Old clients sending `address`, `gender`, ... get no error |
| Low | Department and Designation code is duplicated | Two parallel copies of service/repository/controller |
| Low | Redundant EF package references in `smartHRMS.Api.csproj` | `SqlServer`/`Tools` also come through Infrastructure (`Design` is needed) |
| Low | xUnit 2 is marked legacy | Tests work; migration to xUnit v3 is optional |
| Info | Smart App Control on the development machine | May block freshly built DLLs, sometimes persistently (see §20) |

**Not implemented (future work):** refresh tokens and single sign-on; leave balances and holiday calendar; tax
slabs, overtime tracking and payroll line-item tables (allowances/deductions beyond the fixed columns); PDF payslips
(the payslip page is print-ready instead); recruitment and performance modules; cloud storage for photos and
documents (the storage interfaces allow adding it without changing the Application layer).

---

## 25. Day 13 — Attendance Management

**Module:** Employee Attendance Management (`Features/Attendances`, `AttendanceController`, `Attendances` table).

**Features:** attendance CRUD (HR), self check-in and check-out, working-time calculation, attendance status, employee
attendance history, date / date-range / status / employee filtering, employee validation, duplicate prevention.
Authorization is **not** implemented (the project has no authentication).

### Database

```text
Employees 1 ──── * Attendances
             EmployeeId (FK, NO ACTION)
             unique (EmployeeId, AttendanceDate)
```

`Attendances`: `Id`, `EmployeeId`, `AttendanceDate` (`date`), `CheckInTime`/`CheckOutTime` (`time(0)`),
`WorkingMinutes` (`int`), `Status` (`nvarchar(20)`), `Remarks` (`nvarchar(500)`), `CreatedAt`, `UpdatedAt`.
Migration `AddAttendance`. The database itself rejects a second record for the same employee and date and a record
for an unknown employee, and refuses to delete an employee who has attendance.

### Date and time handling

- Configuration (`appsettings.json`, section `Attendance`): `TimeZone` = `Asia/Dhaka`, `WorkdayStartTime` =
  `"09:00"`, `LateGraceMinutes` = `15`. Invalid values stop the API at startup.
- `AttendanceClock` is the only place attendance reads the clock: it converts the UTC instant from `TimeProvider`
  into the office time zone. The server's own time zone is never used.
- Attendance dates are office calendar dates and times are office wall-clock times, stored and returned without
  offsets (`"2026-09-30"`, `"09:05:00"`), precise to the second. Example verified live: at 21:32 UTC on 29 Sep, a
  check-in was recorded on **30 Sep at 03:32:59** (Dhaka).

### Business rules

| Rule | Behavior |
|---|---|
| One record per employee per date | second create or check-in → `409` (also enforced by the unique index, including simultaneous requests) |
| Employee must exist | `404` |
| Employee must be current (`Active` or `OnLeave`) for create and check-in | otherwise `400`. Updates of existing records remain possible |
| Check-in / check-out time | always the server's current office time; `attendanceDate` in the body is optional and must be today (`400` otherwise) — no back-dating |
| Status on check-in | `Late` if later than workday start + grace (09:15 by default), otherwise `Present`. Checking in on a pre-created `Absent` record fills it in; a `HalfDay` stays `HalfDay`; a `Leave` day → `409` |
| Check-out | needs a check-in today (`400`), only once (`409`), not earlier than the check-in (`400`) |
| Working minutes | `checkOutTime − checkInTime` in whole minutes, calculated by the server on check-out, create and update; `null` until both times exist; clients can't set it |
| Manual create (HR) | `status` required; not before the joining date; future dates only for `Leave`; `Absent`/`Leave` have no times; check-out needs check-in and ≥ check-in; today's times not in the future |
| Update (HR) | full update of status, times and remarks with the same rules; employee and date can't change |
| Delete (HR) | permanent |
| Filters | `employeeId`, `date` **or** `startDate`/`endDate` (not both; start ≤ end), `status` by name (case-insensitive; numbers rejected) |

### API

See §21 for the full table. Frontend-ready: every response is the standard envelope with `AttendanceDto` items that
already contain `employeeCode` and `employeeName`, so a dashboard ("today's attendance", table, employee/date/status
filters, check-in/out buttons, details) needs no extra calls.

### Day 13 test checklist (executed on 2026-09-30)

| Area | Test | Result |
|---|---|---|
| Employee validation | existing / non-existing / inactive employee | PASS |
| Creation | valid, duplicate, invalid employee, invalid date, invalid status (name and number), missing fields, future date, before joining date, times on Absent/Leave | PASS |
| Check-in | valid (office date/time, status rule), duplicate, invalid employee, inactive employee, wrong date, Leave day, parallel requests | PASS |
| Check-out | valid, without attendance, without check-in, duplicate, earlier than check-in (unit test), working-minute calculation (510 min for 09:00–17:30, and live check) | PASS |
| Retrieval | by id, unknown id, employee history, by date, by status, by date range, invalid filters | PASS |
| Update / delete | valid update with recalculation, invalid times, unknown record, delete, delete unknown | PASS |
| Database | table, FK, unique constraint, NO ACTION delete, migration applied, no model drift | PASS |
| Swagger | attendance routes and schemas present | PASS |
| Authentication / authorization | — | NOT TESTED (not implemented) |

Results: 46 new unit tests (215 / 215 total) and 137 / 137 live API checks. The Day 1–12 regression suites (193 +
167 + 108 + 57 API checks and 59 browser checks) all passed against the Day 13 build.

---

## 26. Day 16 — Payroll Processing & Salary Management

### 26.0 Module purpose and what had to be built first

Payroll turns each employee's salary structure, attendance and approved leave into a monthly payroll record, takes it
through review and approval, and gives the employee a payslip. The Day 15 and Day 16 payroll prompts both assumed
authentication, roles, a Leave module (Day 14) and an audit log; **none of them existed** (Day 14's backend was never
built, and `ApplicationUser` was an unused table). With the owner's approval they were built first, as the minimum
payroll needs, inside the existing architecture: §26.1–§26.4. Day 15 and Day 16 payroll are delivered together as one
module (§26.5 onward); where the two prompts differed, Day 16 was followed (period name and date range, statuses
`Draft → Calculated → PendingApproval → Approved → Paid`, Admin approval).

### 26.1 Authentication and roles

- `ApplicationUser` (existing table) gained `Role` (Employee, Manager, HR, Admin), `SecurityStamp`,
  `FailedLoginCount`, `LockoutEndAt` and `LastLoginAt`; `EmployeeId` became optional (an Admin may have no employee).
- `AuthService`: login (same message for unknown user, wrong password and inactive account; lockout after
  `Auth:LockoutThreshold` = 5 failures for `Auth:LockoutMinutes` = 15), current user, change password.
- `UserService` (Admin only): create, update role/active/employee, reset password. Employee and Manager accounts must
  be linked to an employee; one account per employee; the last active Admin can't be demoted or deactivated; Admins
  can't demote or deactivate themselves. A role, active-flag or password change ends existing sessions.
- First Admin: on startup, if no Admin exists and `Auth:BootstrapAdmin:Username`/`:Password` are configured
  (user-secrets or environment variables), the account is created and a warning is logged. Remove the setting
  afterwards.
- API: `smartHRMS.Api/Auth` (`AuthenticationSetup`, `JwtTokenService`, `HttpCurrentUser`, `Policies`). Application:
  `ICurrentUser` + `CurrentUserExtensions` (`EnsureHrOrAdmin`, `EnsureAdmin`, `EnsureSelfOrHrOrAdmin`) and
  `IEmployeeAccess` for the existing modules.
- Permission matrix: §17.

### 26.2 Employee–manager relationship

`Employees.ManagerId` is an optional self reference (NO ACTION, check `ManagerId <> Id`). It is set with
`PUT /api/employees/{id}/manager` (HR/Admin). Rules: the manager must exist and be current; no self; no loops
(A → B → A). A manager reviews the leave of direct reports and sees their basic record (without salary) and
attendance, nothing else.

### 26.3 Leave (the minimum Day 14 functionality payroll needs)

`LeaveRequest`: employee, `LeaveType` (Annual, Sick, Casual — paid; Unpaid), start/end date, `TotalDays` (working
days, calculated by the server), reason, `LeaveStatus` (Pending, Approved, Rejected, Cancelled), requested/reviewed by,
review comment. Rules:

- whole working days; at most 90 calendar days; not before joining;
- no overlap with the employee's Pending/Approved leave (409);
- Employees and Managers apply only for themselves; HR/Admin for anyone;
- the direct manager, HR or Admin approve/reject Pending requests, but **nobody reviews their own**;
- the owner cancels while Pending; HR/Admin also cancel Approved leave;
- leave can't be approved or cancelled once payroll covering those dates is PendingApproval, Approved or Paid;
- requests outside the caller's visibility are reported as 404.

### 26.4 Audit log

`AuditLogs` is append-only, with no FK, so failed sign-ins of unknown usernames are kept. `IAuditLogger.Add` stages an
entry in the same `SaveChanges` as the change it describes. Logged: sign-in success/failure, password change/reset,
user create/update, manager assignment, salary structure change (**without amounts**), leave
requested/approved/rejected/cancelled, payroll period created/updated/deleted, calculated, record updated, submitted,
approved, paid, cancelled, and payslip viewed. Read with `GET /api/audit-logs` (Admin).

### 26.5 Database changes (migration `AddAuthLeaveAuditPayroll`)

| Table | Key columns | Constraints and indexes |
|---|---|---|
| `EmployeeSalaryStructures` | `EmployeeId`, `HouseRent`, `MedicalAllowance`, `TransportAllowance`, `OtherAllowance`, `MonthlyTax` — `decimal(18,2)` | unique `EmployeeId`; FK → Employees (NO ACTION); check all ≥ 0 |
| `PayrollPeriods` | `Name`, `StartDate`, `EndDate` (`date`), `Status`, `Notes`, `CreatedBy/CalculatedBy/SubmittedBy/ApprovedBy/PaidBy/CancelledByUserId` with matching `…At`, `RowVersion` | check `StartDate < EndDate`; **unique (`StartDate`, `EndDate`) where `Status <> 'Cancelled'`**; index `Status`; FKs → ApplicationUsers (NO ACTION); rowversion for concurrent status changes (409) |
| `PayrollRecords` | `PayrollPeriodId`, `EmployeeId`, snapshot `EmployeeCode/Name/DepartmentName/DesignationName`; 15 money columns `decimal(18,2)`; 5 day columns `decimal(5,1)`; `Status`, `Remarks` | **unique (`PayrollPeriodId`, `EmployeeId`)**; check all earnings and deductions ≥ 0; FKs → PayrollPeriods, Employees (NO ACTION); index `EmployeeId` |
| `LeaveRequests` | see §26.3 | check `StartDate <= EndDate`; indexes (`EmployeeId`, `StartDate`) and `Status`; FKs → Employees, ApplicationUsers |
| `AuditLogs` | `UserId`, `Username`, `Action`, `EntityType`, `EntityId`, `Details`, `CreatedAt` | indexes `CreatedAt` and (`EntityType`, `EntityId`) |

- The basic salary stays on `Employees.BasicSalary`, so it is stored in one place; the salary structure holds
  allowances and tax.
- Records copy the employee's name, code, department and designation, so an issued payslip never changes later.
- Before the migration a verified copy-only backup was taken (`SmartHRMSDB_before_Day16_*.bak`).
- Monetary values are always `decimal`, rounded to 2 places, halves away from zero (`Money.Round`).

### 26.6 Relationships

- Employee 1–0..1 SalaryStructure
- Employee 1–n LeaveRequest
- Employee 0..1–n Employee (manager)
- PayrollPeriod 1–n PayrollRecord
- Employee 1–n PayrollRecord (one per period)
- ApplicationUser 0..1–1 Employee
- ApplicationUser 1–n PayrollPeriod and LeaveRequest actions (by id)

### 26.7 APIs and DTOs

The endpoints are listed in §21 (the rows from `/api/auth/login` down).

Route mapping to the Day 16 prompt: the suggested `GET /api/payroll/records/{periodId}` became
`GET /api/payroll/periods/{periodId}/records`, because `/api/payroll/records/{id}` is the single-record route. The
nested form follows the existing `/api/employees/{id}/attendance` convention. All other suggested routes are used as
given.

DTOs (`Features/Payroll/Dtos`):

- `PayrollPeriodDto`: totals, who did each step and when, and `actions` (what the caller may do next);
- `PayrollRecordDto`;
- `PayrollCalculationResultDto`: created/updated/removed/skipped, with reasons;
- `PayslipDto`, `SalaryStructureDto`;
- `Create/UpdatePayrollPeriodDto`, `UpdatePayrollRecordDto` (manual amounts only), `UpdateSalaryStructureDto`,
  `CancelPayrollDto`, and the query DTOs.

No entity is returned by any endpoint.

### 26.8 Payroll calculation (`PayrollCalculator`, pure and unit-tested)

```
GrossSalary    = Basic + HouseRent + Medical + Transport + OtherAllowance + Overtime + Bonus
TotalDeduction = Tax + LeaveDeduction + Advance + Loan + OtherDeduction
NetSalary      = GrossSalary − TotalDeduction
```

- **Working days**: the days of the period that are not weekly days off (`WorkCalendar:WeekendDays`, default Friday
  and Saturday).
- **Proration**: an employee who joined during the period gets `employed working days ÷ period working days` of basic,
  allowances and tax.
- **Leave deduction**: `monthly basic ÷ period working days × approved unpaid-leave days`, never more than the prorated
  basic.
  - Paid leave (Annual, Sick, Casual) is not deducted.
  - Pending, Rejected and Cancelled leave have no effect.
  - With `Payroll:DeductRecordedAbsences = true`, days with an explicit Absent record (and half of a HalfDay) are
    deducted the same way. Days with no attendance record are never deducted.
- **Day counts**, per working day: approved leave comes first (paid/unpaid); otherwise the attendance record decides.
  Present/Late = present; HalfDay = ½ present + ½ absent; Absent = absent; Leave (recorded by HR) = paid leave.
  Attendance and leave are only read, never changed.
- Overtime, bonus, advance, loan and other deductions are entered by HR per record and **kept on recalculation**.
- A negative net salary marks the record **NeedsReview**, which blocks submission.
- Employees with no basic salary are **skipped and reported** (never paid zero silently). Inactive, resigned and
  terminated employees, and those joining after the period, are not included.

### 26.9 Business rules and validation

| Rule | Where | Result |
|---|---|---|
| Start before end; ≤ `Payroll:MaxPeriodDays` (31); years 2000–2100; ≥ 1 working day | service (+ DB check) | 400 |
| Overlap with a period that isn't Cancelled; duplicate dates | service + filtered unique index | 409 |
| One record per employee and period | service (upsert) + unique index | never duplicated; 409 on a race |
| Negative salary, allowance, tax or manual amount | DTO validation + DB check | 400 |
| Invalid status/filters; numbers for enums | service | 400 |
| Edit/delete a period only while Draft; edit records or recalculate only while Draft/Calculated | service | 409 |
| Submit only Calculated, with ≥ 1 record and none NeedsReview | service | 409 / 400 |
| Approve only PendingApproval; mark paid only Approved; cancel only Draft/Calculated/PendingApproval | service | 409 |
| Approver's own salary is in the payroll | service | 403 |
| Concurrent status changes | rowversion | 409 |

### 26.10 Role / permission matrix (payroll)

| | Employee | Manager | HR | Admin |
|---|---|---|---|---|
| View periods and records | — | — | ✓ | ✓ |
| Create/edit/delete (Draft) period | — | — | ✓ | ✓ |
| Calculate / recalculate | — | — | ✓ | ✓ |
| Edit record (manual amounts) | — | — | ✓ | ✓ |
| Submit for approval | — | — | ✓ | ✓ |
| Approve | — | — | — | ✓ (not own salary) |
| Mark paid | — | — | — | ✓ |
| Cancel | — | — | ✓ | ✓ |
| Salary structures | own (read) | own (read) | ✓ | ✓ |
| Payroll history and payslip | own, Approved/Paid only | own, Approved/Paid only | all (also previews) | all |

Managers have **no** payroll or salary access to their reports. Every rule is enforced in `PayrollService` and
`SalaryStructureService`, and again by the controller policies. Records an employee may not see return 404.

### 26.11 Approval workflow

1. `Draft`: the period is created.
2. **Calculate** → `Calculated`: review, edit manual amounts, recalculate.
3. **Submit** (HR/Admin) → `PendingApproval`: locked; leave in these dates is locked too.
4. **Approve** (Admin) → `Approved`: final; employees can see their payslips.
5. **Mark paid** (Admin) → `Paid`. *Day 18: replaced by Finalize → payment batch; the period becomes `Paid` when every
   payable payslip is paid (§28).*

`Draft`, `Calculated` and `PendingApproval` can be **Cancelled**. The records are kept with status Cancelled, and the
same dates can then be used by a new period. Records follow the period (`Approved`, `Paid`, `Cancelled`). There is no
"return to Calculated" step: cancel and recreate instead.

### 26.12 Payslip workflow

`GET /api/payroll/payslip/{recordId}` returns:

- company: `Payroll:CompanyName`, `:CompanyAddress`, `:Currency`;
- employee: name, ID, department and designation, as copied at calculation;
- the period, earnings and deduction lines, gross, total deduction and net;
- day counts, payment status, approved by/at, paid at, and `isFinal`.

Employees get only their own Approved/Paid payslips; HR/Admin also see previews. Each view is audited. No PDF library
was added; the frontend payslip page is print-ready (A4 print styles).

### 26.13 Error handling

The standard envelope is used throughout:

- `AppExceptionHandler` now maps `ForbiddenException` → 403, `UnauthorizedException` → 401 and
  `DbUpdateConcurrencyException` → 409.
- A 401 from the pipeline (missing, expired or revoked token) and a 403 from a policy use the same envelope, through
  `StatusCodeResponseWriter`.

### 26.14 Testing checklist (executed 2026-10-03)

| Check | Result |
|---|---|
| Payroll period created; invalid range; overlapping; duplicate; > 31 days | PASS |
| Records generated; duplicate employee record prevented (service and unique index) | PASS |
| Gross, total deduction and net calculations; decimals; proration; leave deduction cap | PASS |
| Approved unpaid leave deducted; paid, pending and rejected leave not | PASS |
| Unauthorized users can't modify payroll (Employee, Manager, HR approving) | PASS |
| An employee can't access another employee's payroll or payslip; a manager can't see reports' payroll | PASS |
| Approved payroll can't be edited, recalculated, cancelled or deleted; paid payroll can't be edited | PASS |
| Payroll can't become Paid before approval; approve before submit is refused | PASS |
| The payslip shows the correct salary information | PASS (API and browser) |
| Edge cases: zero allowance, zero deduction, multiple deductions, unpaid leave, bonus, overtime, negative net (NeedsReview), missing salary (skipped), inactive employee (excluded), no employees (submit refused), already approved, already paid | PASS |
| Existing Day 1–15 functionality | PASS with authentication on (regression suites); the only change is the expected OpenAPI route count |

Results:

- unit tests: 282 / 282 (67 new);
- live API checks: 249 / 249;
- browser checks: 28 / 28;
- Day 1–13 regression suites: 193 + 167 + 108 + 137 passed, and 56 of 57 (the one difference is the route count).

All test data used `TEST-*` prefixes and 2099 payroll dates, and was removed afterwards. A checksum of every
pre-existing table was identical before and after.

### 26.15 Known limitations

See §24: refresh tokens, public photos, leave balances, holidays, tax slabs, overtime module, final settlement, and
single-Admin approval. Frontend tests: the frontend has no unit-test setup (no Vitest or Jest), and none was added; the
browser suite covers it instead.

### 26.16 Future extension points

- `PayrollCalculator` is the single place for formulas: tax slabs, overtime rates or attendance-based deductions plug
  in there.
- Payroll line items: add `PayrollAllowance`/`PayrollDeduction` tables referencing `PayrollRecord` if more than the
  fixed columns are needed. The Day 15 prompt suggested them; today the fixed columns cover every listed type.
- `WorkCalendar` can read a holiday table to exclude public holidays from working days.
- Leave balances: a `LeaveEntitlement` table, checked in `LeaveService.CreateAsync`.
- PDF payslips: a server-side renderer behind the existing payslip DTO.

---

## 27. Day 17 — Payslip, Payroll History & Employee Payroll

### 27.1 What was missing after Day 16

Day 16 built payslips on the fly from the payroll record and tracked payment per payroll period only. There was:

- no payslip identity (number, issue date);
- no payment status or date per employee;
- no paged or filtered history across periods;
- no endpoints that take the employee from the token;
- no provident fund.

Day 17 adds these. It doesn't recalculate anything: the payroll calculation stays in `PayrollCalculator`.

### 27.2 Payslip generation timing

> **Day 18 change:** the two `mark-paid` endpoints and the "Record payment" panel described in this section were
> removed. Approved payroll is now finalized (and locked), and salaries are paid through payment batches (§28). The
> rest of §27 (payslip issuing, history, self-service) is unchanged.

- **A payslip is issued only when its payroll is approved.** `PayrollService.ApproveAsync` issues one payslip per
  payroll record in the same database transaction as the approval.
- No payslip ever exists for Draft, Calculated, PendingApproval or Cancelled payroll. `POST /api/payroll/{periodId}/payslips`
  answers 409 for those.
- That endpoint only fills gaps, for payroll approved before Day 17. It is idempotent.
- The payslip **is the approved snapshot**. It points to the payroll record, which is locked from approval on: no
  edit, recalculation, cancel or delete. The amounts are never copied a second time and never recalculated.
- **Payment is separate from approval:**
  - an issued payslip starts `Unpaid` with no payment date;
  - `POST /api/payroll/payslips/{id}/mark-paid` (Admin) records one employee's payment;
  - the existing `POST /api/payroll/{periodId}/mark-paid` records every remaining payment at once;
  - the payment date defaults to today's office date and can't be in the future or before the period starts;
  - payslips already paid keep their own date;
  - the payroll period becomes `Paid` automatically when its last payslip is paid.

### 27.3 Database (migration `AddPayslipsAndProvidentFund`)

| Change | Details |
|---|---|
| New table `Payslips` | `PayslipNumber` ("PS-{period start yyyyMMdd}-{employee code}", unique), `PayrollRecordId` (unique, FK), `PayrollPeriodId`, `EmployeeId`, `GeneratedAt`, `GeneratedByUserId`, `PaymentStatus` (Unpaid/Paid), `PaymentDate` (`date`), `PaidAt`, `PaidByUserId`; check: Paid ⇔ payment date present; every FK NO ACTION |
| `PayrollRecords.ProvidentFund` | `decimal(18,2)`, default 0; included in `TotalDeduction` |
| `EmployeeSalaryStructures.MonthlyProvidentFund` | `decimal(18,2)`, default 0; prorated like tax |

The migration is purely additive. A verified backup was taken first (`SmartHRMSDB_before_Day17_*.bak`).

Formula (unchanged apart from the provident fund):

```
Gross  = Basic + HouseRent + Medical + Transport + OtherAllowance + Overtime + Bonus
Deduct = Tax + ProvidentFund + UnpaidLeave + Advance + Loan + Other
Net    = Gross − Deduct
```

### 27.4 API (Day 17 endpoints)

| Method | Endpoint | Who | Notes |
|---|---|---|---|
| GET | `/api/payroll/history` | HR, Admin | every record, any status, with payslip and payment |
| GET | `/api/payroll/payslips` | HR, Admin | issued payslips only |
| GET | `/api/payroll/payslips/{id}` | owner, HR, Admin | 404 for anyone else |
| POST | `/api/payroll/{periodId}/payslips` | HR, Admin | issue missing payslips of Approved/Paid payroll; 409 otherwise |
| POST | `/api/payroll/payslips/{id}/mark-paid` | Admin | `{ "paymentDate": "yyyy-MM-dd" }` (optional) |
| GET | `/api/payroll/employee/{employeeId}/payslips` | the employee, HR, Admin | 403 for anyone else |
| GET | `/api/payroll/me/payslips` | any signed-in employee | the employee comes from the token; a sent `employeeId` is ignored |
| GET | `/api/payroll/me/payslips/current` | any signed-in employee | latest payslip; 404 while none is issued |
| GET | `/api/payroll/me/payments` | any signed-in employee | paid payslips, newest payment first |

Changed:

- `POST /api/payroll/{periodId}/mark-paid` accepts an optional `RecordPaymentDto`.
- `GET /api/payroll/payslip/{recordId}` (Day 16 route) is still there: HR/Admin get any record (a preview before
  approval), employees only their own issued payslip.

### 27.5 DTOs

- `PayrollHistoryQueryDto` (query): `employeeId`, `departmentId`, `month` (1–12), `year` (2000–2100), `status` (payroll
  status), `paymentStatus` (Unpaid, Paid), `search`, `page` (≥ 1), `pageSize` (1–100, default 25), `sortBy` (`period`,
  `employee`, `gross`, `net`, `paymentDate`), `sortDirection` (`asc`, `desc`). Invalid values give 400.
- `PagedResult<T>`: `items`, `page`, `pageSize`, `totalCount`, `totalPages`. This is the first paged response in the
  API; it is returned inside the standard envelope.
- `PayrollRecordDto` gained `providentFund`, `payslipId`, `payslipNumber`, `payslipGeneratedAt`, `paymentStatus`
  (null until issued) and `paymentDate`.
- `PayslipDto` gained `payslipId`, `payslipNumber`, `payrollPeriodId`, `employeePhotoUrl`, `payrollStatus`,
  `paymentDate` and `generatedAt`; a "Provident fund" deduction line was added.
  - **Breaking change:** in `PayslipDto`, `paymentStatus` used to carry the payroll status. It is now `Unpaid`/`Paid`,
    and the payroll status is in `payrollStatus`. The bundled frontend was updated.
- `PayrollPeriodDto` gained `payslipCount`, `paidPayslipCount` and `actions.canGeneratePayslips`.
- `RecordPaymentDto` (`paymentDate`) and `PayslipGenerationResultDto` (`generated`, `alreadyGenerated`).
- `SalaryStructureDto` and its update DTO gained `monthlyProvidentFund`.

### 27.6 Permissions and security rules

| | Employee | Manager | HR | Admin |
|---|---|---|---|---|
| Own payslips, current payslip, payment history | ✓ | ✓ (own) | ✓ (own) | ✓ (own) |
| Another employee's payslip | — (404) | — (404, also for direct reports) | ✓ | ✓ |
| Payroll history, payslip list | — (403) | — (403) | ✓ | ✓ |
| Issue missing payslips | — | — | ✓ | ✓ |
| Record a payment (payslip or whole period) | — | — | — (403) | ✓ |

- Every rule is enforced in `PayslipService` and again by the controller policies.
- The "me" endpoints never read an employee id from the request.
- `employee/{employeeId}` routes compare the id with the token's employee.
- Requests without a valid token get 401.
- The audit log records `PayslipsGenerated`, `PayslipPaid`, `PayrollPaid` and every payslip view (`PayslipViewed`).

### 27.7 Frontend

- **Payroll history** (`/payroll/history`) and **Payslips** (`/payroll/payslips`), both HR/Admin:
  - server-side filters (search, employee, department, month, year, payroll status, payment status) and sorting, all
    kept in the URL;
  - server paging and a reset button;
  - actions: view breakdown, payslip, print;
  - loading, empty and error states.
- **Payslip details** (`/payroll/payslips/:id`, plus the Day 16 `/payroll/payslip/:recordId`):
  - shows payslip number, photo, pay period, payment date, earnings, deductions (with provident fund), net, and
    payroll and payment status;
  - Admins get a "Record payment" panel with a date picker;
  - "Print / save as PDF", and `?print=1` opens the print dialog automatically.
- **My Payroll** (`/payroll/my`) has three tabs: current payslip (full payslip), payslip history (paged), and payment
  history (paged). Every call uses the token-based "me" endpoints.
- **Elsewhere:**
  - the period page shows "N payslips · M paid" and a *Generate payslips* action when payslips are missing;
  - the records table shows payment status and date;
  - the salary structures page has the provident fund.
- **Print and download:** printing is browser print with print CSS (sidebar, topbar and background hidden; A4). "Save
  as PDF" in the print dialog is the download. No PDF library was added.

### 27.8 Day 17 Checklist

**Backend**
- [x] Payslip implemented (issued at approval; approved snapshot)
- [x] Payslip API implemented
- [x] Payroll history API implemented
- [x] Employee payroll API implemented (`me/*`, from the token)
- [x] Filtering implemented
- [x] Pagination implemented (plus sorting)
- [x] Authorization verified
- [x] Ownership verified

**Frontend**
- [x] Payroll history
- [x] Payslip list
- [x] Payslip details
- [x] My Payroll
- [x] Payment history
- [x] Print functionality
- [x] Loading states
- [x] Empty states
- [x] Error handling

**Security**
- [x] Employee isolation (404 for other payslips, 403 for other employees' lists, `me` ignores a sent id)
- [x] HR permissions (history, payslips, issue; no payment recording, no audit log, no users)
- [x] Admin permissions
- [x] 401 tested
- [x] 403 tested

**Regression**
- [x] Day 1–16 functionality verified (API and browser suites; the changes are listed in §27.9)
- [x] No breaking changes, except `PayslipDto.paymentStatus` (now the payment status; the payroll status moved to
  `payrollStatus`) and payment dates before the period start, which are now refused

### 27.9 Testing results (2026-10-03)

| Suite | Result |
|---|---|
| Unit tests | 306 / 306 (24 new) |
| Day 17 live API | 184 / 184 |
| Day 16 live API (regression) | 251 / 251; two Day 17 updates: the payslip status assertions, and a past payment month for the test period |
| Day 10–13 live API (regression) | 193 + 167 + 108 + 137, and 56 / 57 (the OpenAPI route count is now 62, with 0 duplicates; the old check expects 26) |
| Day 17 browser | 13 / 13 after correcting one hard-coded count in the test (the page showed 16 payslips, as the database confirmed) |
| Day 1–16 browser (regression) | 59 / 59, 28 / 28, 28 / 28; two Day 16 expectations updated to the Day 17 wording and layout |

All test data used `TEST-*` prefixes and was removed afterwards.

---

## 28. Day 18 — Payroll Finalization & Payment Management

### 28.1 Overview

Day 18 splits "paid" out of the payroll workflow:

```
Calculated → PendingApproval → Approved (payslips issued) → Finalized + locked → payment batch → Pending → Processing → Paid → payroll Paid
```

- **Finalization** freezes an approved payroll. From then on no payroll value can change.
- **Payments** are tracked per employee in payment batches, with a status history and an audit trail.
- **Payment is recorded, not executed.** There is no bank, mobile-banking or payment-gateway integration: an Admin
  records the outcome of each transfer. The model (method, transaction reference, statuses, failure reason, retry) is
  ready for a future provider.

**Reused:**
- the payroll status enum, extended with `Finalized`;
- `PayrollPeriod`, `PayrollRecord` and `Payslip`;
- the audit logger, `ICurrentUser` and the role policies;
- `AttendanceClock` for office dates;
- `PagedResult<T>`, the response envelope and the exception middleware.

**Replaced (breaking):** `POST /api/payroll/{periodId}/mark-paid`, `POST /api/payroll/payslips/{id}/mark-paid` and
`RecordPaymentDto` were removed. Payment batches are now the only way to pay a salary, so there is a single payment
path with full history. The bundled frontend was updated.

### 28.2 Payroll finalization workflow

`POST /api/payroll/{periodId}/finalize` (Admin) runs in one database transaction:

1. Checks the payroll exists (404).
2. Checks it isn't finalized already (409 `Payroll is already finalized.`).
3. Checks it is `Approved`. Draft, Calculated or PendingApproval get 400 `Payroll must be approved before finalization.`
4. Validates the records. Each failing condition is reported (400 `Payroll can't be finalized: …`):
   - the payroll has records;
   - no record needs review and none has a negative net salary;
   - every record has salary information.
5. Issues any missing payslip (only for payroll approved before Day 17).
6. Sets `Status = Finalized`, `IsLocked = true`, `FinalizedAt` and `FinalizedByUserId`, and marks every record `Finalized`.
7. Writes the `PayrollFinalized` and `PayrollLocked` audit entries.

`PayrollPeriodDto` gains `finalizedAt`, `finalizedBy` and `isLocked`. Its `actions` gain:
- `canFinalize`: Admin, and the payroll is Approved;
- `canCreatePaymentBatch`: Admin, the payroll is Finalized, and some payslips are unpaid.

### 28.3 Payroll locking rules

- Once `IsLocked` is set, every payroll change answers 409 `Payroll is locked and cannot be modified.` This covers:
  - editing the period;
  - deleting or cancelling it;
  - calculating or recalculating;
  - editing a record (salary, allowances, deductions, net).
- Leave approval and attendance changes inside a finalized period's dates are refused, as for approved payroll.
- Payment amounts come from the locked records. The client never sends an amount.
- There is no unlock or correction workflow. A finalized payroll can only be paid (§24).

### 28.4 Payment batch workflow

1. **Create** (`POST /api/payments/batches`, Admin), with `payrollPeriodId`, `paymentMethod`, and optionally
   `paymentDate` (planned; defaults to today's office date) and `notes`. The server:
   - requires the payroll to be Finalized and locked (400 `Payroll must be finalized before payment.`, or
     `This payroll is already fully paid.`);
   - refuses a second open batch (409 `Payment batch already exists for this payroll.`);
   - selects every record with net salary > 0, an issued unpaid payslip, and no active payment
     (400 `There is nothing left to pay for this payroll.` when none is left);
   - creates one item, one transaction (`Pending`) and one history row per employee;
   - calculates `TotalEmployees` and `TotalAmount` itself (totals sent by a client are ignored);
   - writes a `PaymentBatchCreated` audit entry, all in a single save.
2. **Process** (`POST /api/payments/batches/{id}/process`): every `Pending` payment becomes `Processing`. With nothing
   pending it answers 409.
3. **Per payment:**
   - `paid`: optional `transactionReference` and `paymentDate` (defaults to today; not in the future, not before the
     period starts);
   - `failed`: reason required;
   - `retry`: Failed → Processing, reason optional;
   - `cancel`: Pending only, reason required.
4. **Cancel a batch** (reason required) while every active payment is still `Pending`. The payroll can then be paid in
   a new batch.
5. **Completion.** When a payment is paid:
   - its payslip is marked `Paid`, with payment date, method and reference;
   - when every payable payslip of the payroll is paid, the payroll and its records become `Paid` (audit `PayrollPaid`).
6. **Leftovers.** Employees whose payment was cancelled are paid in a later batch (`PAY-yyyyMM-0002`, …), which only
   contains what is still unpaid.

**Batch numbers.** `PAY-{period start yyyyMM}-{sequence:0000}`. The sequence counts existing batches of that month,
and a unique index on `BatchNumber` rejects a collision from a parallel request (409).

### 28.5 Payment statuses

| Payment (`PaymentTransactionStatus`) | Meaning |
|---|---|
| `Pending` | created with the batch, nothing sent yet |
| `Processing` | the transfer is in progress |
| `Paid` | completed (final) |
| `Failed` | the transfer failed (reason kept); can be retried |
| `Cancelled` | withdrawn before processing (final) |

**Batch status** (`PaymentBatchStatus`) is derived from its payments every time one of them changes:
- all cancelled → `Cancelled`;
- any processing → `Processing`;
- all active payments paid → `Paid` (cancelled payments are ignored);
- some paid → `PartiallyPaid`;
- any failed → `Failed`;
- otherwise `Pending`.

Batch totals are recomputed at the same time, without cancelled items.

**Payment method** (`PaymentMethod`): `BankTransfer`, `Cash`, `MobileBanking`, `Cheque`, `Other`. It is a controlled
enum; any other value gets 400.

### 28.6 Payment status transitions

| From → To | Allowed | Action |
|---|---|---|
| Pending → Processing | ✓ | batch `process` |
| Pending → Cancelled | ✓ | `cancel` (or batch `cancel`) |
| Processing → Paid | ✓ | `paid` |
| Processing → Failed | ✓ | `failed` |
| Failed → Processing | ✓ | `retry` |
| Paid → anything | ✗ | 409 `Payment has already been completed.` |
| Cancelled → anything | ✗ | 409 `Payment has been cancelled.` |
| anything else (e.g. Pending → Paid, Failed → Cancelled) | ✗ | 409 `Invalid payment status transition: X → Y.` |

The rules live in one place, `PaymentRules`, and are unit-tested. Every change:
- adds a `PaymentStatusHistory` row (previous status, new status, reason, user, time);
- mirrors the status onto the batch item;
- refreshes the batch;
- writes an audit entry, all in one save.

### 28.7 Entities / tables

| Table | Columns (besides `Id`, `CreatedAt`, `UpdatedAt`) |
|---|---|
| `PayrollPeriods` (changed) | + `FinalizedAt`, `FinalizedByUserId`, `IsLocked` |
| `Payslips` (changed) | + `PaymentMethod` (`nvarchar(20)`), `PaymentReference` (`nvarchar(100)`) |
| `PaymentBatches` | `BatchNumber` (unique), `PayrollPeriodId`, `PaymentDate` (`date`), `PaymentMethod`, `TotalEmployees`, `TotalAmount`, `Status`, `Notes`, `CreatedByUserId`, `RowVersion` |
| `PaymentBatchItems` | `PaymentBatchId`, `EmployeeId`, `PayrollRecordId`, `Amount`, `Status`, `PaymentReference`, `FailureReason`, `PaidAt` |
| `PaymentTransactions` | `PaymentBatchId`, `PaymentBatchItemId` (unique), `EmployeeId`, `Amount`, `PaymentMethod`, `Status`, `TransactionReference`, `FailureReason`, `ProcessedAt`, `PaymentDate` (`date`), `RowVersion` |
| `PaymentStatusHistories` | `PaymentTransactionId`, `PreviousStatus` (null at creation), `NewStatus`, `Reason`, `ChangedByUserId` (the time is `CreatedAt`) |

**Constraints:**
- check `TotalEmployees ≥ 0 AND TotalAmount ≥ 0`;
- check item and transaction `Amount > 0`;
- check that a `Paid` transaction has `PaymentDate` and `ProcessedAt`.

**What is never stored:** no account numbers, card data, PINs, OTPs, passwords or other credentials. Transaction
references allow only letters, digits, spaces and `. _ / # : -` (max 100).

### 28.8 Relationships

```
PayrollPeriod 1 ─ * PaymentBatch 1 ─ * PaymentBatchItem 1 ─ 1 PaymentTransaction 1 ─ * PaymentStatusHistory
PaymentBatchItem * ─ 1 PayrollRecord (1 ─ 0..1 Payslip)      Item/Transaction * ─ 1 Employee
PaymentBatch.CreatedByUserId, PaymentStatusHistory.ChangedByUserId, PayrollPeriod.FinalizedByUserId → ApplicationUsers
```

Every new foreign key is `ON DELETE NO ACTION`: financial records are never removed by cascade.

### 28.9 API endpoints

| Method | Endpoint | Who | Body / query | Response |
|---|---|---|---|---|
| POST | `/api/payroll/{periodId}/finalize` | Admin | — | `PayrollPeriodDto` |
| POST | `/api/payments/batches` | Admin | `CreatePaymentBatchDto` | 201 `PaymentBatchDto` |
| GET | `/api/payments/batches` | HR, Admin | `payrollPeriodId`, `status`, `page`, `pageSize` | `PagedResult<PaymentBatchDto>` |
| GET | `/api/payments/batches/{id}` | HR, Admin | — | `PaymentBatchDto` (with counts and `actions`) |
| POST | `/api/payments/batches/{id}/process` | Admin | — | `PaymentBatchDto` |
| POST | `/api/payments/batches/{id}/cancel` | Admin | `{ "reason" }` | `PaymentBatchDto` |
| GET | `/api/payments` | HR, Admin | `batchId`, `employeeId`, `payrollPeriodId`, `status`, `paymentMethod`, `from`, `to`, `search`, `page`, `pageSize` | `PagedResult<PaymentDto>` |
| GET | `/api/payments/me` | any signed-in employee | same filters; the employee comes from the token | `PagedResult<PaymentDto>` |
| GET | `/api/payments/{id}` | owner, HR, Admin | — | `PaymentDto` with `history` |
| POST | `/api/payments/{id}/paid` | Admin | `{ "transactionReference", "paymentDate" }` (optional) | `PaymentDto` |
| POST | `/api/payments/{id}/failed` | Admin | `{ "reason" }` | `PaymentDto` |
| POST | `/api/payments/{id}/retry` | Admin | `{ "reason" }` (optional) | `PaymentDto` |
| POST | `/api/payments/{id}/cancel` | Admin | `{ "reason" }` | `PaymentDto` |

**Payment list behaviour:**
- paged: default 25, maximum 100;
- newest first;
- the date range filters on the payment date, or the batch's planned date while unpaid;
- `search` matches employee code, employee name or transaction reference.

**`PaymentDto`** carries: employee, period, batch number, payslip id, amount, method, status, reference, failure
reason, payment date, processed time, and `actions` (`canMarkPaid`, `canMarkFailed`, `canRetry`, `canCancel`, as
computed by the server).

The Day 17 `GET /api/payroll/me/payments` (paid payslips) still works.

### 28.10 Authorization / permissions

The project uses roles, not permission strings. The Day 18 permissions map to roles as follows:

| Permission | Employee | Manager | HR | Admin |
|---|---|---|---|---|
| Payroll.View | own payslips | own payslips | ✓ | ✓ |
| Payroll.Finalize / Payroll.Lock | — | — | — (403) | ✓ |
| Payroll.Payment.View / ViewHistory | own payments (`/me`, `/{id}`) | own only | ✓ | ✓ |
| Payroll.Payment.CreateBatch / Process / MarkPaid / MarkFailed / Retry / Cancel | — | — | — (403) | ✓ (not own salary) |

**Where the rules are enforced:**
- **Controller policies:** `Admin` for every change, `HrOrAdmin` for the lists.
- **The services, again:** `EnsureAdmin`, `EnsureHrOrAdmin`.
- **Ownership:**
  - another employee's payment is 404 for employees and managers;
  - `/me` ignores a sent `employeeId`;
  - an Admin can't change the status of their own salary payment (403 `You can't change the status of your own
    salary payment; another Admin must do it.`).
- **Authentication:** requests without a token get 401.
- **Frontend:** it only hides what the server would refuse, using the server-computed `actions`.

### 28.11 Validation rules

| Rule | Where | Response |
|---|---|---|
| `payrollPeriodId` and `paymentMethod` required; method must be a defined enum value | DTO | 400 |
| Planned payment date not before the period start | service | 400 |
| Paid date not in the future and not before the period start | `PayslipIssuer.PaymentDate` | 400 |
| Reason required for failed, cancel and batch cancel (max 500) | service / DTO | 400 |
| Transaction reference max 100 characters, restricted character set | DTO | 400 |
| Status / method filters must be defined names; `from` ≤ `to`; `page` ≥ 1; `pageSize` 1–100 | service | 400 |
| Only payable records (net > 0, payslip issued and unpaid, no active payment) | service | not included |
| Unknown payroll, batch or payment | service | 404 |

### 28.12 Duplicate-payment protection

**Application level:**
- one open batch per payroll (`HasOpenBatchAsync`);
- records that already have an active (non-cancelled) payment are skipped;
- paid payslips are skipped.

**Database level** (safe when two requests arrive together):
- filtered unique index `IX_PaymentBatches_PayrollPeriodId_Open` on `PayrollPeriodId` WHERE `Status <> 'Paid' AND
  Status <> 'Cancelled'`: one open batch per payroll. (SQL Server filtered indexes don't accept `NOT IN`.)
- filtered unique index `IX_PaymentBatchItems_PayrollRecordId_Active` on `PayrollRecordId` WHERE `Status <>
  'Cancelled'`: one active payment per payroll record, so per employee and payroll.
- unique `(PaymentBatchId, EmployeeId)`, unique `PaymentTransactions.PaymentBatchItemId` and unique `BatchNumber`.
- `RowVersion` on batches and transactions: two Admins changing the same payment get 409 instead of a lost update.

A violation is mapped to 409 by `SmartHRMSDbContext`. Verified live: 5 parallel batch creations gave one 201 and
four 409, one open batch, and no record with two active payments.

### 28.13 Audit logging

| Action | Entity | Details (never amounts) |
|---|---|---|
| `PayrollFinalized` | PayrollPeriod | number of records |
| `PayrollLocked` | PayrollPeriod | Approved → Finalized |
| `PaymentBatchCreated` | PaymentBatch | batch number, employee count, method, payroll |
| `PaymentBatchCancelled` | PaymentBatch | batch number, reason |
| `PaymentProcessingStarted` | PaymentBatch | number of payments moved to Processing |
| `PaymentMarkedPaid` | PaymentTransaction | employee code, date, reference |
| `PaymentMarkedFailed` / `PaymentRetried` / `PaymentCancelled` | PaymentTransaction | employee code, transition, reason |
| `PayrollPaid` | PayrollPeriod | when the last payable payslip is paid |

- Each entry has the user, time, entity and entity id.
- The before/after state is in the details and in `PaymentStatusHistories`.
- As in Day 16, salary and payment amounts are never written to the audit log. A live check confirms no amount
  appears in it.
- The Day 17 `PayslipPaid` action is no longer written.

### 28.14 Error handling

The global `AppExceptionHandler` is used unchanged:
- 400 for validation;
- 401 without a valid token;
- 403 for role and own-salary refusals;
- 404 for unknown or foreign resources;
- 409 for state conflicts, duplicates and concurrency;
- 500 without details.

Messages include those required by Day 18, for example:
- `Payroll must be approved before finalization.`
- `Payroll is already finalized.`
- `Payroll is locked and cannot be modified.`
- `Payment batch already exists for this payroll.`
- `Payment has already been completed.`
- `Invalid payment status transition: X → Y.`

Every multi-row change (finalize, batch creation, status change with history, batch refresh, payslip mirror and audit)
is one `SaveChanges`, so it is one database transaction and rolls back as a whole.

### 28.15 Database migration

- **Migration:** `20261005095811_AddPayrollFinalizationAndPayments`. It is purely additive: 5 new columns and 4 new
  tables, and nothing is dropped or rewritten.
- **Backup first:** a verified copy-only backup, `SmartHRMSDB_before_Day18_20261005_155535.bak`.
- **First attempt:** the first generated version failed on SQL Server (a filtered index with `NOT IN`). The update
  rolled back completely, verified by querying the schema. The filter was rewritten with `<>` and the migration
  regenerated before it was applied.
- **Verified:** `has-pending-model-changes` reports no drift, and both filtered indexes were queried in
  `sys.indexes`.

### 28.16 Testing checklist (executed 2026-10-05)

| Suite | Result |
|---|---|
| `dotnet build` | 0 warnings, 0 errors |
| Unit tests | 339 / 339 (38 payment-service tests; finalization and lock tests in the payroll tests) |
| Day 18 live API (`TEST-D18-*`) | 250 / 250 |
| Day 18 browser | 20 / 20, no JS exceptions or console errors |
| Day 17 live API (adapted to finalize + batch) | 199 / 199 |
| Day 16 live API (adapted to finalize + batch) | 264 / 264 |
| Day 10–13 live API (regression) | 193 + 167 + 108 + 137, and 56 / 57 (the old OpenAPI check expects 26 routes; there are now 72, with 0 duplicates) |
| Day 13 / 14 / 16 / 17 browser (regression) | 59 / 59, 28 / 28, 28 / 28, 13 / 13 |
| `npm run build`, `tsc`, `eslint` | pass |

**Problems found by testing and fixed:**
- Status changes failed against SQL Server with 409. A new history row added only through the navigation was saved
  as an UPDATE. It is now registered explicitly (`IPaymentRepository.AddHistory`).
- Payment amounts were written into three audit entries, against the Day 16 "no amounts" rule. They were removed.
- The batch details table didn't refresh after "Start processing". It now reloads in place.
- The Payment history breadcrumb read "History".

**Covered:**
- finalization (approved, unprocessed, unapproved, twice, every role, unauthenticated, lock);
- batch creation (from finalized only, duplicates, counts, totals, items);
- transitions (all valid ones, invalid ones, paid twice, cancelled can't be paid);
- concurrency;
- employee security (own vs other, `/me` can't be widened, managers can't see reports' payments);
- database constraints and audit.

All test data used `TEST-*` prefixes and was removed. A checksum of every real table is identical to the pre-test
baseline, except two entries caused by one rejected sign-in during setup (§28.17).

### 28.17 Security checklist

- [x] Every payment and finalize endpoint requires authentication (401 tested)
- [x] Role checks in the controller and again in the service (403 tested for HR, Manager, Employee)
- [x] Ownership: employees see only their own payments (404 otherwise); `/me` ignores a sent `employeeId`
- [x] No self-service on one's own salary: an Admin can't change their own payment
- [x] Amounts, totals, status and employee are decided by the server; client-sent totals are ignored
- [x] No credentials, PINs, OTPs, card or account data stored; reference character set restricted
- [x] No amounts in the audit log
- [x] Duplicate payment blocked by the application and by filtered unique indexes; concurrent requests tested
- [x] Optimistic concurrency (`RowVersion`) on batches and transactions
- [x] No cascade deletes on financial records
- Test setup note: `documentation/log.md` no longer matches the real admin password (it was reset in the app on
  2026-10-05). One sign-in attempt with it was refused. That left `FailedLoginCount = 1` on the real admin (reset by
  the next successful sign-in) and one genuine `LoginFailed` audit entry, which was kept. Test admins were created as
  `TEST-*` rows and removed afterwards.

### 28.18 Day 18 completion status

- [x] Payroll finalization implemented
- [x] Payroll lock implemented
- [x] Finalization authorization verified
- [x] Payment batch implemented
- [x] Payment batch items implemented
- [x] Payment transactions implemented
- [x] Payment status history implemented
- [x] Payment status transitions verified
- [x] Duplicate payment protection verified
- [x] Payment batch totals verified
- [x] Audit logging verified
- [x] Employee self-service verified
- [x] HR permission verified
- [x] Admin permission verified
- [x] Unauthorized access tested
- [x] API validation tested
- [x] Database migration tested
- [x] Backend tests passed
- [x] Frontend tests passed
- [x] Day 1–17 regression passed (two old suites adapted to the replaced payment endpoints; one pre-existing route-count check outdated)
- [x] No existing feature broken, apart from the intended replacement of the Day 17 `mark-paid` endpoints

**Frontend (Day 18):**
- **Period page:**
  - Finalize action with the required confirmation dialog;
  - Finalized and Locked badges, a "Finalized and locked" timeline step and a lock notice;
  - the payroll's payment batches, and a Create payment batch form (method, planned date, notes).
- **Payment batches** (`/payments/batches`):
  - filters (payroll period, status) and paging;
  - View, Process and Cancel, shown only when the server allows them.
- **Batch details** (`/payments/batches/:id`):
  - summary with counts;
  - payment table with search, employee and status filters, and paging;
  - per-row actions: paid (reference and date), failed (reason), retry, cancel, and a details view with the status
    history.
- **Payment history** (`/payments/history`): filters for employee, payroll period, date range, status, method and
  search.
- **My Payroll → Payment history:** the employee's own payments (month, net, status, date, method, reference,
  payslip) and their status history; read-only.
- **Removed:** the Day 17 "Record payment" panel on the payslip page.
- **Payslip:** now shows the payment method and reference.

---

## 29. Day 19 — Payroll Reporting, Payslip History & Payroll Audit Trail

### 29.1 Audit: what already existed and was reused

| Day 19 requirement | Before Day 19 | Day 19 |
|---|---|---|
| Payroll history | Record-level history `/api/payroll/history` (Day 17); period list `/api/payroll/periods` (gross/deduction/net only, unpaged) | **Added** period-level history with full totals, filters and paging: `/api/payroll/reports/periods`. The *Payroll periods* page now uses it |
| Payroll details | `/api/payroll/periods/{id}/records` (unpaged) | **Reused** `/api/payroll/history` with new `payrollPeriodId`, `designationId` and `recordStatus` filters (server paging, sorting); the records page now uses it |
| Payslip history | `/api/payroll/payslips` with employee, department, month, year, status, payment status, search, paging (Day 17) | **Reused**, plus the payroll-period filter and a Generated column |
| Payslip view, print / PDF | Payslip page with print CSS ("Save as PDF") (Day 17) | **Reused unchanged.** It already shows every field the brief lists |
| Reports | none | **Added** summary, department, employee, deduction and allowance/bonus reports |
| Export | none (no export library in the project) | **Added** CSV and XLSX using only the .NET standard library (`TableExporter`) |
| Audit log | Generic `AuditLogs` table, `IAuditLogger`, Admin-only `GET /api/audit-logs` | **Reused**, with a `category=payroll` filter, user and date filters, status transitions in details, export events, and an Admin page |
| Payroll statuses | Draft, Calculated, PendingApproval, Approved, Finalized, Paid, Cancelled | **Unchanged.** "Processing/Processed" in the brief map to Calculated/PendingApproval; no new status was introduced |
| Manager access | Managers see their reports' basic record but no salary (Day 16 rule) | **Unchanged.** Managers get no payroll report or payslip access beyond their own payslips |

### 29.2 Database

No migration. Reporting reads the existing `PayrollPeriods`, `PayrollRecords`, `Payslips` and `AuditLogs` tables,
and every aggregate is computed by SQL Server (`GROUP BY`, `SUM`, `COUNT(DISTINCT)`). `has-pending-model-changes`
reports no drift.

### 29.3 Payroll history (period level)

`GET /api/payroll/reports/periods` (HR/Admin) returns one row per payroll period, any status.

- **Row content:**
  - name, start and end dates, month and year, status, lock flag;
  - employees, basic salary, allowances, overtime, bonus, gross, tax, deductions, net, paid and outstanding net;
  - created, processed (`calculatedAt`), approved, finalized, paid and cancelled times.
- **Filters:** `search` (period name), `month`, `year`, `status`, `from` / `to` (periods overlapping the range).
  Paging: `page`, `pageSize` (1–100).
- **Frontend:** *Payroll periods* (`/payroll/periods`), with filters in the URL, CSV/Excel export, View details and
  Records actions.

### 29.4 Payroll details

The period page (`/payroll/{id}`) and its records page (`/payroll/{id}/records`).

- **Data source:** the records page reads `GET /api/payroll/history?payrollPeriodId=…`.
- **Server-side features:** search, department, designation, record status, sorting (employee, gross, net) and paging.
- **Columns:**
  - employee ID and name, department and designation;
  - basic, allowances, overtime, bonus, gross;
  - tax, other deductions, total deduction, net;
  - record and payment status.
- **Server-computed totals.** `PayrollRecordDto` gained two fields so the browser never adds amounts:
  - `totalAllowances` (house rent + medical + transport + other);
  - `otherDeductions` (total deduction − tax).
- **Finalized payroll is read-only:**
  - the page shows a lock notice and no Edit action (`canEdit` is false);
  - the API refuses any change with 409 "Payroll is locked and cannot be modified." (Day 18 rule; re-verified).
- **Export:** CSV/Excel through the employee report, limited to the period.

### 29.5 Payslip history

- **Lists:**
  - `GET /api/payroll/payslips` (HR/Admin) now also filters by `payrollPeriodId`, `designationId` and `recordStatus`;
  - the employee's own list is `GET /api/payroll/me/payslips`.
- **Columns:** employee, employee ID, payroll month, gross, deduction, net, generated date, payroll status and payment
  status.
- **Actions:** view, print (print-ready page, "Save as PDF"), and export of the list as CSV/Excel.
- **Access:**
  - **Employees:** only their own payslips. Another payslip id gives 404, `/me` ignores a sent `employeeId`, and the
    HR lists give 403.
  - **Managers:** only their own payslips. A report's payslip gives 404, and lists give 403.
  - **HR/Admin:** every payslip.

### 29.6 Payroll reports

All reports are `GET /api/payroll/reports/…`, HR/Admin only.

**Filters** (all server-side): `month`, `year`, `payrollPeriodId`, `employeeId`, `departmentId`, `designationId`,
`status`, `from`, `to`, plus `search` for the employee report.
- **Default scope:** issued payroll only (Approved, Finalized, Paid). A `status` filter selects one status instead,
  for example `Calculated` to preview a draft.
- **Department and designation filters** use the employee's current assignment, as in the Day 17 history.
- **Department grouping** uses the department name stored on the payroll record, so history keeps the name it had at
  calculation.

| Report | Endpoint | Content |
|---|---|---|
| Monthly payroll summary / salary expense | `summary` | Totals: employees (distinct), records, basic, allowances, overtime, bonus, gross, tax, other deductions, total deduction, net, paid, outstanding. One row per payroll period |
| Department-wise | `departments` | Per department: employees, gross, total deduction, net (also basic, allowances, tax) |
| Employee payroll | `employees` | One row per employee and period: department, designation, period, gross, deduction, net, payslip, payment status; paged |
| Deduction summary | `deductions` (`groupBy=period\|department`) | Tax, provident fund, unpaid leave, advance, loan, other, total |
| Allowance / bonus summary | `allowances` (`groupBy=period\|department`) | House rent, medical, transport, other allowance, total allowances, overtime, bonus |

**Implementation:**
- `PayrollReportQueries` holds the filters and `GroupBy`/`Sum` projections as LINQ, written once.
  `PayrollReportRepository` runs them in SQL Server, and the unit-test fake runs the identical code in memory.
- Amounts are the stored payroll record values; nothing is recalculated.

**Frontend:** *Payroll reports* (`/payroll/reports`):
- shared filter bar;
- cards: total payroll records, employees, total gross (salary expense), total deduction (with tax), total net (with
  paid and outstanding);
- one tab per report, each with CSV/Excel export;
- a group-by toggle on the deduction and allowance reports;
- proportional bars for salary expense by period and each department's share of net. No chart library is installed,
  so none was added.

### 29.7 Export

**Formats:**
- **CSV:** UTF-8 with BOM so Excel detects the encoding.
- **XLSX:** a minimal single-sheet Office Open XML workbook with a bold header, a frozen first row and number formats.
- **PDF:** the existing print-ready payslip page ("Print / save as PDF").

Both CSV and XLSX are produced by `smartHRMS.Application.Common.Export.TableExporter` with only the .NET standard
library. No package was added.

| Endpoint | Who | Content |
|---|---|---|
| `GET /api/payroll/reports/{report}/export?format=csv\|xlsx` | HR, Admin | periods, summary, departments, employees, deductions or allowances, with the report's filters; grouped reports end with a Total row |
| `GET /api/payroll/payslips/export?format=…` | HR, Admin | issued payslips with the list filters |
| `GET /api/payroll/me/payslips/export?format=…` | any signed-in employee | own payslips only (employee from the token) |

**Rules:**
- **Same permissions as the lists:** an employee or manager gets 403 for report and HR payslip exports, and 401
  without a token.
- **Row cap:** at most 10,000 rows. Larger exports get 400 asking for narrower filters.
- **Formula injection:** CSV text starting with `= + - @` or a tab is prefixed with an apostrophe.
- **File names:** the browser names the file `{report}-{yyyy-MM-dd}.{csv|xlsx}`, because the server's
  `Content-Disposition` isn't readable across origins.
- **Auditing:** every export writes `PayrollReportExported` or `PayslipsExported` with the report, format, row count
  and filter ids, never amounts.

### 29.8 Payroll audit trail

**The existing `AuditLogs` table and `IAuditLogger` are reused.** No second audit mechanism was added.

**Query:** `GET /api/audit-logs` (Admin) gained:
- `category=payroll`: the payroll entity types `PayrollPeriod`, `PayrollRecord`, `Payslip`, `EmployeeSalaryStructure`,
  `PaymentBatch`, `PaymentTransaction` and `PayrollReport`;
- `username` (contains);
- `from` / `to`: office dates, converted to UTC with the office time zone.

**Entries:**
- Each entry holds the user id and user name, action, entity, entity id, time and details.
- **Status transitions:**
  - Before Day 19, Created, Submitted, Approved and Cancelled had no transition in their details. They now read for
    example "→ Draft", "Calculated → PendingApproval", "PendingApproval → Approved" and "PendingApproval → Cancelled:
    reason".
  - Calculated reads "Draft → Calculated: …", and Finalized/Locked "Approved → Finalized".
- **Payroll actions recorded:**
  - created, updated, deleted;
  - calculated (processed), record modified;
  - submitted, approved, finalized, locked, paid, cancelled;
  - payslips generated, payslip viewed;
  - salary structure changed;
  - payment batch and payment actions (Day 18);
  - report exported, payslips exported (new).
- **No amounts:** salary and payment amounts are never written into audit details. A live check on every run confirms
  it.

**Frontend:** *Payroll audit trail* (`/payroll/audit`, Admin, under Administration):
- filters: action, user name, date range, number of entries;
- an entity view when opened with `?entityId=`;
- links to the period, payslip or payment batch.

### 29.9 Authorization summary

| | Employee | Manager | HR | Admin |
|---|---|---|---|---|
| Own payslips (view, print, export own list) | ✓ | ✓ | ✓ | ✓ |
| Another employee's payslip | 404 | 404 (also direct reports) | ✓ | ✓ |
| Payroll history (period and record level), payroll details | 403 | 403 | ✓ | ✓ |
| Payroll reports and report exports, payslip-list export | 403 | 403 | ✓ | ✓ |
| Payroll audit trail | 403 | 403 | 403 | ✓ |
| Edit finalized payroll | — | — | 409 | 409 |

**How it is enforced:**
- **Role checks twice:** in the controller policy and again in the service.
- **Employee identity:** comes from the token for every `me` endpoint. Query parameters such as `employeeId` can't
  widen what a caller sees.
- **Frontend:** it only hides what the server refuses.

### 29.10 Known limitations

- **Audit log reading:** up to 500 newest entries per request (no paging).
- **Department and designation filters:** apply to the employee's *current* assignment. Department grouping uses the
  department stored on the record.
- **Exports:** limited to 10,000 rows. XLSX has one sheet and no charts.
- **PDF:** produced through the browser's print dialog, not generated on the server.
- **Charts:** simple proportional bars; there is no chart library.
- **Report scope:** reports cover issued payroll by default; draft payroll needs an explicit status filter.

### 29.11 Day 19 testing checklist (executed 2026-10-08)

| Suite | Result |
|---|---|
| `dotnet build` | 0 warnings, 0 errors |
| Unit tests | 371 / 371 (32 new: reports, filters, totals, exports, CSV injection guard, payslip export isolation, audit category and dates, workflow transitions) |
| Day 19 live API (`TEST-D19-*`) | 274 / 274; every amount compared with SQL Server's own `SUM` |
| Day 19 browser | 14 / 14, no JS exceptions or console errors |
| Day 18 API / browser (regression) | 250 / 250, 20 / 20 |
| Day 17 API / browser (regression) | 195 / 195, 13 / 13 |
| Day 16 API / browser (regression) | 260 / 260, 28 / 28 |
| Day 10–13 API (regression) | 193 + 167 + 108 + 137, and 56 / 57 (the old OpenAPI check expects 26 routes) |
| Day 13 / 14 browser (regression) | 59 / 59, 28 / 28 |
| `npm run build`, `tsc`, `eslint` | pass |
| Migrations | no Day 19 migration; `has-pending-model-changes` clean |

- **Day 16/17 totals vs Day 18:** each total is 4 lower than on Day 18. Those suites loop over every payment, and
  fewer leftover TEST employees existed this time. They have no failures.
- **Two browser tests needed waits:**
  - two filters changed within one React render (faster than a person can act) lost the first URL update;
  - a sidebar check ran before the page shell had mounted after sign-in.

  The tests now wait for the page to settle, and both suites pass.

**Checklist:**
- **A. Payroll history:** [x] periods load [x] search [x] month [x] year [x] status [x] date range [x] pagination
  [x] details
- **B. Payroll details:** [x] records load [x] values match the database [x] department filter [x] designation filter
  [x] search [x] sort [x] finalized is read-only (UI and 409)
- **C. Payslip:**
  - [x] own payslip [x] another employee's payslip → 404
  - [x] HR sees all [x] manager limited to own
  - [x] values match payroll [x] print [x] export (CSV/XLSX, own and HR)
- **D. Reports:** [x] monthly [x] department [x] employee [x] deduction [x] allowance/bonus [x] filters [x] totals
  equal SQL sums
- **E. Authorization:** [x] employee [x] manager [x] HR [x] admin [x] unauthenticated 401 on every report and export
  endpoint
- **F. Audit:** [x] processing (calculated) [x] submission [x] approval [x] finalization [x] payslip generation
  [x] record modification [x] exports [x] user and timestamp [x] no amounts
- **G. Frontend:** [x] no console errors [x] loading, empty and error states [x] responsive at 390 / 820 px
  [x] pagination [x] filters

All test data used `TEST-*` prefixes and was removed. The checksum of every real table is identical to the pre-test
baseline.
