using FlowLab.Application;
using FlowLab.Domain;

namespace FlowLab.Tests.Application;

public sealed class RunServiceTests
{
    [Fact]
    public async Task Full_queue_does_not_create_a_persistent_run()
    {
        var store = new StubStore();
        var scheduler = new StubScheduler { HasCapacity = false };
        Assert.Null(await new RunService(store, scheduler).StartAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Equal(0, store.CreateCalls);
        Assert.Null(scheduler.EnqueuedId);
    }

    [Fact]
    public async Task Failed_persistence_releases_reserved_capacity()
    {
        var store = new StubStore { FailCreation = true };
        var scheduler = new StubScheduler();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new RunService(store, scheduler).StartAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Equal(1, scheduler.Releases);
        Assert.Null(scheduler.EnqueuedId);
    }

    [Fact]
    public async Task Successful_creation_is_enqueued_only_after_snapshot_is_persisted()
    {
        var store = new StubStore();
        var scheduler = new StubScheduler();
        var run = await new RunService(store, scheduler).StartAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(1, store.CreateCalls);
        Assert.Equal(run!.Id, scheduler.EnqueuedId);
        Assert.Equal(0, scheduler.Releases);
    }

    [Fact]
    public async Task Invalid_scenario_never_reaches_storage()
    {
        var store = new StubStore();
        await Assert.ThrowsAsync<ArgumentException>(() => new ScenarioService(store).SaveAsync(null, new("", "", new()), CancellationToken.None));
        Assert.Equal(0, store.SaveCalls);
    }

    [Fact]
    public void Demo_presets_are_valid() => Assert.All(DemoScenarios.Create(), input => Assert.Empty(ScenarioValidator.Validate(input)));

    private sealed class StubScheduler : IRunScheduler
    {
        public bool HasCapacity { get; init; } = true;
        public int Releases { get; private set; }
        public Guid? EnqueuedId { get; private set; }
        public bool TryReserve() => HasCapacity;
        public void ReleaseReservation() => Releases++;
        public void Enqueue(Guid runId) => EnqueuedId = runId;
        public bool Cancel(Guid runId) => false;
    }

    private sealed class StubStore : ILabStore
    {
        public int CreateCalls { get; private set; }
        public int SaveCalls { get; private set; }
        public bool FailCreation { get; init; }

        public Task<RunDto> CreateRunAsync(Guid scenarioId, CancellationToken ct = default)
        {
            CreateCalls++;
            if (FailCreation)
            {
                throw new InvalidOperationException("Simulated persistence outage");
            }

            return Task.FromResult(new RunDto(Guid.NewGuid(), scenarioId, RunStatus.Queued, 0,
                DateTimeOffset.UtcNow, null, new("Test", "", new()), null, null));
        }

        public Task<ScenarioDto> SaveScenarioAsync(Guid? id, ScenarioInput input, CancellationToken ct = default)
        {
            SaveCalls++;
            return Task.FromResult(new ScenarioDto(id ?? Guid.NewGuid(), input.Name, input.Description, input.Settings, DateTimeOffset.UtcNow));
        }

        public Task<List<ScenarioDto>> ListScenariosAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ScenarioDto?> GetScenarioAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> DeleteScenarioAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<RunDto?> GetRunAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<RunDto>> ListRunsAsync(Guid scenarioId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<RequestFailureDto>> GetFailuresAsync(Guid runId, int offset, int limit, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateRunAsync(Guid id, RunStatus status, double progress, SimulationResult? result = null, string? error = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RecoverInterruptedRunsAsync(CancellationToken ct) => throw new NotSupportedException();
    }
}
