using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Common.Security;
using smartHRMS.Application.Common.Text;
using smartHRMS.Application.Features.Auth;
using smartHRMS.Application.Features.Users.Dtos;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.Users;

/// <summary>
/// Account rules:
/// - only Admins manage accounts;
/// - usernames are unique; an employee has at most one account;
/// - Employee and Manager accounts must be linked to an employee (their own data is what they see);
/// - the last active Admin can't be demoted or deactivated, and Admins can't deactivate or demote themselves;
/// - a role, active-flag or password change ends the account's existing sessions (new security stamp).
/// </summary>
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;

    public UserService(
        IUserRepository userRepository,
        IEmployeeRepository employeeRepository,
        IPasswordHasher passwordHasher,
        IAuditLogger auditLogger,
        ICurrentUser currentUser)
    {
        _userRepository = userRepository;
        _employeeRepository = employeeRepository;
        _passwordHasher = passwordHasher;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
    }

    public async Task<List<UserDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        _currentUser.EnsureAdmin();
        var users = await _userRepository.GetAllAsync(cancellationToken);
        return users.Select(MapToDto).ToList();
    }

    public async Task<UserDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        _currentUser.EnsureAdmin();
        return MapToDto(await GetExistingAsync(id, cancellationToken));
    }

    public async Task<UserDto> CreateAsync(CreateUserDto dto, CancellationToken cancellationToken)
    {
        _currentUser.EnsureAdmin();

        var username = InputText.Required(dto.Username);
        var role = dto.Role!.Value;
        PasswordPolicy.EnsureValid(dto.Password);

        if (await _userRepository.UsernameExistsAsync(username, null, cancellationToken))
        {
            throw new ConflictException($"Username '{username}' is already in use.");
        }

        var employee = await ValidateEmployeeLinkAsync(role, dto.EmployeeId, null, cancellationToken);

        var user = new ApplicationUser
        {
            Username = username,
            PasswordHash = _passwordHasher.Hash(dto.Password),
            Role = role,
            EmployeeId = employee?.Id,
            Employee = employee,
        };

        await _userRepository.AddAsync(user, cancellationToken);
        _auditLogger.Add(AuditActions.UserCreated, nameof(ApplicationUser), user.Id, $"Username '{username}', role {role}.");
        await _userRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(user);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserDto dto, CancellationToken cancellationToken)
    {
        _currentUser.EnsureAdmin();

        var user = await GetExistingAsync(id, cancellationToken);
        var role = dto.Role!.Value;
        var isActive = dto.IsActive!.Value;

        var losesAdmin = user.Role == UserRole.Admin && user.IsActive && (role != UserRole.Admin || !isActive);
        if (losesAdmin && user.Id == _currentUser.UserId)
        {
            throw new BadRequestException("You can't remove your own Admin role or deactivate your own account.");
        }

        if (losesAdmin && await _userRepository.CountActiveAdminsAsync(user.Id, cancellationToken) == 0)
        {
            throw new BadRequestException("This is the last active Admin account; another Admin must exist first.");
        }

        var employee = await ValidateEmployeeLinkAsync(role, dto.EmployeeId, user.Id, cancellationToken);

        var sessionChanged = user.Role != role || user.IsActive != isActive || user.EmployeeId != employee?.Id;
        var details = $"Role {user.Role} -> {role}, active {user.IsActive} -> {isActive}.";

        user.Role = role;
        user.IsActive = isActive;
        user.EmployeeId = employee?.Id;
        user.Employee = employee;
        user.UpdatedAt = DateTime.UtcNow;
        if (sessionChanged)
        {
            user.SecurityStamp = Guid.NewGuid().ToString("N");
        }

        _auditLogger.Add(AuditActions.UserUpdated, nameof(ApplicationUser), user.Id, details);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(user);
    }

    public async Task ResetPasswordAsync(Guid id, ResetPasswordDto dto, CancellationToken cancellationToken)
    {
        _currentUser.EnsureAdmin();

        var user = await GetExistingAsync(id, cancellationToken);
        PasswordPolicy.EnsureValid(dto.NewPassword);

        user.PasswordHash = _passwordHasher.Hash(dto.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.FailedLoginCount = 0;
        user.LockoutEndAt = null;
        user.UpdatedAt = DateTime.UtcNow;

        _auditLogger.Add(AuditActions.PasswordReset, nameof(ApplicationUser), user.Id);
        await _userRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> EnsureBootstrapAdminAsync(string? username, string? password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password) || await _userRepository.AnyAdminAsync(cancellationToken))
        {
            return false;
        }

        PasswordPolicy.EnsureValid(password);
        username = username.Trim();

        if (await _userRepository.UsernameExistsAsync(username, null, cancellationToken))
        {
            throw new InvalidOperationException($"The bootstrap Admin username '{username}' is already used by a non-Admin account.");
        }

        var user = new ApplicationUser
        {
            Username = username,
            PasswordHash = _passwordHasher.Hash(password),
            Role = UserRole.Admin,
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _auditLogger.WriteAsync(AuditActions.UserCreated, nameof(ApplicationUser), user.Id, "Bootstrap Admin created from configuration.", null, "system", cancellationToken);

        return true;
    }

    private async Task<Employee?> ValidateEmployeeLinkAsync(UserRole role, Guid? employeeId, Guid? userId, CancellationToken cancellationToken)
    {
        if (employeeId is null || employeeId == Guid.Empty)
        {
            if (role is UserRole.Employee or UserRole.Manager)
            {
                throw new BadRequestException($"A {role} account must be linked to an employee.");
            }

            return null;
        }

        var employee = await _employeeRepository.GetByIdAsync(employeeId.Value, cancellationToken)
            ?? throw new BadRequestException($"Employee with id '{employeeId}' does not exist.");

        if (await _userRepository.EmployeeHasUserAsync(employee.Id, userId, cancellationToken))
        {
            throw new ConflictException($"Employee '{employee.EmployeeCode}' already has an account.");
        }

        return employee;
    }

    private async Task<ApplicationUser> GetExistingAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"User with id '{id}' was not found.");
    }

    private static UserDto MapToDto(ApplicationUser user)
    {
        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Role = user.Role.ToString(),
            IsActive = user.IsActive,
            EmployeeId = user.EmployeeId,
            EmployeeCode = user.Employee?.EmployeeCode,
            EmployeeName = user.Employee is null ? null : $"{user.Employee.FirstName} {user.Employee.LastName}".Trim(),
            LastLoginAt = user.LastLoginAt,
            LockoutEndAt = user.LockoutEndAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
        };
    }
}
