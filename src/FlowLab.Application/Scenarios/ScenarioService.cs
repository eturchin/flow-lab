using FlowLab.Domain;

namespace FlowLab.Application;

public sealed class ScenarioService(ILabStore store)
{
    public Task<List<ScenarioDto>> ListAsync(CancellationToken ct) => store.ListScenariosAsync(ct);
    public Task<ScenarioDto?> GetAsync(Guid id, CancellationToken ct) => store.GetScenarioAsync(id, ct);
    public Task<bool> DeleteAsync(Guid id, CancellationToken ct) => store.DeleteScenarioAsync(id, ct);

    public Task<ScenarioDto> SaveAsync(Guid? id, ScenarioInput input, CancellationToken ct)
    {
        var errors = ScenarioValidator.Validate(input);
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors));
        }

        return store.SaveScenarioAsync(id, input, ct);
    }
}
