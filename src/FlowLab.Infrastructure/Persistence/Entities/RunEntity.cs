using System.Text.Json;
using System.Text.Json.Serialization;
using FlowLab.Application;
using FlowLab.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowLab.Infrastructure;

public sealed class RunEntity
{
    public Guid Id { get; set; }
    public Guid ScenarioId { get; set; }
    public RunStatus Status { get; set; }
    public double Progress { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string SnapshotJson { get; set; } = "";
    public string? ResultJson { get; set; }
    public string? FailuresJson { get; set; }
    public string? Error { get; set; }
}
