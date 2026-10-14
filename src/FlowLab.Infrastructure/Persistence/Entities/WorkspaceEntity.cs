namespace FlowLab.Infrastructure;

public sealed class WorkspaceEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
