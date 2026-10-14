namespace FlowLab.Domain;

public sealed record ScenarioSettings
{
    public double ArrivalRate { get; init; } = 80;
    public double DurationMs { get; init; } = 60000;
    public int Seed { get; init; } = 42;
    public Distribution ArrivalDistribution { get; init; } = Distribution.Exponential;
    public ResilienceSettings Resilience { get; init; } = new();
    public List<ConnectionSettings>? Connections { get; init; }
    public RoutingMode SourceRouting { get; init; }
    public List<NodeSettings> Nodes { get; init; } = [new(), new() { Id = "external", Name = "Payment API", Kind = NodeKind.External, Workers = 8, ProcessingMs = 65, ErrorProbability = 0.03 }];
}
