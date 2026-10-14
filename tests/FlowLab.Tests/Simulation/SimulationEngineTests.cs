using System.Text.Json;
using FlowLab.Domain;
using FlowLab.Simulation;

namespace FlowLab.Tests.Simulation;

public sealed class SimulationEngineTests
{
    private readonly SimulationEngine engine = new();
    private static NodeSettings FixedNode(string id, double duration, bool parallel = false) => new()
    {
        Id = id,
        Name = id,
        ProcessingMs = duration,
        Distribution = Distribution.Fixed,
        ErrorProbability = 0,
        ParallelWithPrevious = parallel,
        Workers = 1,
        QueueCapacity = 100
    };

    [Theory]
    [InlineData(false, 125)]
    [InlineData(true, 85)]
    public void Parallel_stage_waits_for_slowest_branch_before_continuation(bool parallel, double latency)
    {
        var settings = Single() with
        {
            ArrivalDistribution = Distribution.Fixed,
            ArrivalRate = 1,
            DurationMs = 2000,
            Nodes = [FixedNode("gateway", 10), FixedNode("catalog", 40), FixedNode("inventory", 70, parallel), FixedNode("response", 5)]
        };
        var result = engine.Run(settings);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal(latency, result.P95);
        Assert.All(result.Nodes, node => Assert.Equal(1, node.MaxBusy));
    }

    [Fact]
    public void Parallel_failures_and_retries_complete_each_request_once()
    {
        var settings = Single() with
        {
            ArrivalDistribution = Distribution.Fixed,
            ArrivalRate = 20,
            DurationMs = 2000,
            Nodes = [FixedNode("a", 100) with { ErrorProbability = 1 }, FixedNode("b", 200, true) with { ErrorProbability = 1 }],
            Resilience = new() { MaxAttempts = 3, TimeoutMs = 500, RetryDelayMs = 5 }
        };
        var result = engine.Run(settings);
        Assert.Equal(result.Total, result.Failed);
        Assert.Equal(result.Total * 2, result.Retries);
        Assert.Equal(result.Total, result.Failures.Select(x => x.RequestId).Distinct().Count());
        Assert.Equal(result.Total, result.Failures.Count);
        Assert.All(result.Nodes, node => Assert.Equal(1, node.MaxBusy));
    }

    [Fact]
    public void Parallel_timeouts_remove_all_waiting_branches()
    {
        var result = engine.Run(Single() with
        {
            ArrivalDistribution = Distribution.Fixed,
            ArrivalRate = 100,
            DurationMs = 100,
            Nodes = [FixedNode("a", 1000), FixedNode("b", 1000, true)],
            Resilience = new() { TimeoutMs = 20 }
        });
        Assert.Equal(result.Total, result.Failed);
        Assert.Equal(result.Total, result.FailureReasons["Timeout"]);
        Assert.Equal(1010, result.ElapsedMs);
        // All queued branches expire by t=110. No stale queue remains during the long drain.
        Assert.All(result.Nodes, node => Assert.InRange(node.AverageQueue, 0, .2));
    }

    [Theory]
    [InlineData(QueueDiscipline.Fifo, 0)]
    [InlineData(QueueDiscipline.Lifo, 1)]
    public void Queue_discipline_changes_timeout_outcome(QueueDiscipline discipline, int successes)
    {
        var result = engine.Run(Single() with
        {
            ArrivalDistribution = Distribution.Fixed,
            ArrivalRate = 25,
            DurationMs = 150,
            Nodes = [FixedNode("service", 100) with { QueueDiscipline = discipline }],
            Resilience = new() { TimeoutMs = 140 }
        });
        Assert.Equal(3, result.Total);
        Assert.Equal(1 + successes, result.Succeeded);
        Assert.Equal(result.Total, result.Succeeded + result.Failed);
    }

    [Fact]
    public void Parallel_topology_survives_json_roundtrip_and_seed_reproduces_results()
    {
        var settings = Single() with { Nodes = [FixedNode("a", 10), FixedNode("b", 20, true) with { QueueDiscipline = QueueDiscipline.Lifo }] };
        var restored = JsonSerializer.Deserialize<ScenarioSettings>(JsonSerializer.Serialize(settings))!;
        Assert.Equal(JsonSerializer.Serialize(engine.Run(settings)), JsonSerializer.Serialize(engine.Run(restored)));
        Assert.Throws<ArgumentException>(() => engine.Run(settings with { Nodes = [FixedNode("invalid", 10, true)] }));
    }
    private static ScenarioSettings Single(NodeSettings? node = null) => new()
    {
        ArrivalRate = 30,
        DurationMs = 30000,
        Nodes = [node ?? new() { Workers = 1, ProcessingMs = 20, ErrorProbability = 0, QueueCapacity = 10000 }],
        Resilience = new() { TimeoutMs = 120000 }
    };

    [Fact]
    public void Identical_seed_reproduces_every_metric_and_time_point()
    {
        var settings = new ScenarioSettings();
        Assert.Equal(JsonSerializer.Serialize(engine.Run(settings)), JsonSerializer.Serialize(engine.Run(settings)));
    }

    [Fact]
    public void Different_seeds_change_random_simulation()
    {
        var settings = new ScenarioSettings();
        Assert.NotEqual(engine.Run(settings).P95, engine.Run(settings with { Seed = 43 }).P95);
    }

    [Fact]
    public void Parallel_workers_are_never_exceeded()
    {
        var settings = Single(new() { Workers = 3, ProcessingMs = 100, ErrorProbability = 0 }) with { ArrivalRate = 300 };
        var result = engine.Run(settings);
        Assert.Equal(3, result.Nodes[0].MaxBusy);
        Assert.InRange(result.Nodes[0].Utilization, 0, 100);
        Assert.Equal(result.Total, result.Succeeded + result.Failed);
    }

    [Fact]
    public void Bounded_queue_drops_requests_without_retrying()
    {
        var settings = Single(new() { Workers = 1, ProcessingMs = 100, Distribution = Distribution.Fixed, QueueCapacity = 2, ErrorProbability = 0 }) with { ArrivalRate = 200, Resilience = new() { MaxAttempts = 3, TimeoutMs = 120000 } };
        var result = engine.Run(settings);
        Assert.Equal(2, result.MaxQueue);
        Assert.True(result.Dropped > 0);
        Assert.Equal(result.Dropped, result.Failed);
        Assert.Equal(0, result.Retries);
    }

    [Fact]
    public void Explicit_queue_configures_next_service_buffer()
    {
        var settings = Single() with { ArrivalRate = 100, Nodes = [new() { Kind = NodeKind.Queue, Id = "buffer", QueueCapacity = 1 }, new() { Workers = 1, ProcessingMs = 100, ErrorProbability = 0, QueueCapacity = 1000 }] };
        Assert.Equal(1, engine.Run(settings).MaxQueue);
    }

    [Fact]
    public void Successful_and_failed_requests_are_counted_exactly_once()
    {
        var settings = Single(new() { Workers = 10, ProcessingMs = 10, ErrorProbability = .5 });
        var result = engine.Run(settings);
        Assert.True(result.Succeeded > 0);
        Assert.True(result.Failed > 0);
        Assert.Equal(result.Total, result.Succeeded + result.Failed);
        Assert.Equal(result.Failed, result.FailureReasons.Values.Sum());
        Assert.Equal(result.Failed, result.Failures.Count);
        Assert.Equal(result.Failed, result.Failures.Select(failure => failure.RequestId).Distinct().Count());
        Assert.All(result.Failures, failure => Assert.True(failure.CompletedAtMs >= failure.CreatedAtMs));
        Assert.Equal(100.0 * result.Failed / result.Total, result.ErrorPercent);
    }

    [Fact]
    public void Timeout_and_backoff_bound_total_request_latency_and_attempts()
    {
        var settings = Single(new() { Workers = 1024, ProcessingMs = 100, Distribution = Distribution.Fixed, ErrorProbability = 0 }) with
        {
            ArrivalRate = 1,
            DurationMs = 2000,
            ArrivalDistribution = Distribution.Fixed,
            Resilience = new() { TimeoutMs = 10, MaxAttempts = 3, RetryDelayMs = 5, MaxRetryDelayMs = 8 }
        };
        var result = engine.Run(settings);
        Assert.Equal(1, result.Total);
        Assert.Equal(1, result.Failed);
        Assert.Equal(2, result.Retries);
        Assert.Equal(43, result.P95);
        Assert.Equal(1, result.FailureReasons["Timeout"]);
    }

    [Fact]
    public void Retries_cannot_run_forever_when_every_service_call_fails()
    {
        var settings = Single(new() { Workers = 100, ProcessingMs = 5, ErrorProbability = 1 }) with { Resilience = new() { MaxAttempts = 4 } };
        var result = engine.Run(settings);
        Assert.Equal(result.Total, result.Failed);
        Assert.Equal(result.Total * 3, result.Retries);
    }

    [Fact]
    public void Empty_request_sample_has_finite_zero_metrics()
    {
        var result = engine.Run(Single() with { ArrivalRate = .001, DurationMs = 100, ArrivalDistribution = Distribution.Fixed });
        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.P99);
        Assert.Equal(0, result.ErrorPercent);
        Assert.Equal(0, result.Throughput);
    }

    [Fact]
    public void External_outage_opens_breaker_then_probe_recovers()
    {
        var settings = Single(new() { Kind = NodeKind.External, Workers = 1, ProcessingMs = 5, Distribution = Distribution.Fixed, ErrorProbability = 0, OutageStartMs = 0, OutageDurationMs = 1000, OutageFails = true, OutageLatencyMultiplier = 1 }) with
        {
            ArrivalDistribution = Distribution.Fixed,
            ArrivalRate = 20,
            DurationMs = 5000,
            Resilience = new() { CircuitBreaker = true, FailureThreshold = 2, BreakDurationMs = 200 }
        };
        var result = engine.Run(settings);
        Assert.True(result.Nodes[0].BreakerOpened > 0);
        Assert.True(result.Nodes[0].BreakerHalfOpened > 0);
        Assert.True(result.Nodes[0].BreakerClosed > 0);
        Assert.True(result.FailureReasons["CircuitOpen"] > 0);
        Assert.True(result.Succeeded > 0);
    }

    [Fact]
    public void Mm1_matches_analytical_mean_latency_and_queue_within_sampling_tolerance()
    {
        // λ = 100/s, μ = 200/s, rho = .5; W = 10ms; Lq = .5.
        var settings = Single(new() { Workers = 1, ProcessingMs = 5, Distribution = Distribution.Exponential, ErrorProbability = 0, QueueCapacity = 1000000 }) with { ArrivalRate = 100, DurationMs = 3600000 };
        var result = engine.Run(settings);
        Assert.Equal(0, result.Failed);
        Assert.InRange(result.MeanLatency, 9.5, 10.5);
        Assert.InRange(result.AverageQueue, .45, .55);
        Assert.InRange(result.Nodes[0].Utilization, 48, 52);
    }

    [Fact]
    public void Cancellation_stops_the_event_loop()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => engine.Run(Single(), cancellationToken: cancellation.Token));
    }

    [Fact]
    public void Timeout_wins_when_completion_has_the_exact_same_timestamp()
    {
        var settings = Single(new() { Workers = 1, ProcessingMs = 10, Distribution = Distribution.Fixed, ErrorProbability = 0 }) with
        {
            ArrivalRate = 1,
            DurationMs = 2000,
            ArrivalDistribution = Distribution.Fixed,
            Resilience = new() { TimeoutMs = 10 }
        };
        var result = engine.Run(settings);
        Assert.Equal(1, result.FailureReasons["Timeout"]);
        Assert.Equal(0, result.Succeeded);
    }
}
