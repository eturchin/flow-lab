namespace FlowLab.Application;

/// <summary>Host-provided bounded scheduling and cancellation, independent of HTTP.</summary>
public interface IRunScheduler
{
    bool TryReserve();
    void ReleaseReservation();
    /// <summary>Transfers a reservation to the queue on success; caller retains it on failure.</summary>
    void Enqueue(Guid runId);
    bool Cancel(Guid runId);
}
