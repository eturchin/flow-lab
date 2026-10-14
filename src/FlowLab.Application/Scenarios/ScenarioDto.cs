using FlowLab.Domain;

namespace FlowLab.Application;

public sealed record ScenarioDto(Guid Id, string Name, string Description, ScenarioSettings Settings, DateTimeOffset UpdatedAt, Guid WorkspaceId = default);
