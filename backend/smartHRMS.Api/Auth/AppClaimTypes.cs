namespace smartHRMS.Api.Auth;

/// <summary>Claim names inside SmartHRMS access tokens (short JWT names; inbound claim mapping is off).</summary>
public static class AppClaimTypes
{
    public const string UserId = "sub";
    public const string Username = "name";
    public const string Role = "role";
    public const string EmployeeId = "employee_id";
    public const string SecurityStamp = "stamp";
}
