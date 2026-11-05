using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace JewelleryOrderbook.Api.Extensions;

/// <summary>
/// Lets the API run locally before an Entra app registration exists.
/// Registered only when the environment is Development and no tenant is configured,
/// and it never returns a challenge, so every request is authenticated as a local owner.
/// </summary>
public sealed class DevelopmentAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Development";
    private const string RoleClaimType = "roles";

    public DevelopmentAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new System.Security.Claims.Claim("oid", "dev-user"),
            new System.Security.Claims.Claim("name", "Development User"),
            new System.Security.Claims.Claim(RoleClaimType, "Owner")
        };

        var identity = new System.Security.Claims.ClaimsIdentity(claims, SchemeName, "name", RoleClaimType);
        var ticket = new AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
