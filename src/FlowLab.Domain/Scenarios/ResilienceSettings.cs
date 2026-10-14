namespace FlowLab.Domain;

public sealed record ResilienceSettings
{
    public double TimeoutMs { get; init; } = 3000;
    public int MaxAttempts { get; init; } = 1;
    public double RetryDelayMs { get; init; } = 100;
    public double MaxRetryDelayMs { get; init; } = 2000;
    public bool CircuitBreaker { get; init; }
    public int FailureThreshold { get; init; } = 5;
    public double BreakDurationMs { get; init; } = 2000;
}
