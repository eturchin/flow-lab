using FlowLab.Domain;

namespace FlowLab.Application;

/// <summary>Stable public result contract, independent of simulation runtime and EF entities.</summary>
public sealed record SimulationResultDto(
    int Total, int Succeeded, int Failed, double ErrorPercent, double Throughput,
    double P50, double P95, double P99, double MeanLatency, double AverageQueue,
    int MaxQueue, int Dropped, int Retries, double ElapsedMs,
    IReadOnlyList<NodeMetricsDto> Nodes, IReadOnlyList<TimePointDto> Timeline,
    IReadOnlyDictionary<string, int> FailureReasons)
{
    public static SimulationResultDto From(SimulationResult result) => new(
        result.Total, result.Succeeded, result.Failed, result.ErrorPercent, result.Throughput,
        result.P50, result.P95, result.P99, result.MeanLatency, result.AverageQueue,
        result.MaxQueue, result.Dropped, result.Retries, result.ElapsedMs,
        result.Nodes.Select(n => new NodeMetricsDto(n.Id, n.Name, n.Utilization, n.AverageQueue,
            n.MaxQueue, n.MaxBusy, n.BreakerOpened, n.BreakerHalfOpened, n.BreakerClosed, n.Started)).ToArray(),
        result.Timeline.Select(p => new TimePointDto(p.TimeMs, p.LatencyMs, p.QueueLength, p.Utilization, p.Completed)).ToArray(),
        new Dictionary<string, int>(result.FailureReasons));
}

public sealed record NodeMetricsDto(string Id, string Name, double Utilization, double AverageQueue,
    int MaxQueue, int MaxBusy, int BreakerOpened, int BreakerHalfOpened, int BreakerClosed, int Started = 0);

public sealed record TimePointDto(double TimeMs, double LatencyMs, int QueueLength, double Utilization, int Completed);
