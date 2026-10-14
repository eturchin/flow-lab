using FlowLab.Application;
using FlowLab.Domain;

namespace FlowLab.Infrastructure;

internal static class EntityMapping
{
    public static ScenarioDto ToDto(this ScenarioEntity entity) => new(
        entity.Id, entity.Name, entity.Description,
        LabJson.Read<ScenarioSettings>(entity.SettingsJson), entity.UpdatedAt, entity.WorkspaceId);

    public static RunDto ToDto(this RunEntity entity) => new(
        entity.Id, entity.ScenarioId, entity.Status, entity.Progress, entity.CreatedAt,
        entity.FinishedAt, LabJson.Read<ScenarioInput>(entity.SnapshotJson),
        entity.ResultJson is null ? null : LabJson.Read<SimulationResultDto>(entity.ResultJson),
        entity.Error);
}
