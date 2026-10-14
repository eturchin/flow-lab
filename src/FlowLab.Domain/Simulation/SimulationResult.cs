namespace FlowLab.Domain;

public sealed record SimulationResult(int Total, int Succeeded, int Failed, double ErrorPercent, double Throughput, double P50, double P95, double P99, double MeanLatency, double AverageQueue, int MaxQueue, int Dropped, int Retries, double ElapsedMs, List<NodeMetrics> Nodes, List<TimePoint> Timeline, Dictionary<string, int> FailureReasons, List<RequestFailure> Failures);
