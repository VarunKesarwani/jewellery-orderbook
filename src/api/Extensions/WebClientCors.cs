namespace JewelleryOrderbook.Api.Extensions;

public static class WebClientCors
{
    public const string PolicyName = "web-client";

    /// <summary>
    /// The Angular app is served from a different origin in development.
    /// In production both sit behind the same reverse proxy, so the list stays empty.
    /// </summary>
    public static IServiceCollection AddCorsForWebClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                      ?? ["http://localhost:4200"];

        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy => policy
                .WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod());
        });

        return services;
    }
}
