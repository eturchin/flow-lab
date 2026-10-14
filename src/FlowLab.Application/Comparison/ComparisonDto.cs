using FlowLab.Domain;

namespace FlowLab.Application;

public sealed record ComparisonDto(Guid BaselineId, Guid CandidateId, List<MetricChange> Metrics);
