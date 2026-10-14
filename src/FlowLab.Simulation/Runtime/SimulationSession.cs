using FlowLab.Domain;

namespace FlowLab.Simulation;

internal sealed partial class SimulationSession
{
    private const int MaximumRequests = 1_500_000;
    private readonly ScenarioSettings settings;
    private readonly Action<SimulationProgress>? onProgress;
    private readonly CancellationToken cancellationToken;
    private readonly Random random;
    private readonly EventCalendar calendar = new();
    private readonly ResultAccumulator metrics = new();
    private readonly List<ServiceStation> stations = [];
    private readonly List<List<int>> stages = [];
    private double now;

    private ResilienceSettings Resilience => settings.Resilience;

    public SimulationSession(ScenarioSettings settings, Action<SimulationProgress>? onProgress, CancellationToken cancellationToken)
    {
        this.settings = settings;
        this.onProgress = onProgress;
        this.cancellationToken = cancellationToken;
        random = new Random(settings.Seed);
        if (settings.Connections is not null)
        {
            InitializeGraph();
            return;
        }
        int? capacity = null;
        QueueDiscipline? discipline = null;
        foreach (var node in settings.Nodes)
        {
            if (node.Kind == NodeKind.Queue)
            {
                capacity = node.QueueCapacity;
                discipline = node.QueueDiscipline;
            }
            else
            {
                if (!node.ParallelWithPrevious || stages.Count == 0)
                {
                    stages.Add([]);
                }

                stages[^1].Add(stations.Count);
                stations.Add(new(node, capacity ?? node.QueueCapacity, Resilience, discipline ?? node.QueueDiscipline));
                capacity = null;
                discipline = null;
            }
        }
    }

    public SimulationResult Execute()
    {
        var first = Sample(1000 / settings.ArrivalRate, settings.ArrivalDistribution);
        if (first < settings.DurationMs)
        {
            calendar.Schedule(first, Arrive);
        }

        for (var i = 1; i <= 100; i++)
        {
            calendar.Schedule(settings.DurationMs * i / 100, ReportProgress);
        }

        while (calendar.TryNext(out var time, out var action))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var station in stations)
            {
                station.Integrate(time - now);
            }

            now = time;
            action();
            if (now >= settings.DurationMs && metrics.Succeeded + metrics.Failed == metrics.Total && stations.All(x => x.Busy == 0))
            {
                break;
            }
        }

        return metrics.Build(Math.Max(settings.DurationMs, now), stations);
    }

    private double Sample(double mean, Distribution distribution, double variation = 0)
    {
        var value = distribution == Distribution.Exponential ? -Math.Log(1 - random.NextDouble()) * mean : mean;
        if (variation > 0)
        {
            value *= 1 + variation * (2 * random.NextDouble() - 1);
        }

        return Math.Max(0.000001, value);
    }

    private void Arrive()
    {
        if (++metrics.Total > MaximumRequests)
        {
            throw new InvalidOperationException("Превышен безопасный предел 1 500 000 запросов.");
        }

        BeginAttempt(new(metrics.Total, now));
        var next = now + Sample(1000 / settings.ArrivalRate, settings.ArrivalDistribution);
        if (next < settings.DurationMs)
        {
            calendar.Schedule(next, Arrive);
        }
    }

    private void BeginAttempt(SimulatedRequest request)
    {
        var version = request.Version;
        request.TimeoutEvent = calendar.Schedule(now + Resilience.TimeoutMs, () =>
        {
            if (!request.IsCompleted && request.Version == version)
            {
                FailAttempt(request, "Timeout");
            }
        });
        if (settings.Connections is not null)
        {
            BeginGraph(request);
        }
        else
        {
            VisitStage(request, 0);
        }
    }

    private void FailAttempt(SimulatedRequest request, string reason)
    {
        if (request.IsCompleted)
        {
            return;
        }

        request.LeaveQueue();
        calendar.Cancel(request.TimeoutEvent);
        request.TimeoutEvent = null;
        request.Version++;
        if (request.Attempt >= Resilience.MaxAttempts || reason == "QueueFull")
        {
            CompleteRequest(request, reason);
            return;
        }

        var delay = Math.Min(Resilience.MaxRetryDelayMs, Resilience.RetryDelayMs * Math.Pow(2, request.Attempt - 1));
        request.Attempt++;
        metrics.Retries++;
        calendar.Schedule(now + delay, () => BeginAttempt(request));
    }

    private void VisitStage(SimulatedRequest request, int stage)
    {
        if (stage == stages.Count)
        {
            CompleteRequest(request, null);
            return;
        }
        var version = request.Version;
        var remaining = stages[stage].Count;
        foreach (var index in stages[stage])
        {
            if (request.IsCompleted || request.Version != version)
            {
                break;
            }

            Visit(request, index, () =>
            {
                if (--remaining == 0)
                {
                    VisitStage(request, stage + 1);
                }
            });
        }
    }

    private void Visit(SimulatedRequest request, int index, Action onSuccess, Action? onFinished = null)
    {
        var station = stations[index];
        if (station.Busy < station.Settings.Workers)
        {
            StartProcessing(station, index, new(request, request.Version, onSuccess, onFinished));
            return;
        }

        if (station.QueueLength >= station.Capacity)
        {
            FailAttempt(request, "QueueFull");
        }
        else
        {
            station.Enqueue(new(request, request.Version, onSuccess, onFinished));
        }
    }

    private void StartProcessing(ServiceStation station, int index, QueuedRequest work)
    {
        // A half-open probe is acquired only when actual processing starts, never while waiting.
        if (Resilience.CircuitBreaker && !station.Breaker.Allow(now))
        {
            FailAttempt(work.Request, "CircuitOpen");
            work.OnFinished?.Invoke();
            return;
        }

        station.Busy++;
        station.Started++;
        station.MaxBusy = Math.Max(station.MaxBusy, station.Busy);
        var node = station.Settings;
        var outage = node.Kind == NodeKind.External && now >= node.OutageStartMs && now < node.OutageStartMs + node.OutageDurationMs;
        var duration = Sample(node.ProcessingMs, node.Distribution, node.Variability) * (outage ? node.OutageLatencyMultiplier : 1);
        var failed = random.NextDouble() < node.ErrorProbability || outage && node.OutageFails;
        var breakerGeneration = station.Breaker.Generation;
        calendar.Schedule(now + duration, () => CompleteProcessing(station, index, work, failed, breakerGeneration));
    }

    private void CompleteProcessing(ServiceStation station, int index, QueuedRequest work, bool failed, int breakerGeneration)
    {
        station.Busy--;
        work.OnFinished?.Invoke();
        if (Resilience.CircuitBreaker)
        {
            station.Breaker.Complete(!failed, now, breakerGeneration);
        }

        if (work.IsAlive)
        {
            if (failed)
            {
                FailAttempt(work.Request, "ServiceError");
            }
            else
            {
                work.OnSuccess();
            }
        }

        while (station.Busy < station.Settings.Workers && station.TryDequeue(out var waiting))
        {
            if (waiting.IsAlive)
            {
                StartProcessing(station, index, waiting);
            }
        }
    }

    private void ReportProgress()
    {
        var point = metrics.Sample(now, stations);
        onProgress?.Invoke(new(Math.Min(99, now / settings.DurationMs * 100), metrics.Total, metrics.Succeeded, metrics.Failed, point));
    }

    private void CompleteRequest(SimulatedRequest request, string? reason)
    {
        calendar.Cancel(request.TimeoutEvent);
        request.TimeoutEvent = null;
        metrics.Complete(request, now, reason);
    }
}
