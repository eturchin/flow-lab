using System.Text.Json;
using FlowLab.Domain;
using FlowLab.Simulation;

namespace FlowLab.Tests.Simulation;

public sealed class GraphSimulationTests
{
    private readonly SimulationEngine engine = new();
    private static NodeSettings Service(string id, double ms = 10) => new() { Id = id, Name = id, Workers = 1, ProcessingMs = ms, Distribution = Distribution.Fixed, ErrorProbability = 0, QueueCapacity = 10000 };
    private static ScenarioSettings Graph(List<NodeSettings> nodes, params ConnectionSettings[] connections) => new()
    {
        Nodes = nodes,
        Connections = connections.ToList(),
        ArrivalRate = 1,
        ArrivalDistribution = Distribution.Fixed,
        DurationMs = 2000,
        Resilience = new() { TimeoutMs = 120000 }
    };

    [Fact]
    public void Independent_apis_start_together_and_join_executes_once()
    {
        var result = engine.Run(Graph([Service("a", 40), Service("b", 70), Service("join", 5)], new ConnectionSettings("source", "a"), new ConnectionSettings("source", "b"), new ConnectionSettings("a", "join"), new ConnectionSettings("b", "join")));
        Assert.Equal(75, result.P95);
        Assert.Equal(1, result.Succeeded);
        Assert.All(result.Nodes, n => Assert.Equal(1, n.Started));
    }

    [Fact]
    public void Branch_continues_without_waiting_for_unrelated_slow_api()
    {
        var result = engine.Run(Graph([Service("a", 10), Service("b", 100), Service("tail", 5)], new ConnectionSettings("source", "a"), new ConnectionSettings("source", "b"), new ConnectionSettings("a", "tail")));
        Assert.Equal(100, result.P95);
        Assert.Equal(1, result.Nodes.Single(n => n.Id == "tail").Started);
    }

    [Fact]
    public void Round_robin_source_calls_one_api_per_request()
    {
        var result = engine.Run(Graph([Service("a", 40), Service("b", 70)], new ConnectionSettings("source", "a"), new ConnectionSettings("source", "b")) with { DurationMs = 5000, SourceRouting = RoutingMode.RoundRobin });
        Assert.Equal(4, result.Succeeded);
        Assert.All(result.Nodes, n => Assert.Equal(2, n.Started));
        Assert.Equal(55, result.MeanLatency);
    }

    [Fact]
    public void Skipped_alternative_does_not_deadlock_merge()
    {
        var result = engine.Run(Graph([Service("router", 5) with { Routing = RoutingMode.RoundRobin }, Service("a", 10), Service("b", 20), Service("join", 5)],
            new ConnectionSettings("source", "router"), new ConnectionSettings("router", "a"), new ConnectionSettings("router", "b"), new ConnectionSettings("a", "join"), new ConnectionSettings("b", "join")));
        Assert.Equal(20, result.P95);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal(0, result.Nodes.Single(n => n.Id == "b").Started);
        Assert.Equal(1, result.Nodes.Single(n => n.Id == "join").Started);
    }

    [Fact]
    public void Broker_delivers_each_message_to_one_consumer_and_uses_shared_bounded_queue()
    {
        var settings = Graph([Service("mq") with { Kind = NodeKind.Broker, QueueCapacity = 3 }, Service("a", 100), Service("b", 100)],
            new ConnectionSettings("source", "mq"), new ConnectionSettings("mq", "a"), new ConnectionSettings("mq", "b")) with
        { ArrivalRate = 100, DurationMs = 1000 };
        var result = engine.Run(settings);
        var mq = result.Nodes.Single(n => n.Id == "mq");
        Assert.Equal(3, mq.MaxQueue);
        Assert.Equal(2, mq.MaxBusy);
        Assert.True(result.Dropped > 0);
        Assert.Equal(result.Total, result.Succeeded + result.Failed);
        Assert.Equal(result.Succeeded, result.Nodes.Where(n => n.Id != "mq").Sum(n => n.Started));
        Assert.Equal(result.Succeeded, mq.Started);
        Assert.All(result.Nodes.Where(n => n.Id != "mq"), n => { Assert.Equal(1, n.MaxBusy); Assert.Equal(0, n.MaxQueue); });
    }

    [Fact]
    public void Faster_consumer_pulls_more_work_without_waiting_for_slow_consumer()
    {
        var result = engine.Run(Graph([Service("mq") with { Kind = NodeKind.Broker }, Service("fast", 10), Service("slow", 100)],
            new ConnectionSettings("source", "mq"), new ConnectionSettings("mq", "fast"), new ConnectionSettings("mq", "slow")) with
        { ArrivalRate = 100, DurationMs = 1000 });
        Assert.Equal(result.Total, result.Succeeded);
        Assert.True(result.Nodes.Single(n => n.Id == "fast").Started > result.Nodes.Single(n => n.Id == "slow").Started);
    }

    [Fact]
    public void Broker_timeouts_remove_waiting_messages_but_inflight_work_drains()
    {
        var result = engine.Run(Graph([Service("mq") with { Kind = NodeKind.Broker }, Service("consumer", 1000)], new ConnectionSettings("source", "mq"), new ConnectionSettings("mq", "consumer"))
            with
        { ArrivalRate = 100, DurationMs = 100, Resilience = new() { TimeoutMs = 20 } });
        Assert.Equal(result.Total, result.Failed);
        Assert.Equal(1, result.Nodes.Single(n => n.Id == "consumer").Started);
        Assert.Equal(1010, result.ElapsedMs);
    }

    [Fact]
    public void Consumer_failures_retries_and_breaker_release_broker_slots()
    {
        var settings = Graph([Service("mq") with { Kind = NodeKind.Broker }, Service("consumer", 20) with { ErrorProbability = 1 }], new ConnectionSettings("source", "mq"), new ConnectionSettings("mq", "consumer"))
            with
        { ArrivalRate = 100, DurationMs = 1000, Resilience = new() { TimeoutMs = 500, MaxAttempts = 3, RetryDelayMs = 1, CircuitBreaker = true, FailureThreshold = 1, BreakDurationMs = 50 } };
        var result = engine.Run(settings);
        Assert.Equal(result.Total, result.Failed);
        Assert.Equal(result.Total * 2, result.Retries);
        Assert.True(result.FailureReasons.GetValueOrDefault("CircuitOpen") > 0);
        Assert.All(result.Nodes, n => Assert.InRange(n.Utilization, 0, 100));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(engine.Run(settings)));
    }

    [Fact]
    public void Graph_buffer_preserves_capacity_and_is_not_a_processing_station()
    {
        var result = engine.Run(Graph([Service("buffer") with { Kind = NodeKind.Queue, QueueCapacity = 1 }, Service("api", 100)], new ConnectionSettings("source", "buffer"), new ConnectionSettings("buffer", "api")) with { ArrivalRate = 100, DurationMs = 1000 });
        Assert.Single(result.Nodes);
        Assert.Equal(1, result.MaxQueue);
        Assert.True(result.Dropped > 0);
    }

    [Fact]
    public void Separate_subscriber_queues_each_receive_a_copy_with_round_robin_consumers()
    {
        var result = engine.Run(Graph([Service("q1") with { Kind = NodeKind.Broker }, Service("q2") with { Kind = NodeKind.Broker }, Service("a"), Service("b"), Service("c")],
            new ConnectionSettings("source", "q1"), new ConnectionSettings("source", "q2"), new ConnectionSettings("q1", "a"), new ConnectionSettings("q1", "b"), new ConnectionSettings("q2", "c")) with
        { DurationMs = 5000 });
        Assert.Equal(4, result.Succeeded);
        Assert.Equal(2, result.Nodes.Single(n => n.Id == "a").Started);
        Assert.Equal(2, result.Nodes.Single(n => n.Id == "b").Started);
        Assert.Equal(4, result.Nodes.Single(n => n.Id == "c").Started);
        Assert.All(result.Nodes.Where(n => n.Id.StartsWith('q')), n => Assert.Equal(4, n.Started));
    }

    [Fact]
    public void Invalid_cycles_disconnected_nodes_and_shared_broker_consumers_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => engine.Run(Graph([Service("a"), Service("b")], new ConnectionSettings("source", "a"), new ConnectionSettings("a", "b"), new ConnectionSettings("b", "a"))));
        Assert.Throws<ArgumentException>(() => engine.Run(Graph([Service("a"), Service("b")], new ConnectionSettings("source", "a"))));
        Assert.Throws<ArgumentException>(() => engine.Run(Graph([Service("mq") with { Kind = NodeKind.Broker }, Service("a")], new ConnectionSettings("source", "mq"), new ConnectionSettings("mq", "a"), new ConnectionSettings("source", "a"))));
        Assert.Throws<ArgumentException>(() => engine.Run(Graph([Service("mq") with { Kind = NodeKind.Broker }], new ConnectionSettings("source", "mq"))));
        Assert.Throws<ArgumentException>(() => engine.Run(Graph([Service("sink")], new ConnectionSettings("source", "sink"))));
        Assert.Throws<ArgumentException>(() => engine.Run(Graph([Service("a") with { Name = new string('x', 81) }], new ConnectionSettings("source", "a"))));
    }
}
