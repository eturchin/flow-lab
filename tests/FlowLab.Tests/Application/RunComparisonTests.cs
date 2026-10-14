using FlowLab.Application;
using FlowLab.Domain;
using FlowLab.Simulation;

namespace FlowLab.Tests.Application;

public sealed class RunComparisonTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(10, 15, 50)]
    [InlineData(10, 5, -50)]
    public void Comparison_reports_absolute_and_relative_change(double baseline, double candidate, double percent)
    {
        var change = RunComparison.Change("Test", baseline, candidate);
        Assert.Equal(candidate - baseline, change.Absolute);
        Assert.Equal(percent, change.Percent);
    }

    [Fact]
    public void Comparison_does_not_divide_by_zero() => Assert.Null(RunComparison.Change("Test", 0, 1).Percent);

    [Fact]
    public void Cannot_compare_different_scenarios_or_incomplete_runs()
    {
        var snapshot = new ScenarioInput("Test", "", new());
        var run = new RunDto(Guid.NewGuid(), Guid.NewGuid(), RunStatus.Completed, 100, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, snapshot, SimulationResultDto.From(new SimulationEngine().Run(snapshot.Settings)), null);
        Assert.Throws<ArgumentException>(() => RunComparison.Compare(run, run with { ScenarioId = Guid.NewGuid() }));
        Assert.Throws<ArgumentException>(() => RunComparison.Compare(run, run with { Result = null }));
    }
}
