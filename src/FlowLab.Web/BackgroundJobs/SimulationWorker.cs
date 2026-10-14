using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using System.Data.Common;
using FlowLab.Application;
using FlowLab.Domain;
using FlowLab.Simulation;
using Microsoft.AspNetCore.SignalR;

namespace FlowLab.Web;

public sealed class SimulationWorker(RunQueue queue, IServiceScopeFactory scopes, IHubContext<SimulationHub> hub, ILogger<SimulationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.WhenAll(Consume(stoppingToken), Consume(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Closing an idle channel reader is normal during host shutdown or a failed startup.
        }
    }
    private async Task Consume(CancellationToken stoppingToken)
    {
        await foreach (var id in queue.Reader.ReadAllAsync(stoppingToken))
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, queue.Token(id));
            try
            {
                linked.Token.ThrowIfCancellationRequested();
                await Set(id, RunStatus.Running, 0);
                using var scope = scopes.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<ILabStore>();
                var run = await store.GetRunAsync(id, stoppingToken); if (run is null)
                {
                    continue;
                }

                var result = await RunWithProgressAsync(id, run.Snapshot.Settings, linked.Token);
                linked.Token.ThrowIfCancellationRequested();
                await Set(id, RunStatus.Completed, 100, result);
                logger.LogInformation("Simulation {RunId} completed: {Total} requests, {Failed} failures", id, result.Total, result.Failed);
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested) { await TrySetTerminalAsync(id, RunStatus.Cancelled, "Запуск остановлен."); }
            catch (Exception ex)
            {
                logger.LogError(ex, "Simulation {RunId} failed", id);
                await TrySetTerminalAsync(id, RunStatus.Failed, "Ошибка симуляции. Проверьте настройки и серверный журнал.");
            }
            finally { queue.Release(id); }
        }
    }
    private async Task<SimulationResult> RunWithProgressAsync(Guid id, ScenarioSettings settings, CancellationToken ct)
    {
        var updates = Channel.CreateBounded<SimulationProgress>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        });
        var simulation = Task.Run(() =>
        {
            var timer = Stopwatch.StartNew();
            try
            {
                return new SimulationEngine().Run(settings, progress =>
                {
                    if (timer.ElapsedMilliseconds < 150)
                    {
                        return;
                    }

                    timer.Restart();
                    updates.Writer.TryWrite(progress);
                }, ct);
            }
            finally { updates.Writer.TryComplete(); }
        }, CancellationToken.None);

        var publisher = PublishUpdatesAsync();
        await Task.WhenAll(simulation, publisher);
        return await simulation;

        async Task PublishUpdatesAsync()
        {
            await foreach (var progress in updates.Reader.ReadAllAsync(ct))
            {
                try { await PublishProgress(id, progress); }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    // Progress is best effort; losing a sample must not discard simulation results.
                    logger.LogWarning(ex, "Could not publish progress for {RunId}", id);
                }
            }
        }
    }

    private async Task TrySetTerminalAsync(Guid id, RunStatus status, string error)
    {
        try { await Set(id, status, 0, error: error); }
        catch (Exception ex)
        {
            // Database outages must not kill both consumers; startup recovery repairs orphaned jobs.
            logger.LogError(ex, "Could not persist terminal status for {RunId}; startup recovery required", id);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        try
        {
            using var scope = scopes.CreateScope();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await scope.ServiceProvider.GetRequiredService<ILabStore>().RecoverInterruptedRunsAsync(timeout.Token);
        }
        catch (Exception ex) { logger.LogError(ex, "Could not finalize interrupted jobs during shutdown"); }
    }
    private async Task PublishProgress(Guid id, SimulationProgress p)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ILabStore>().UpdateRunAsync(id, RunStatus.Running, p.Percent, ct: timeout.Token);
        await NotifyAsync("Progress", id, new { runId = id, progress = p }, timeout.Token);
    }
    private async Task Set(Guid id, RunStatus status, double progress, SimulationResult? result = null, string? error = null)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ILabStore>().UpdateRunAsync(id, status, progress, result, error, timeout.Token);
                break;
            }
            catch (Exception ex) when (attempt < 2 && !timeout.IsCancellationRequested && ex is TimeoutException or DbException { IsTransient: true })
            {
                logger.LogWarning(ex, "Retrying persistence of {Status} for {RunId}, attempt {Attempt}", status, id, attempt + 2);
                await Task.Delay(100 * (attempt + 1), timeout.Token);
            }
        }
        await NotifyAsync("Status", id, new { runId = id, status, progress }, timeout.Token);
    }

    private async Task NotifyAsync(string method, Guid id, object message, CancellationToken ct)
    {
        try { await hub.Clients.Group(id.ToString()).SendAsync(method, message, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Realtime notification {Method} failed for {RunId}; clients can recover via HTTP", method, id); }
    }
}
