using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace JewelleryOrderbook.Api.Extensions;

public static class AuthenticationExtensions
{
    /// <summary>
    /// Wires Entra ID as the only identity provider.
    /// The SPA is a public client using authorization code + PKCE and never holds a secret;
    /// this API is a resource server that validates the resulting bearer token locally.
    /// </summary>
    public static WebApplicationBuilder AddEntraAuthentication(
        this WebApplicationBuilder builder,
        IConfiguration configuration)
    {
        var tenantId = configuration["Entra:TenantId"];
        var audience = configuration["Entra:Audience"];
        var configured = !string.IsNullOrWhiteSpace(tenantId) && !string.IsNullOrWhiteSpace(audience);

        if (!configured && !builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Entra:TenantId and Entra:Audience must be configured outside Development.");
        }

        if (configured)
        {
            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
                    options.Audience = audience;
                    options.TokenValidationParameters = new()
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(30),
                        RoleClaimType = "roles",
                        NameClaimType = "name"
                    };
                });
        }
        else
        {
            // Local development without an app registration yet.
            builder.Services
                .AddAuthentication(DevelopmentAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(
                    DevelopmentAuthenticationHandler.SchemeName, _ => { });
        }

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.Owner, p => p.RequireRole("Owner"))
            .AddPolicy(AuthorizationPolicies.Write, p => p.RequireRole("Owner", "Accountant"))
            .AddPolicy(AuthorizationPolicies.Read, p => p.RequireRole("Owner", "Accountant", "Viewer"));

        return builder;
    }
}

public static class AuthorizationPolicies
{
    public const string Owner = "Owner";
    public const string Write = "Write";
    public const string Read = "Read";
}

public static class ClaimsPrincipalExtensions
{
    public static string SubjectId(this ClaimsPrincipal user) =>
        user.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier")
        ?? user.FindFirstValue("oid")
        ?? user.Identity?.Name
        ?? "anonymous";
}
