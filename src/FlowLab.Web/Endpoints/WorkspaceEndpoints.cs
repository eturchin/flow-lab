using FlowLab.Infrastructure;

namespace FlowLab.Web;

public static class WorkspaceEndpoints
{
    public static IEndpointRouteBuilder MapWorkspaceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workspaces").WithTags("Workspaces");
        group.MapGet("/", async (WorkspaceStore store, CancellationToken ct) => Results.Ok(await store.ListAsync(ct)));
        group.MapPost("/", async (WorkspaceInput input, WorkspaceStore store, CancellationToken ct) =>
        {
            var workspace = await store.SaveAsync(null, input, ct);
            return Results.Created($"/api/workspaces/{workspace.Id}", workspace);
        });
        group.MapPut("/{id:guid}", async (Guid id, WorkspaceInput input, WorkspaceStore store, CancellationToken ct) => Results.Ok(await store.SaveAsync(id, input, ct)));
        group.MapDelete("/{id:guid}", async (Guid id, WorkspaceStore store, CancellationToken ct) => await store.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound());
        return app;
    }
}
