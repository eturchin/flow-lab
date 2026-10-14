using FlowLab.Domain;

namespace FlowLab.Application;

public sealed record MetricChange(string Name, double Baseline, double Candidate, double Absolute, double? Percent);
