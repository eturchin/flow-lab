using FlowLab.Domain;

namespace FlowLab.Application;

public sealed class RunService(ILabStore store, IRunScheduler scheduler)
{
    public Task<RunDto?> GetAsync(Guid id, CancellationToken ct) => store.GetRunAsync(id, ct);
    public async Task<List<RunDto>> ListAsync(Guid scenarioId, CancellationToken ct)
    {
        _ = await store.GetScenarioAsync(scenarioId, ct) ?? throw new KeyNotFoundException("Сценарий не найден.");
        return await store.ListRunsAsync(scenarioId, ct);
    }

    public async Task<RunDto?> StartAsync(Guid scenarioId, CancellationToken ct)
    {
        if (!scheduler.TryReserve())
        {
            return null;
        }

        RunDto run;
        try { run = await store.CreateRunAsync(scenarioId, ct); }
        catch
        {
            scheduler.ReleaseReservation();
            throw;
        }
        try { scheduler.Enqueue(run.Id); }
        catch
        {
            scheduler.ReleaseReservation();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await store.UpdateRunAsync(run.Id, RunStatus.Failed, 0, error: "Не удалось поставить запуск в очередь.", ct: timeout.Token);
            throw;
        }
        return run;
    }

    public async Task<bool> CancelAsync(Guid id, CancellationToken ct)
    {
        _ = await store.GetRunAsync(id, ct) ?? throw new KeyNotFoundException("Запуск не найден.");
        return scheduler.Cancel(id);
    }

    public async Task<ComparisonDto> CompareAsync(Guid baseline, Guid candidate, CancellationToken ct)
    {
        var a = await store.GetRunAsync(baseline, ct) ?? throw new KeyNotFoundException("Исходный запуск не найден.");
        var b = await store.GetRunAsync(candidate, ct) ?? throw new KeyNotFoundException("Сравниваемый запуск не найден.");
        return RunComparison.Compare(a, b);
    }

    public async Task<FailurePageDto> GetFailuresAsync(Guid id, int offset, int limit, CancellationToken ct)
    {
        if (offset < 0 || limit is < 1 or > 200)
        {
            throw new ArgumentException("Offset должен быть неотрицательным, limit — от 1 до 200.");
        }

        var run = await store.GetRunAsync(id, ct) ?? throw new KeyNotFoundException("Запуск не найден.");
        if (run.Result is null)
        {
            throw new InvalidOperationException("Журнал ошибок доступен после завершения симуляции.");
        }

        return new(run.Result.Failed, offset, limit, await store.GetFailuresAsync(id, offset, limit, ct));
    }
}
