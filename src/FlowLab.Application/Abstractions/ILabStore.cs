using FlowLab.Domain;

namespace FlowLab.Application;

public interface ILabStore
{
    Task<List<ScenarioDto>> ListScenariosAsync(CancellationToken ct = default);
    Task<ScenarioDto?> GetScenarioAsync(Guid id, CancellationToken ct = default);
    Task<ScenarioDto> SaveScenarioAsync(Guid? id, ScenarioInput input, CancellationToken ct = default);
    Task<bool> DeleteScenarioAsync(Guid id, CancellationToken ct = default);
    Task<RunDto> CreateRunAsync(Guid scenarioId, CancellationToken ct = default);
    Task<RunDto?> GetRunAsync(Guid id, CancellationToken ct = default);
    Task<List<RunDto>> ListRunsAsync(Guid scenarioId, CancellationToken ct = default);
    Task<List<RequestFailureDto>> GetFailuresAsync(Guid runId, int offset, int limit, CancellationToken ct = default);
    Task UpdateRunAsync(Guid id, RunStatus status, double progress, SimulationResult? result = null, string? error = null, CancellationToken ct = default);
    Task RecoverInterruptedRunsAsync(CancellationToken ct);
}
