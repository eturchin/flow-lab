namespace FlowLab.Domain;

public sealed record ScenarioInput(string Name, string Description, ScenarioSettings Settings, Guid? WorkspaceId = null);
