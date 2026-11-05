using System.Security.Claims;
using JewelleryOrderbook.Api.Extensions;

namespace JewelleryOrderbook.Api.Endpoints;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me", (ClaimsPrincipal user) => Results.Ok(new
            {
                subject = user.SubjectId(),
                name = user.Identity?.Name,
                roles = user.FindAll("roles").Select(c => c.Value),
                authenticated = user.Identity?.IsAuthenticated ?? false
            }))
            .WithTags("identity")
            .RequireAuthorization()
            .WithName("GetCurrentUser");

        return app;
    }
}

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health/live", () => Results.Ok(new { status = "live" }))
            .WithTags("health")
            .AllowAnonymous()
            .WithName("Liveness");

        app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }))
            .WithTags("health")
            .AllowAnonymous()
            .WithName("Readiness");

        return app;
    }
}
