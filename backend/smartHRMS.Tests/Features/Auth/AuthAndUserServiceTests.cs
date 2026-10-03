using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Features.Auth;
using smartHRMS.Application.Features.Auth.Dtos;
using smartHRMS.Application.Features.Employees;
using smartHRMS.Application.Features.Employees.Dtos;
using smartHRMS.Application.Features.Users;
using smartHRMS.Application.Features.Users.Dtos;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;
using smartHRMS.Tests.Fakes;
using Xunit;

namespace smartHRMS.Tests.Features.Auth;

public class AuthAndUserServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 6, 0, 0, TimeSpan.Zero);

    private sealed class Setup
    {
        public FakeUserRepository Users { get; } = new();
        public FakeEmployeeRepository Employees { get; } = new();
        public FakeAuditLogger Audit { get; } = new();
        public FakeCurrentUser User { get; } = new();
        public FakeTimeProvider Clock { get; } = new(Now);
        public AuthService Auth { get; }
        public UserService UserService { get; }
        public Employee Employee { get; }

        public Setup()
        {
            Employee = new Employee { EmployeeCode = "EMP-001", FirstName = "Jane", LastName = "Doe", Status = EmployeeStatus.Active };
            Employees.Employees.Add(Employee);
            Auth = new AuthService(Users, new FakePasswordHasher(), new FakeTokenService(), Audit, User, Clock, new AuthOptions { LockoutThreshold = 3, LockoutMinutes = 15 });
            UserService = new UserService(Users, Employees, new FakePasswordHasher(), Audit, User);
        }

        public ApplicationUser AddUser(string username, string password, UserRole role, Employee? employee = null)
        {
            var user = new ApplicationUser { Username = username, PasswordHash = "hashed:" + password, Role = role, EmployeeId = employee?.Id, Employee = employee };
            Users.Users.Add(user);
            return user;
        }

        public Task<LoginResultDto> LoginAsync(string username, string password) =>
            Auth.LoginAsync(new LoginDto { Username = username, Password = password }, CancellationToken.None);
    }

    // ---- sign-in ----

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenAndUser()
    {
        var s = new Setup();
        s.AddUser("jane", "Secret123", UserRole.Employee, s.Employee);

        var result = await s.LoginAsync("JANE", "Secret123");

        Assert.Equal("token-for-jane", result.AccessToken);
        Assert.Equal("Employee", result.User.Role);
        Assert.Equal(s.Employee.Id, result.User.EmployeeId);
        Assert.Equal("Jane Doe", result.User.DisplayName);
        Assert.Contains(s.Audit.Entries, e => e.Action == "LoginSucceeded");
    }

    [Fact]
    public async Task Login_UnknownUserAndWrongPassword_GiveTheSameMessage()
    {
        var s = new Setup();
        s.AddUser("jane", "Secret123", UserRole.Employee, s.Employee);

        var unknown = await Assert.ThrowsAsync<UnauthorizedException>(() => s.LoginAsync("nobody", "Secret123"));
        var wrong = await Assert.ThrowsAsync<UnauthorizedException>(() => s.LoginAsync("jane", "wrong1234"));

        Assert.Equal(unknown.Message, wrong.Message);
    }

    [Fact]
    public async Task Login_LocksAccountAfterRepeatedFailures_EvenWithTheRightPassword()
    {
        var s = new Setup();
        var user = s.AddUser("jane", "Secret123", UserRole.Employee, s.Employee);

        for (var i = 0; i < 3; i++)
        {
            await Assert.ThrowsAsync<UnauthorizedException>(() => s.LoginAsync("jane", "wrong1234"));
        }

        Assert.NotNull(user.LockoutEndAt);
        var locked = await Assert.ThrowsAsync<UnauthorizedException>(() => s.LoginAsync("jane", "Secret123"));
        Assert.Contains("Too many", locked.Message);

        s.Clock.UtcNow = Now.AddMinutes(16);
        Assert.NotNull(await s.LoginAsync("jane", "Secret123"));
    }

    [Fact]
    public async Task Login_InactiveAccountOrFormerEmployee_IsRefused()
    {
        var s = new Setup();
        var user = s.AddUser("jane", "Secret123", UserRole.Employee, s.Employee);

        user.IsActive = false;
        await Assert.ThrowsAsync<UnauthorizedException>(() => s.LoginAsync("jane", "Secret123"));

        user.IsActive = true;
        s.Employee.Status = EmployeeStatus.Resigned;
        await Assert.ThrowsAsync<UnauthorizedException>(() => s.LoginAsync("jane", "Secret123"));
    }

    [Fact]
    public async Task Session_EndsWhenSecurityStampChanges()
    {
        var s = new Setup();
        var user = s.AddUser("jane", "Secret123", UserRole.Employee, s.Employee);
        var stamp = user.SecurityStamp;

        Assert.True(await s.Auth.IsSessionValidAsync(user.Id, stamp, CancellationToken.None));

        s.User.SignInAs(UserRole.Employee, s.Employee.Id);
        s.User.UserId = user.Id;
        await s.Auth.ChangePasswordAsync(new ChangePasswordDto { CurrentPassword = "Secret123", NewPassword = "NewSecret456" }, CancellationToken.None);

        Assert.False(await s.Auth.IsSessionValidAsync(user.Id, stamp, CancellationToken.None));
        Assert.True(await s.Auth.IsSessionValidAsync(user.Id, user.SecurityStamp, CancellationToken.None));
    }

    [Fact]
    public async Task ChangePassword_RequiresCurrentPassword_AndPolicy()
    {
        var s = new Setup();
        var user = s.AddUser("jane", "Secret123", UserRole.Employee, s.Employee);
        s.User.SignInAs(UserRole.Employee, s.Employee.Id);
        s.User.UserId = user.Id;

        await Assert.ThrowsAsync<BadRequestException>(() => s.Auth.ChangePasswordAsync(new ChangePasswordDto { CurrentPassword = "nope", NewPassword = "NewSecret456" }, CancellationToken.None));
        await Assert.ThrowsAsync<BadRequestException>(() => s.Auth.ChangePasswordAsync(new ChangePasswordDto { CurrentPassword = "Secret123", NewPassword = "short" }, CancellationToken.None));
        await Assert.ThrowsAsync<BadRequestException>(() => s.Auth.ChangePasswordAsync(new ChangePasswordDto { CurrentPassword = "Secret123", NewPassword = "lettersonly" }, CancellationToken.None));
    }

    // ---- user administration ----

    [Fact]
    public async Task OnlyAdminsManageUsers()
    {
        var s = new Setup();
        s.User.SignInAs(UserRole.HR);

        await Assert.ThrowsAsync<ForbiddenException>(() => s.UserService.GetAllAsync(CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.UserService.CreateAsync(new CreateUserDto { Username = "x", Password = "Secret123", Role = UserRole.HR }, CancellationToken.None));
    }

    [Fact]
    public async Task CreateUser_EmployeeRoleNeedsEmployee_AndOneAccountPerEmployee()
    {
        var s = new Setup();
        s.User.SignInAs(UserRole.Admin);

        await Assert.ThrowsAsync<BadRequestException>(() => s.UserService.CreateAsync(new CreateUserDto { Username = "jane", Password = "Secret123", Role = UserRole.Employee }, CancellationToken.None));

        var created = await s.UserService.CreateAsync(new CreateUserDto { Username = "jane", Password = "Secret123", Role = UserRole.Employee, EmployeeId = s.Employee.Id }, CancellationToken.None);
        Assert.Equal("EMP-001", created.EmployeeCode);
        Assert.NotEqual("Secret123", s.Users.Users.Single().PasswordHash);

        await Assert.ThrowsAsync<ConflictException>(() => s.UserService.CreateAsync(new CreateUserDto { Username = "jane2", Password = "Secret123", Role = UserRole.Employee, EmployeeId = s.Employee.Id }, CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() => s.UserService.CreateAsync(new CreateUserDto { Username = "JANE", Password = "Secret123", Role = UserRole.HR }, CancellationToken.None));
    }

    [Fact]
    public async Task LastActiveAdmin_CannotBeDemotedOrDeactivated()
    {
        var s = new Setup();
        var admin = s.AddUser("root", "Secret123", UserRole.Admin);
        var other = s.AddUser("boss", "Secret123", UserRole.Admin);
        s.User.SignInAs(UserRole.Admin);
        s.User.UserId = admin.Id;

        // Can't demote yourself.
        await Assert.ThrowsAsync<BadRequestException>(() => s.UserService.UpdateAsync(admin.Id, new UpdateUserDto { Role = UserRole.HR, IsActive = true }, CancellationToken.None));

        // Demoting the other admin is fine while one remains, and it ends their sessions.
        var stamp = other.SecurityStamp;
        await s.UserService.UpdateAsync(other.Id, new UpdateUserDto { Role = UserRole.HR, IsActive = true }, CancellationToken.None);
        Assert.NotEqual(stamp, other.SecurityStamp);

        s.User.UserId = Guid.NewGuid();
        await Assert.ThrowsAsync<BadRequestException>(() => s.UserService.UpdateAsync(admin.Id, new UpdateUserDto { Role = UserRole.Admin, IsActive = false }, CancellationToken.None));
    }

    [Fact]
    public async Task BootstrapAdmin_IsCreatedOnlyWhenNoAdminExists()
    {
        var s = new Setup();

        Assert.True(await s.UserService.EnsureBootstrapAdminAsync("admin", "Secret123", CancellationToken.None));
        Assert.False(await s.UserService.EnsureBootstrapAdminAsync("admin2", "Secret123", CancellationToken.None));
        Assert.Equal(UserRole.Admin, Assert.Single(s.Users.Users).Role);
        Assert.False(await new Setup().UserService.EnsureBootstrapAdminAsync(null, null, CancellationToken.None));
    }

    // ---- manager assignment ----

    [Fact]
    public async Task Manager_CannotBeSelf_OrCreateALoop()
    {
        var s = new Setup();
        var a = s.Employee;
        var b = new Employee { EmployeeCode = "EMP-002", FirstName = "B", LastName = "X", Status = EmployeeStatus.Active, ManagerId = a.Id };
        s.Employees.Employees.Add(b);
        s.User.SignInAs(UserRole.HR);
        var service = new EmployeeManagerService(s.Employees, s.Audit, s.User);

        await Assert.ThrowsAsync<BadRequestException>(() => service.AssignAsync(a.Id, new AssignManagerDto { ManagerId = a.Id }, CancellationToken.None));
        await Assert.ThrowsAsync<BadRequestException>(() => service.AssignAsync(a.Id, new AssignManagerDto { ManagerId = b.Id }, CancellationToken.None));

        s.User.SignInAs(UserRole.Employee, a.Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.AssignAsync(b.Id, new AssignManagerDto { ManagerId = null }, CancellationToken.None));
    }
}
