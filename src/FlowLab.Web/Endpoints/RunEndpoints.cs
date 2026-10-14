using FlowLab.Application;

namespace FlowLab.Web;

public static class RunEndpoints
{
    public static IEndpointRouteBuilder MapRunEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Runs");
        group.MapGet("/scenarios/{id:guid}/runs", async (Guid id, RunService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(id, ct)));
        group.MapPost("/scenarios/{id:guid}/runs", async (Guid id, RunService service, CancellationToken ct) =>
        {
            var run = await service.StartAsync(id, ct);
            return run is null
                ? Results.Problem(statusCode: 429, title: "Очередь заполнена", detail: "Максимум 10 активных и ожидающих запусков. Повторите позже.")
                : Results.Accepted($"/api/runs/{run.Id}", run);
        });
        group.MapGet("/runs/{id:guid}", async (Guid id, RunService service, CancellationToken ct) =>
            await service.GetAsync(id, ct) is { } run ? Results.Ok(run) : Results.NotFound());
        group.MapPost("/runs/{id:guid}/cancel", async (Guid id, RunService service, CancellationToken ct) =>
            await service.CancelAsync(id, ct) ? Results.Accepted() : Results.Conflict(new { detail = "Запуск уже завершён." }));
        group.MapGet("/runs/compare", async (Guid baseline, Guid candidate, RunService service, CancellationToken ct) =>
            Results.Ok(await service.CompareAsync(baseline, candidate, ct)));
        group.MapGet("/runs/{id:guid}/failures", async (Guid id, RunService service, CancellationToken ct, int offset = 0, int limit = 50) =>
            Results.Ok(await service.GetFailuresAsync(id, offset, limit, ct)));
        return app;
    }
}
