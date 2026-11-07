using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace JewelleryOrderbook.Api.Extensions;

public static class AuthenticationExtensions
{
    /// <summary>
    /// Wires Entra ID as the only identity provider.
    ///
    /// The SPA is a public client: authorization code + PKCE, no secret, token kept in the browser.
    /// This API is a resource server: it validates the bearer token locally against the tenant's
    /// published signing keys and never calls Microsoft per request.
    ///
    /// Required configuration (appsettings or user secrets):
    ///   Entra:TenantId   the tenant GUID
    ///   Entra:Audience   the API app registration's Application ID URI, e.g. api://&lt;guid&gt;
    ///   Entra:ClientId   optional; the API app registration's client id, used for logging only
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
                "Entra:TenantId and Entra:Audience must be configured outside Development. " +
                "Refusing to start an unprotected API.");
        }

        if (configured)
        {
            var authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";

            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = authority;
                    options.Audience = audience;
                    options.RequireHttpsMetadata = true;
                    options.SaveToken = false;

                    options.TokenValidationParameters = new()
                    {
                        // Pinned rather than taken from the discovery document, so a misconfigured
                        // tenant cannot widen what this API accepts.
                        ValidIssuer = authority,
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        RequireExpirationTime = true,
                        RequireSignedTokens = true,
                        ClockSkew = TimeSpan.FromSeconds(30),
                        RoleClaimType = "roles",
                        NameClaimType = "name"
                    };

                    options.Events = new JwtBearerEvents
                    {
                        // A 401 with no reason is the hardest thing to debug during setup,
                        // so every failure is logged with the exception attached.
                        OnAuthenticationFailed = context =>
                        {
                            var logger = context.HttpContext.RequestServices
                                .GetRequiredService<ILoggerFactory>()
                                .CreateLogger("JewelleryOrderbook.Auth");

                            logger.LogWarning(
                                context.Exception,
                                "Bearer token rejected for {Path}",
                                context.HttpContext.Request.Path);

                            return Task.CompletedTask;
                        },
                        OnTokenValidated = context =>
                        {
                            var logger = context.HttpContext.RequestServices
                                .GetRequiredService<ILoggerFactory>()
                                .CreateLogger("JewelleryOrderbook.Auth");

                            var subject = context.Principal?.FindFirstValue("oid");
                            var roles = context.Principal?.FindAll("roles").Select(c => c.Value) ?? [];

                            logger.LogInformation(
                                "Token accepted for subject {Subject} with roles {Roles}",
                                subject,
                                string.Join(",", roles));

                            return Task.CompletedTask;
                        }
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
    /// <summary>
    /// Entra puts the stable object id in <c>oid</c>; the long URI form appears in v1 tokens.
    /// The subject is what the API keys its own data on, never the email.
    /// </summary>
    public static string SubjectId(this ClaimsPrincipal user) =>
        user.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier")
        ?? user.FindFirstValue("oid")
        ?? user.Identity?.Name
        ?? "anonymous";
}
