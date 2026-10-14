namespace FlowLab.Domain;

public sealed record TimePoint(double TimeMs, double LatencyMs, int QueueLength, double Utilization, int Completed);
