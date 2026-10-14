using FlowLab.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowLab.Infrastructure;

public sealed record WorkspaceInput(string Name);

public sealed class WorkspaceStore(LabDbContext db)
{
    public Task<List<WorkspaceEntity>> ListAsync(CancellationToken ct) => db.Workspaces.AsNoTracking().OrderBy(x => x.CreatedAt).ToListAsync(ct);

    public async Task<WorkspaceEntity> SaveAsync(Guid? id, WorkspaceInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 120)
        {
            throw new ArgumentException("Название пространства должно содержать от 1 до 120 символов.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var workspace = id.HasValue
            ? (await db.Workspaces.FromSqlInterpolated($"SELECT * FROM \"Workspaces\" WHERE \"Id\" = {id.Value} FOR UPDATE").ToListAsync(ct)).SingleOrDefault() ?? throw new KeyNotFoundException("Рабочее пространство не найдено.")
            : new WorkspaceEntity { Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };
        workspace.Name = input.Name.Trim();
        if (!id.HasValue)
        {
            db.Workspaces.Add(workspace);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return workspace;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var workspace = await db.Workspaces.FromSqlInterpolated($"SELECT * FROM \"Workspaces\" WHERE \"Id\" = {id} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        if (workspace.Count == 0)
        {
            return false;
        }
        // Lock scenarios in stable order, also serializing with run creation and scenario deletion.
        var scenarios = await db.Scenarios.FromSqlInterpolated($"SELECT * FROM \"Scenarios\" WHERE \"WorkspaceId\" = {id} ORDER BY \"Id\" FOR UPDATE").AsNoTracking().ToListAsync(ct);
        var ids = scenarios.Select(x => x.Id).ToArray();
        if (await db.Runs.AnyAsync(x => ids.Contains(x.ScenarioId) && (x.Status == RunStatus.Queued || x.Status == RunStatus.Running), ct))
        {
            throw new InvalidOperationException("Сначала остановите активные запуски в рабочем пространстве.");
        }

        await db.Workspaces.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }
}
