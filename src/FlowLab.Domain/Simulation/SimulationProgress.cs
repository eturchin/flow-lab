namespace FlowLab.Domain;

public sealed record SimulationProgress(double Percent, int Total, int Succeeded, int Failed, TimePoint Point);
