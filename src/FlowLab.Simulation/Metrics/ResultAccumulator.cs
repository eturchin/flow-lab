using FlowLab.Domain;

namespace FlowLab.Simulation;

internal sealed class ResultAccumulator
{
    private readonly List<double> latencies = [];
    private readonly Dictionary<string, int> failureReasons = [];
    private readonly List<RequestFailure> failures = [];
    private double intervalLatency;
    private int intervalCompleted;

    public int Total { get; set; }
    public int Succeeded { get; private set; }
    public int Failed { get; private set; }
    public int Dropped { get; private set; }
    public int Retries { get; set; }
    public List<TimePoint> Timeline { get; } = [];

    public void Complete(SimulatedRequest request, double now, string? reason)
    {
        if (request.IsCompleted)
        {
            return;
        }

        request.IsCompleted = true;
        var latency = now - request.CreatedAt;
        latencies.Add(latency);
        intervalLatency += latency;
        intervalCompleted++;
        if (reason is null)
        {
            Succeeded++;
        }
        else
        {
            Failed++;
            failureReasons[reason] = failureReasons.GetValueOrDefault(reason) + 1;
            failures.Add(new(request.Id, request.CreatedAt, now, request.Attempt, reason));
            if (reason == "QueueFull")
            {
                Dropped++;
            }
        }
    }

    public TimePoint Sample(double now, IReadOnlyList<ServiceStation> stations)
    {
        var point = new TimePoint(now, intervalCompleted == 0 ? 0 : intervalLatency / intervalCompleted,
            stations.Sum(x => x.QueueLength), stations.Average(x => (double)x.Busy / x.Settings.Workers) * 100,
            Succeeded + Failed);
        Timeline.Add(point);
        intervalLatency = 0;
        intervalCompleted = 0;
        return point;
    }

    public SimulationResult Build(double elapsed, IReadOnlyList<ServiceStation> stations)
    {
        latencies.Sort();
        var nodes = stations.Select(x => x.ToMetrics(elapsed)).ToList();
        return new(Total, Succeeded, Failed, Total == 0 ? 0 : 100.0 * Failed / Total,
            Succeeded / (elapsed / 1000), Statistics.Percentile(latencies, .5),
            Statistics.Percentile(latencies, .95), Statistics.Percentile(latencies, .99),
            latencies.Count == 0 ? 0 : latencies.Average(), nodes.Sum(x => x.AverageQueue),
            nodes.Max(x => x.MaxQueue), Dropped, Retries, elapsed, nodes, Timeline, failureReasons, failures);
    }
}
