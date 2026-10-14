namespace FlowLab.Simulation;

internal sealed class SimulatedRequest(int id, double createdAt)
{
    public int Id { get; } = id;
    public double CreatedAt { get; } = createdAt;
    public int Attempt { get; set; } = 1;
    public int Version { get; set; }
    public bool IsCompleted { get; set; }
    public EventCalendar.ScheduledEvent? TimeoutEvent { get; set; }
    public List<(ServiceStation Station, LinkedListNode<QueuedRequest> Entry)> QueueEntries { get; } = [];

    public void LeaveQueue()
    {
        foreach (var (station, entry) in QueueEntries)
        {
            station.Remove(entry);
        }

        QueueEntries.Clear();
    }
}
