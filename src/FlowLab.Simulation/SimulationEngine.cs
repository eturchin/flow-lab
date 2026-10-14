using FlowLab.Domain;

namespace FlowLab.Simulation;

/// <summary>A stateless entry point. Every run owns its calendar, random source and mutable state.</summary>
public sealed class SimulationEngine
{
    public SimulationResult Run(ScenarioSettings settings, Action<SimulationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var errors = ScenarioValidator.Validate(new("Simulation", "", settings));
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors));
        }

        return new SimulationSession(settings, progress, cancellationToken).Execute();
    }
}
