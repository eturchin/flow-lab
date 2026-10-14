using FlowLab.Domain;

namespace FlowLab.Simulation;

internal sealed class ServiceStation(NodeSettings settings, int capacity, ResilienceSettings resilience, QueueDiscipline discipline)
{
    public NodeSettings Settings { get; } = settings;
    public int Capacity { get; } = capacity;
    private readonly LinkedList<QueuedRequest> waiting = new();
    public CircuitBreaker Breaker { get; } = new(resilience.FailureThreshold, resilience.BreakDurationMs);
    public int Busy { get; set; }
    public int Started { get; set; }
    public int MaxBusy { get; set; }
    public int MaxQueue { get; set; }
    public double QueueArea { get; private set; }
    public double BusyArea { get; private set; }

    public int QueueLength => waiting.Count;

    public void Integrate(double elapsed)
    {
        QueueArea += QueueLength * elapsed;
        BusyArea += Busy * elapsed;
    }

    public void Enqueue(QueuedRequest work)
    {
        work.Request.QueueEntries.Add((this, waiting.AddLast(work)));
        MaxQueue = Math.Max(MaxQueue, waiting.Count);
    }

    public void Remove(LinkedListNode<QueuedRequest> entry) => waiting.Remove(entry);

    public bool TryDequeue(out QueuedRequest work)
    {
        if ((discipline == QueueDiscipline.Lifo ? waiting.Last : waiting.First) is not { } entry)
        {
            work = null!;
            return false;
        }
        work = entry.Value;
        waiting.Remove(entry);
        work.Request.QueueEntries.RemoveAll(item => ReferenceEquals(item.Entry, entry));
        return true;
    }

    public NodeMetrics ToMetrics(double elapsed) => new(
        Settings.Id, Settings.Name, BusyArea / elapsed / Settings.Workers * 100,
        QueueArea / elapsed, MaxQueue, MaxBusy, Breaker.Opened, Breaker.HalfOpened, Breaker.Closed, Started);
}

internal sealed record QueuedRequest(SimulatedRequest Request, int Version, Action OnSuccess, Action? OnFinished = null, Action<int>? OnDispatch = null)
{
    public bool IsAlive => !Request.IsCompleted && Version == Request.Version;
}
