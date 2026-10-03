namespace smartHRMS.Api.Auth;

/// <summary>
/// Authorization policy names. Every endpoint requires a signed-in user unless marked [AllowAnonymous]; these add role
/// requirements on top. Services check ownership and roles again, so a missing attribute can't open data up.
/// </summary>
public static class Policies
{
    public const string HrOrAdmin = "HrOrAdmin";
    public const string Admin = "Admin";
}
