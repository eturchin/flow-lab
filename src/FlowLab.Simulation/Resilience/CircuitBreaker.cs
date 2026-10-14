namespace FlowLab.Simulation;

public enum CircuitState { Closed, Open, HalfOpen }

/// <summary>Consecutive provider failures, one recovery probe, and generation-scoped completions.</summary>
public sealed class CircuitBreaker
{
    private readonly int failureThreshold;
    private readonly double breakDurationMs;
    private int consecutiveFailures;
    private double openUntil;
    private bool probeInFlight;

    public CircuitState State { get; private set; }
    public int Opened { get; private set; }
    public int HalfOpened { get; private set; }
    public int Closed { get; private set; }
    public int Generation { get; private set; }

    public CircuitBreaker(int failureThreshold, double breakDurationMs)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(failureThreshold, 1);
        if (!double.IsFinite(breakDurationMs) || breakDurationMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(breakDurationMs));
        }

        this.failureThreshold = failureThreshold;
        this.breakDurationMs = breakDurationMs;
    }

    public bool Allow(double now)
    {
        if (State == CircuitState.Open && now >= openUntil)
        {
            State = CircuitState.HalfOpen;
            HalfOpened++;
            Generation++;
            probeInFlight = false;
        }

        if (State == CircuitState.Open || State == CircuitState.HalfOpen && probeInFlight)
        {
            return false;
        }

        if (State == CircuitState.HalfOpen)
        {
            probeInFlight = true;
        }

        return true;
    }

    public void Complete(bool success, double now, int? generation = null)
    {
        // A completion from an earlier open/closed cycle cannot close the current recovery probe.
        if (generation.HasValue && generation.Value != Generation || State == CircuitState.Open)
        {
            return;
        }

        if (success)
        {
            consecutiveFailures = 0;
            if (State == CircuitState.HalfOpen)
            {
                State = CircuitState.Closed;
                Closed++;
                probeInFlight = false;
            }
            return;
        }

        if (State != CircuitState.HalfOpen && ++consecutiveFailures < failureThreshold)
        {
            return;
        }

        State = CircuitState.Open;
        openUntil = now + breakDurationMs;
        Opened++;
        Generation++;
        probeInFlight = false;
    }
}
