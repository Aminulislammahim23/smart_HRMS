using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Common.Security;
using smartHRMS.Application.Features.Auth.Dtos;
using smartHRMS.Application.Features.Employees;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Application.Features.Auth;

/// <summary>
/// Sign-in rules:
/// - an unknown username, a wrong password and an inactive account all get the same message;
/// - <see cref="AuthOptions.LockoutThreshold"/> wrong passwords in a row lock the account for
///   <see cref="AuthOptions.LockoutMinutes"/> minutes;
/// - an account whose employee no longer works here (Inactive, Resigned, Terminated) can't sign in;
/// - every success and failure is written to the audit log.
/// </summary>
public class AuthService : IAuthService
{
    private const string InvalidCredentials = "Invalid username or password.";

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly AuthOptions _options;

    // Verified when the username is unknown, so that case takes as long as a wrong password.
    private readonly Lazy<string> _dummyHash;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IAuditLogger auditLogger,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        AuthOptions options)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _options = options;
        _dummyHash = new Lazy<string>(() => passwordHasher.Hash(Guid.NewGuid().ToString()));
    }

    public async Task<LoginResultDto> LoginAsync(LoginDto dto, CancellationToken cancellationToken)
    {
        var username = dto.Username.Trim();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var user = await _userRepository.GetByUsernameAsync(username, cancellationToken);

        if (user is null)
        {
            _passwordHasher.Verify(dto.Password, _dummyHash.Value);
            await _auditLogger.WriteAsync(AuditActions.LoginFailed, nameof(ApplicationUser), null, "Unknown username.", null, username, cancellationToken);
            throw new UnauthorizedException(InvalidCredentials);
        }

        if (user.LockoutEndAt > now)
        {
            await _auditLogger.WriteAsync(AuditActions.LoginFailed, nameof(ApplicationUser), user.Id, "Account locked.", user.Id, user.Username, cancellationToken);
            throw new UnauthorizedException("Too many failed sign-in attempts. Try again later.");
        }

        if (!_passwordHasher.Verify(dto.Password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            var details = "Wrong password.";
            if (user.FailedLoginCount >= _options.LockoutThreshold)
            {
                user.LockoutEndAt = now.AddMinutes(_options.LockoutMinutes);
                user.FailedLoginCount = 0;
                details = $"Wrong password; account locked for {_options.LockoutMinutes} minutes.";
            }

            _auditLogger.Add(AuditActions.LoginFailed, nameof(ApplicationUser), user.Id, details);
            await _userRepository.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedException(InvalidCredentials);
        }

        if (!CanSignIn(user))
        {
            await _auditLogger.WriteAsync(AuditActions.LoginFailed, nameof(ApplicationUser), user.Id, "Account inactive or employee no longer current.", user.Id, user.Username, cancellationToken);
            throw new UnauthorizedException(InvalidCredentials);
        }

        user.FailedLoginCount = 0;
        user.LockoutEndAt = null;
        user.LastLoginAt = now;
        await _auditLogger.WriteAsync(AuditActions.LoginSucceeded, nameof(ApplicationUser), user.Id, null, user.Id, user.Username, cancellationToken);

        var (token, expiresAt) = _tokenService.CreateAccessToken(user);
        return new LoginResultDto { AccessToken = token, ExpiresAt = expiresAt, User = MapCurrentUser(user) };
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(_currentUser.RequireUserId(), cancellationToken)
            ?? throw new UnauthorizedException("Authentication is required.");

        return MapCurrentUser(user);
    }

    public async Task ChangePasswordAsync(ChangePasswordDto dto, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(_currentUser.RequireUserId(), cancellationToken)
            ?? throw new UnauthorizedException("Authentication is required.");

        if (!_passwordHasher.Verify(dto.CurrentPassword, user.PasswordHash))
        {
            throw new BadRequestException("The current password is incorrect.");
        }

        PasswordPolicy.EnsureValid(dto.NewPassword);
        if (dto.NewPassword == dto.CurrentPassword)
        {
            throw new BadRequestException("The new password must be different from the current password.");
        }

        user.PasswordHash = _passwordHasher.Hash(dto.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.UpdatedAt = DateTime.UtcNow;
        _auditLogger.Add(AuditActions.PasswordChanged, nameof(ApplicationUser), user.Id);

        await _userRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsSessionValidAsync(Guid userId, string? securityStamp, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        return user is not null && user.SecurityStamp == securityStamp && CanSignIn(user);
    }

    private static bool CanSignIn(ApplicationUser user)
    {
        return user.IsActive && (user.Employee is null || EmployeeStatusRules.CurrentStatuses.Contains(user.Employee.Status));
    }

    internal static CurrentUserDto MapCurrentUser(ApplicationUser user)
    {
        return new CurrentUserDto
        {
            Id = user.Id,
            Username = user.Username,
            Role = user.Role.ToString(),
            EmployeeId = user.EmployeeId,
            EmployeeCode = user.Employee?.EmployeeCode,
            DisplayName = user.Employee is null ? user.Username : $"{user.Employee.FirstName} {user.Employee.LastName}".Trim(),
        };
    }
}
