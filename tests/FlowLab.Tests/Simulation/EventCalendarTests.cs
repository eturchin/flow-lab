using FlowLab.Simulation;

namespace FlowLab.Tests.Simulation;

public sealed class EventCalendarTests
{
    [Fact]
    public void Cancelled_events_are_removed_and_other_events_keep_their_order()
    {
        var calendar = new EventCalendar();
        var actual = new List<int>();
        calendar.Schedule(1, () => actual.Add(1));
        var cancelled = calendar.Schedule(1, () => actual.Add(2));
        calendar.Schedule(1, () => actual.Add(3));
        Assert.True(calendar.Cancel(cancelled));
        Assert.False(calendar.Cancel(cancelled));
        while (calendar.TryNext(out _, out var action))
        {
            action();
        }

        Assert.Equal([1, 3], actual);
    }

    [Fact]
    public void Equal_time_events_follow_insertion_order_including_new_events()
    {
        var calendar = new EventCalendar();
        var actual = new List<int>();
        calendar.Schedule(2, () => actual.Add(4));
        calendar.Schedule(1, () => { actual.Add(1); calendar.Schedule(1, () => actual.Add(3)); });
        calendar.Schedule(1, () => actual.Add(2));
        while (calendar.TryNext(out _, out var action))
        {
            action();
        }

        Assert.Equal([1, 2, 3, 4], actual);
    }
}
