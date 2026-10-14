using System.Reflection;
using FlowLab.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlowLab.Web.Tests.BackgroundJobs;

public sealed class SimulationWorkerTests
{
    [Fact]
    public async Task Cancelling_an_idle_worker_completes_without_a_background_service_failure()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        using var worker = new SimulationWorker(new RunQueue(),
            services.GetRequiredService<IServiceScopeFactory>(), null!,
            NullLogger<SimulationWorker>.Instance);

        // Exercise the consumer directly: BackgroundService.StartAsync schedules ExecuteAsync
        // on the pool in .NET 10, so immediate cancellation could cancel scheduling itself.
        var execution = (Task)typeof(SimulationWorker)
            .GetMethod("ExecuteAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(worker, [cancellation.Token])!;
        cancellation.Cancel();

        await execution.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(execution.IsCompletedSuccessfully);
    }
}
