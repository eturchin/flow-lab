using System.Linq.Expressions;
using FlowLab.Application;
using FlowLab.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowLab.Infrastructure;

public sealed class LabStore(LabDbContext db) : ILabStore
{
    // The potentially large failure journal is deliberately excluded from normal/history queries.
    private static readonly Expression<Func<RunEntity, RunEntity>> RunSummary = entity => new RunEntity
    {
        Id = entity.Id,
        ScenarioId = entity.ScenarioId,
        Status = entity.Status,
        Progress = entity.Progress,
        CreatedAt = entity.CreatedAt,
        FinishedAt = entity.FinishedAt,
        SnapshotJson = entity.SnapshotJson,
        ResultJson = entity.ResultJson,
        Error = entity.Error
    };
    public async Task<List<ScenarioDto>> ListScenariosAsync(CancellationToken ct = default)
    {
        var entities = await db.Scenarios.AsNoTracking().OrderBy(x => x.UpdatedAt).ToListAsync(ct);
        return entities.Select(x => x.ToDto()).ToList();
    }

    public async Task<ScenarioDto?> GetScenarioAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Scenarios.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return entity?.ToDto();
    }

    public async Task<ScenarioDto> SaveScenarioAsync(Guid? id, ScenarioInput input, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var snapshot = id.HasValue
            ? await db.Scenarios.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.Value, ct) ?? throw new KeyNotFoundException("Сценарий не найден.")
            : null;

        var workspaceId = input.WorkspaceId ?? snapshot?.WorkspaceId ?? await db.Workspaces.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => x.Id).FirstOrDefaultAsync(ct);
        var workspace = await db.Workspaces.FromSqlInterpolated($"SELECT * FROM \"Workspaces\" WHERE \"Id\" = {workspaceId} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        if (workspace.Count == 0)
        {
            throw new KeyNotFoundException("Рабочее пространство не найдено.");
        }

        var entity = id.HasValue
            ? (await db.Scenarios.FromSqlInterpolated($"SELECT * FROM \"Scenarios\" WHERE \"Id\" = {id.Value} FOR UPDATE").ToListAsync(ct)).SingleOrDefault() ?? throw new KeyNotFoundException("Сценарий не найден.")
            : new ScenarioEntity { Id = Guid.NewGuid() };
        if (input.WorkspaceId is null && snapshot is not null && entity.WorkspaceId != workspaceId)
        {
            throw new InvalidOperationException("Рабочее пространство сценария изменилось. Обновите список и повторите действие.");
        }

        entity.WorkspaceId = workspaceId;

        entity.Name = input.Name.Trim();
        entity.Description = input.Description;
        entity.SettingsJson = LabJson.Write(input.Settings);
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        if (!id.HasValue)
        {
            db.Scenarios.Add(entity);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteScenarioAsync(Guid id, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (await LockScenarioAsync(id, ct) is null)
        {
            return false;
        }

        var hasActiveRuns = await db.Runs.AnyAsync(x => x.ScenarioId == id &&
            (x.Status == RunStatus.Queued || x.Status == RunStatus.Running), ct);
        if (hasActiveRuns)
        {
            throw new InvalidOperationException("Сначала остановите активные запуски сценария.");
        }

        await db.Scenarios.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    public async Task<RunDto> CreateRunAsync(Guid scenarioId, CancellationToken ct = default)
    {
        // Serialize run creation and scenario deletion, so a queued job cannot lose its snapshot.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var scenario = await LockScenarioAsync(scenarioId, ct) ?? throw new KeyNotFoundException("Сценарий не найден.");
        var entity = new RunEntity
        {
            Id = Guid.NewGuid(),
            ScenarioId = scenario.Id,
            Status = RunStatus.Queued,
            CreatedAt = DateTimeOffset.UtcNow,
            SnapshotJson = LabJson.Write(new ScenarioInput(scenario.Name, scenario.Description,
                LabJson.Read<ScenarioSettings>(scenario.SettingsJson)))
        };
        db.Runs.Add(entity);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return entity.ToDto();
    }

    public async Task<RunDto?> GetRunAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Runs.AsNoTracking().Where(x => x.Id == id).Select(RunSummary).SingleOrDefaultAsync(ct);
        return entity?.ToDto();
    }

    public async Task<List<RunDto>> ListRunsAsync(Guid scenarioId, CancellationToken ct = default)
    {
        var entities = await db.Runs.AsNoTracking().Where(x => x.ScenarioId == scenarioId)
            .OrderByDescending(x => x.CreatedAt).Take(100).Select(RunSummary).ToListAsync(ct);
        return entities.Select(x => x.ToDto()).ToList();
    }

    public async Task UpdateRunAsync(Guid id, RunStatus status, double progress, SimulationResult? result = null,
        string? error = null, CancellationToken ct = default)
    {
        var terminal = status is RunStatus.Completed or RunStatus.Cancelled or RunStatus.Failed;
        var resultJson = result is null ? null : LabJson.Write(SimulationResultDto.From(result));
        var failuresJson = result is null ? null : LabJson.Write(result.Failures);
        var finishedAt = terminal ? DateTimeOffset.UtcNow : (DateTimeOffset?)null;
        // Terminal states are immutable. Late progress or failed notifications must not erase results.
        await db.Runs.Where(x => x.Id == id && (x.Status == RunStatus.Queued || x.Status == RunStatus.Running)).ExecuteUpdateAsync(update => update
            .SetProperty(x => x.Status, status)
            .SetProperty(x => x.Progress, x => terminal ? Math.Clamp(progress, 0, 100) : Math.Max(x.Progress, Math.Clamp(progress, 0, 99)))
            .SetProperty(x => x.ResultJson, resultJson)
            .SetProperty(x => x.FailuresJson, failuresJson)
            .SetProperty(x => x.Error, error)
            .SetProperty(x => x.FinishedAt, finishedAt), ct);
    }

    public Task RecoverInterruptedRunsAsync(CancellationToken ct) => db.Runs
        .Where(x => x.Status == RunStatus.Queued || x.Status == RunStatus.Running)
        .ExecuteUpdateAsync(update => update
            .SetProperty(x => x.Status, RunStatus.Failed)
            .SetProperty(x => x.Error, "Приложение остановлено до завершения запуска.")
            .SetProperty(x => x.FinishedAt, DateTimeOffset.UtcNow), ct);

    private async Task<ScenarioEntity?> LockScenarioAsync(Guid id, CancellationToken ct)
    {
        var rows = await db.Scenarios.FromSqlInterpolated(
            $"SELECT * FROM \"Scenarios\" WHERE \"Id\" = {id} FOR UPDATE")
            .AsNoTracking().ToListAsync(ct);
        return rows.SingleOrDefault();
    }

    public Task<List<RequestFailureDto>> GetFailuresAsync(Guid runId, int offset, int limit, CancellationToken ct = default) =>
        db.Database.SqlQuery<RequestFailureDto>($"""
            SELECT (failure.value->>'requestId')::integer AS "RequestId",
                   (failure.value->>'createdAtMs')::double precision AS "CreatedAtMs",
                   (failure.value->>'completedAtMs')::double precision AS "CompletedAtMs",
                   (failure.value->>'attempts')::integer AS "Attempts",
                   failure.value->>'reason' AS "Reason"
            FROM "Runs" AS run
            CROSS JOIN LATERAL jsonb_array_elements(COALESCE(run."FailuresJson", '[]'::jsonb))
                WITH ORDINALITY AS failure(value, ordinal)
            WHERE run."Id" = {runId} AND failure.ordinal > {offset}
            ORDER BY failure.ordinal
            LIMIT {limit}
            """).ToListAsync(ct);
}
