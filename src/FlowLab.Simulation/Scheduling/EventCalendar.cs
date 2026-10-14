namespace FlowLab.Simulation;

/// <summary>Ordered event set with O(log E) insertion, extraction and cancellation.</summary>
public sealed class EventCalendar
{
    private readonly SortedSet<ScheduledEvent> events = new(Comparer<ScheduledEvent>.Create((a, b) =>
    {
        var byTime = a.Time.CompareTo(b.Time);
        return byTime != 0 ? byTime : a.Sequence.CompareTo(b.Sequence);
    }));
    private long sequence;

    public ScheduledEvent Schedule(double time, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (!double.IsFinite(time) || time < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(time));
        }

        var scheduled = new ScheduledEvent(this, time, sequence++, action);
        events.Add(scheduled);
        return scheduled;
    }

    public bool Cancel(ScheduledEvent? scheduled) => scheduled is not null && ReferenceEquals(scheduled.Owner, this) && events.Remove(scheduled);

    public bool TryNext(out double time, out Action action)
    {
        if (events.Min is { } next)
        {
            events.Remove(next);
            time = next.Time;
            action = next.Action;
            return true;
        }

        time = 0;
        action = null!;
        return false;
    }

    public sealed class ScheduledEvent
    {
        internal EventCalendar Owner { get; }
        internal double Time { get; }
        internal long Sequence { get; }
        internal Action Action { get; }

        internal ScheduledEvent(EventCalendar owner, double time, long sequence, Action action)
        {
            Owner = owner;
            Time = time;
            Sequence = sequence;
            Action = action;
        }
    }
}
