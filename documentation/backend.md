# Smart HRMS Backend Documentation

## 1. Overview

This project is a backend API for a Human Resource Management System built with ASP.NET Core and Entity Framework Core. The application is designed to manage core HR entities such as employees, departments, designations, roles, permissions, and users.

The backend currently provides the foundational structure for the HRMS system:

- ASP.NET Core Web API
- Entity Framework Core with SQL Server
- Dependency injection based service architecture
- Database-first migration support with EF Core
- Clean model structure for HR domain entities
- SQL Server connection configuration through appsettings.json

---

## 2. Technology Stack

### Backend Framework
- ASP.NET Core 10
- C#
- ASP.NET Core Web API

### Database
- Microsoft SQL Server
- EF Core 10
- EF Core SQL Server provider

### Development Tools
- .NET SDK 10
- Entity Framework Core CLI (`dotnet ef`)
- SQL Server LocalDB / SQL Server Express / SQL Server instance

---

## 3. Project Structure

```text
backend/
  smartHRMS.Api/
    Program.cs
    appsettings.json
    appsettings.Development.json
    smartHRMS.Api.csproj
    Data/
      ApplicationDbContext.cs
    Models/
      Department.cs
      Designation.cs
      Employee.cs
      Permission.cs
      Role.cs
      RolePermission.cs
      User.cs
    Migrations/
    Controllers/
    Repositories/
    Services/
    DTOs/
    Extensions/
    Middleware/
    Properties/
```

### Main folders
- `Data/` – database context and database-related configuration
- `Models/` – domain entities
- `Controllers/` – API endpoints
- `Services/` – business logic
- `Repositories/` – database access layer
- `DTOs/` – request/response models
- `Migrations/` – code-first migration files
- `Extensions/` – reusable configuration methods
- `Middleware/` – custom middleware

---

## 4. Application Startup

The application startup is configured in `Program.cs`.

### Current setup
- Controllers are registered with `AddControllers()`
- Endpoint explorer is enabled with `AddEndpointsApiExplorer()`
- EF Core SQL Server provider is configured with `UseSqlServer()`
- HTTPS redirection is enabled
- Authorization and controller routing are mapped

### Database configuration
The default connection string is loaded from `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=MAHIM\\SQLEXPRESS01;Integrated Security=True;Persist Security Info=False;Pooling=False;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=True;Application Name=\"SQL Server Management Studio\";Command Timeout=0"
  }
}
```

This connection uses the local SQL Server instance named `SQLEXPRESS01` on the developer machine.

---

## 5. Database Context

The `ApplicationDbContext` class is the central EF Core database configuration class.

### DbSets
- `Users`
- `Roles`
- `Permissions`
- `RolePermissions`
- `Departments`
- `Designations`
- `Employees`

### Relationship configuration
The model was configured to avoid SQL Server cascade conflict issues. The application intentionally uses `DeleteBehavior.Restrict` for employee relationships to prevent multiple cascade path problems.

---

## 6. Core Domain Models

### 6.1 Department
Represents an HR department.

Properties:
- `Id`
- `Name`
- `Description`
- `IsActive`
- `CreatedAt`
- `Employees`

### 6.2 Designation
Represents a job title or designation within a department.

Properties:
- `Id`
- `Name`
- `Description`
- `DepartmentId`
- `Department`
- `IsActive`
- `CreatedAt`
- `Employees`

### 6.3 Role
Represents an access role in the system.

Properties:
- `Id`
- `Name`
- `Description`
- `IsActive`
- `CreatedAt`
- `Users`
- `RolePermissions`

### 6.4 Permission
Represents a system permission.

Properties:
- `Id`
- `Name`
- `Description`
- `IsActive`
- `CreatedAt`
- `RolePermissions`

### 6.5 RolePermission
Many-to-many join between Role and Permission.

Properties:
- `Id`
- `RoleId`
- `Role`
- `PermissionId`
- `Permission`
- `CreatedAt`

### 6.6 User
Represents authenticated user accounts.

Properties:
- `Id`
- `Username`
- `Email`
- `PasswordHash`
- `IsActive`
- `RoleId`
- `Role`
- `Employee`
- `CreatedAt`
- `UpdatedAt`

### 6.7 Employee
Represents HR employee records.

Properties:
- `Id`
- `EmployeeCode`
- `FirstName`
- `LastName`
- `Email`
- `Phone`
- `DateOfBirth`
- `Gender`
- `Address`
- `JoiningDate`
- `DepartmentId`
- `Department`
- `DesignationId`
- `Designation`
- `UserId`
- `User`
- `Status`
- `CreatedAt`
- `UpdatedAt`

---

## 7. Entity Relationships

### Main relationships
- One `Department` has many `Designations`
- One `Department` has many `Employees`
- One `Designation` has many `Employees`
- One `Role` has many `Users`
- One `Role` has many `RolePermissions`
- One `Permission` has many `RolePermissions`
- One `User` belongs to one `Role`
- One `User` has one `Employee`
- One `Employee` belongs to one `Department`
- One `Employee` belongs to one `Designation`

This creates the foundation for the HR authorization and employee management flow.

---

## 8. Database Migration Workflow

This project uses code-first migrations.

### Generate migration
```bash
dotnet ef migrations add InitialCreate
```

### Apply migration to database
```bash
dotnet ef database update
```

### Remove last migration
```bash
dotnet ef migrations remove
```

---

## 9. API Routes and Usage

The backend currently exposes a basic health check endpoint.

### Base URL
```text
http://localhost:5099
https://localhost:5099
```

### Available route
```text
GET /api/health
```

### Example request
```bash
curl http://localhost:5099/api/health
```

### Example response
```json
{
  "status": "Healthy",
  "application": "smartHRMS",
  "version": "1.0.0"
}
```

### Controller definition
The route is defined in `HealthController` with:

```csharp
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
```

This means the controller name `Health` becomes the route segment `/api/health`.

---

## 10. Running the Backend

From the project folder:

```bash
cd backend/smartHRMS.Api
 dotnet restore
 dotnet build
 dotnet run
```

### Default URL
When launched in development mode, ASP.NET Core typically runs on:

```text
https://localhost:5099
http://localhost:5099
```

Depending on the local development certificate and launch settings, the actual ports may vary.

---

## 11. Current Backend State

The backend is currently at the foundational stage of the HRMS system:

- project structure is created
- database schema is designed
- database context is configured
- EF Core migrations are working
- model layer is established
- API application startup is ready for controller and service expansion

### Existing folders ready for development
- `Controllers/`
- `Services/`
- `Repositories/`
- `DTOs/`
- `Middleware/`

This means the project is ready for building REST APIs for:

- Employee management
- Department management
- Designation management
- Role and permission management
- User authentication and authorization
- Attendance-related modules (future extension)
- Payroll and leave tracking (future extension)

---

## 12. Recommended Next Steps

### API modules to implement
1. `AuthController` for login and token-based auth
2. `EmployeeController` for CRUD operations
3. `DepartmentController` for department management
4. `DesignationController` for designation management
5. `RoleController` and `PermissionController`
6. `UserController` for employee-user linkage

### Best practices to follow
- Use DTOs instead of raw entity models in controllers
- Use repository/service patterns for cleaner architecture
- Apply validation attributes and FluentValidation
- Add JWT authentication for secure API access
- Add audit logging and exception middleware
- Create unit/integration tests

---

## 13. Security Considerations

The current backend foundation includes:
- SQL Server connection string configuration
- Basic ASP.NET Core security configuration
- Authorization pipeline included

Recommended future improvements:
- JWT authentication
- Role-based authorization
- Password hashing using ASP.NET Core identity or secure hashing methods
- Rate limiting and API security policies
- CORS configuration for frontend integration

---

## 14. Summary

The Smart HRMS backend is a modern ASP.NET Core Web API built around a clean domain-driven model layered around EF Core and SQL Server. It currently contains the HR system’s main entities and a working EF Core migration pipeline. The project is ready to be expanded into a full HR management backend with user authentication, employee operations, department management, permissions, and administrative workflows.

---

## 15. Useful Commands

```bash
dotnet restore
dotnet build
dotnet run
dotnet ef migrations add InitialCreate
dotnet ef database update
dotnet ef migrations remove
```

---

## 16. Developer Notes

This backend is currently in a solid starting phase. The database design and project architecture are in place, and the next major step is to implement controllers, services, DTOs, and authentication logic.

If the team continues to expand the project, the recommended architecture is:

- Controller layer
- Service layer
- Repository layer
- EF Core data access
- DTO validation
- JWT auth + role authorization

This will keep the app maintainable and scalable as the HRMS grows.

### Notes
The migration system is already working with the SQL Server instance configured in the project.

---

## 9. Running the Backend

From the project folder:

```bash
cd backend/smartHRMS.Api
 dotnet restore
 dotnet build
 dotnet run
```

### Default URL
When launched in development mode, ASP.NET Core typically runs on:

```text
https://localhost:5001
http://localhost:5000
```

Depending on the local development certificate and launch settings, the actual ports may vary.

---

## 10. Current Backend State

The backend is currently at the foundational stage of the HRMS system:

- project structure is created
- database schema is designed
- database context is configured
- EF Core migrations are working
- model layer is established
- API application startup is ready for controller and service expansion

### Existing folders ready for development
- `Controllers/`
- `Services/`
- `Repositories/`
- `DTOs/`
- `Middleware/`

This means the project is ready for building REST APIs for:

- Employee management
- Department management
- Designation management
- Role and permission management
- User authentication and authorization
- Attendance-related modules (future extension)
- Payroll and leave tracking (future extension)

---

## 11. Recommended Next Steps

### API modules to implement
1. `AuthController` for login and token-based auth
2. `EmployeeController` for CRUD operations
3. `DepartmentController` for department management
4. `DesignationController` for designation management
5. `RoleController` and `PermissionController`
6. `UserController` for employee-user linkage

### Best practices to follow
- Use DTOs instead of raw entity models in controllers
- Use repository/service patterns for cleaner architecture
- Apply validation attributes and FluentValidation
- Add JWT authentication for secure API access
- Add audit logging and exception middleware
- Create unit/integration tests

---

## 12. Security Considerations

The current backend foundation includes:
- SQL Server connection string configuration
- Basic ASP.NET Core security configuration
- Authorization pipeline included

Recommended future improvements:
- JWT authentication
- Role-based authorization
- Password hashing using ASP.NET Core identity or secure hashing methods
- Rate limiting and API security policies
- CORS configuration for frontend integration

---

## 13. Summary

The Smart HRMS backend is a modern ASP.NET Core Web API built around a clean domain-driven model layered around EF Core and SQL Server. It currently contains the HR system’s main entities and a working EF Core migration pipeline. The project is ready to be expanded into a full HR management backend with user authentication, employee operations, department management, permissions, and administrative workflows.

---

## 14. Useful Commands

```bash
dotnet restore
dotnet build
dotnet run
dotnet ef migrations add InitialCreate
dotnet ef database update
dotnet ef migrations remove
```

---

## 15. Developer Notes

This backend is currently in a solid starting phase. The database design and project architecture are in place, and the next major step is to implement controllers, services, DTOs, and authentication logic.

If the team continues to expand the project, the recommended architecture is:

- Controller layer
- Service layer
- Repository layer
- EF Core data access
- DTO validation
- JWT auth + role authorization

This will keep the app maintainable and scalable as the HRMS grows.
