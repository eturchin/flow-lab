using System.Text.Json;
using System.Text.Json.Serialization;
using FlowLab.Application;
using FlowLab.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowLab.Infrastructure;

public sealed class ScenarioEntity
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string SettingsJson { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public List<RunEntity> Runs { get; set; } = [];
}
