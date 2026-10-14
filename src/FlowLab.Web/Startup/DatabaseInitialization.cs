using FlowLab.Application;
using FlowLab.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FlowLab.Web;

public static class DatabaseInitialization
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LabDbContext>();
        var initialized = (await db.Database.GetAppliedMigrationsAsync(ct)).Any();
        await db.Database.MigrateAsync(ct);

        var store = scope.ServiceProvider.GetRequiredService<ILabStore>();
        await store.RecoverInterruptedRunsAsync(ct);
        if (initialized || (await store.ListScenariosAsync(ct)).Count != 0 || !await db.Workspaces.AnyAsync(ct))
        {
            return;
        }

        var scenarios = scope.ServiceProvider.GetRequiredService<ScenarioService>();
        foreach (var input in DemoScenarios.Create())
        {
            await scenarios.SaveAsync(null, input, ct);
        }
    }
}
