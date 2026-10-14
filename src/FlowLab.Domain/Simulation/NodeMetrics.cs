namespace FlowLab.Domain;

public sealed record NodeMetrics(string Id, string Name, double Utilization, double AverageQueue, int MaxQueue, int MaxBusy, int BreakerOpened, int BreakerHalfOpened, int BreakerClosed, int Started = 0);
