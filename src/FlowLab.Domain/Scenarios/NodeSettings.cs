namespace FlowLab.Domain;

public sealed record NodeSettings
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "API Service";
    public NodeKind Kind { get; init; }
    public bool ParallelWithPrevious { get; init; }
    public RoutingMode Routing { get; init; }
    public QueueDiscipline QueueDiscipline { get; init; }
    public int Workers { get; init; } = 4;
    public double ProcessingMs { get; init; } = 30;
    public Distribution Distribution { get; init; } = Distribution.Exponential;
    public double Variability { get; init; }
    public double ErrorProbability { get; init; } = 0.01;
    public int QueueCapacity { get; init; } = 200;
    public double OutageStartMs { get; init; } = 20000;
    public double OutageDurationMs { get; init; }
    public double OutageLatencyMultiplier { get; init; } = 5;
    public bool OutageFails { get; init; }
}
