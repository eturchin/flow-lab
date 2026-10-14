using FlowLab.Domain;

namespace FlowLab.Application;

public sealed record RunDto(Guid Id, Guid ScenarioId, RunStatus Status, double Progress, DateTimeOffset CreatedAt, DateTimeOffset? FinishedAt, ScenarioInput Snapshot, SimulationResultDto? Result, string? Error);
