using FlowLab.Application;
using FlowLab.Domain;

namespace FlowLab.Web;

public static class ScenarioEndpoints
{
    public static IEndpointRouteBuilder MapScenarioEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/scenarios").WithTags("Scenarios");
        group.MapGet("/", async (ScenarioService service, CancellationToken ct) => Results.Ok(await service.ListAsync(ct)));
        group.MapGet("/{id:guid}", async (Guid id, ScenarioService service, CancellationToken ct) =>
            await service.GetAsync(id, ct) is { } scenario ? Results.Ok(scenario) : Results.NotFound());
        group.MapPost("/", async (ScenarioInput input, ScenarioService service, CancellationToken ct) =>
        {
            var scenario = await service.SaveAsync(null, input, ct);
            return Results.Created($"/api/scenarios/{scenario.Id}", scenario);
        });
        group.MapPut("/{id:guid}", async (Guid id, ScenarioInput input, ScenarioService service, CancellationToken ct) =>
            Results.Ok(await service.SaveAsync(id, input, ct)));
        group.MapDelete("/{id:guid}", async (Guid id, ScenarioService service, CancellationToken ct) =>
            await service.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound());
        return app;
    }
}
