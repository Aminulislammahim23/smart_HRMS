using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using smartHRMS.Application.Common.Security;
using smartHRMS.Application.Features.Auth;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Api.Auth;

public static class AuthenticationSetup
{
    private const int MinKeyLength = 32;

    /// <summary>
    /// JWT bearer authentication plus a fallback policy that requires a signed-in user on every endpoint. Each request's
    /// token is also checked against the database (account active, security stamp current), so deactivating a user or
    /// changing their role or password takes effect immediately.
    /// </summary>
    public static IServiceCollection AddAppAuthentication(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment, ILogger logger)
    {
        var options = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        var key = CreateSigningKey(options, environment, logger);

        if (options.AccessTokenMinutes is < 5 or > 24 * 60)
        {
            throw new InvalidOperationException($"{JwtOptions.SectionName}:AccessTokenMinutes must be between 5 and 1440.");
        }

        services.AddSingleton(options);
        services.AddSingleton(key);
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = options.Issuer,
                    ValidAudience = options.Audience,
                    IssuerSigningKey = key,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = AppClaimTypes.Username,
                    RoleClaimType = AppClaimTypes.Role,
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
                jwt.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal!;
                        var authService = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
                        var valid = Guid.TryParse(principal.FindFirst(AppClaimTypes.UserId)?.Value, out var userId)
                            && await authService.IsSessionValidAsync(userId, principal.FindFirst(AppClaimTypes.SecurityStamp)?.Value, context.HttpContext.RequestAborted);

                        if (!valid)
                        {
                            context.Fail("The session is no longer valid.");
                        }
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.HrOrAdmin, policy => policy.RequireRole(nameof(UserRole.HR), nameof(UserRole.Admin)))
            .AddPolicy(Policies.Admin, policy => policy.RequireRole(nameof(UserRole.Admin)));

        return services;
    }

    /// <summary>
    /// Creates the first Admin from "Auth:BootstrapAdmin:Username" and ":Password" (user-secrets or environment variables)
    /// when no Admin account exists yet. Does nothing otherwise.
    /// </summary>
    public static async Task EnsureBootstrapAdminAsync(this WebApplication app)
    {
        var username = app.Configuration["Auth:BootstrapAdmin:Username"];
        var password = app.Configuration["Auth:BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<smartHRMS.Application.Features.Users.IUserService>();
        if (await userService.EnsureBootstrapAdminAsync(username, password, CancellationToken.None))
        {
            app.Logger.LogWarning("Created the bootstrap Admin account '{Username}'. Sign in, create named accounts, and remove Auth:BootstrapAdmin from configuration.", username.Trim());
        }
    }

    private static SymmetricSecurityKey CreateSigningKey(JwtOptions options, IHostEnvironment environment, ILogger logger)
    {
        if (!string.IsNullOrEmpty(options.SigningKey))
        {
            if (options.SigningKey.Length < MinKeyLength)
            {
                throw new InvalidOperationException($"{JwtOptions.SectionName}:SigningKey must be at least {MinKeyLength} characters.");
            }

            return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException($"{JwtOptions.SectionName}:SigningKey is not configured. Set it through an environment variable or a secret store.");
        }

        // Development only: a random key per process. Everyone is signed out when the API restarts.
        logger.LogWarning("{Section}:SigningKey is not configured; using a temporary key. Sign-ins end when the API restarts.", JwtOptions.SectionName);
        return new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(64));
    }
}
