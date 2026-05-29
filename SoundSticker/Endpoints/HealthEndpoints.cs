using SoundSticker.Contracts;

namespace SoundSticker.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/health", () => Results.Ok(new HealthResponse("ok", DateTimeOffset.UtcNow)))
            .WithName("Health");

        return endpoints;
    }
}
