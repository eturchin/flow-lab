using FlowLab.Domain;

namespace FlowLab.Tests.Domain;

public sealed class ScenarioValidatorTests
{
    [Fact]
    public void Valid_default_scenario_is_accepted() => Assert.Empty(ScenarioValidator.Validate(new("Example", "", new())));

    [Fact]
    public void Rejects_invalid_ranges_and_duplicate_node_ids()
    {
        var settings = new ScenarioSettings { ArrivalRate = double.NaN, DurationMs = -1, Nodes = [new() { Id = "same", Workers = 0 }, new() { Id = "same", ErrorProbability = 2 }], Resilience = new() { MaxAttempts = 0 } };
        Assert.True(ScenarioValidator.Validate(new("", "", settings)).Count >= 6);
    }

    [Fact]
    public void Queue_must_precede_a_service() => Assert.NotEmpty(ScenarioValidator.Validate(new("Test", "", new() { Nodes = [new(), new() { Kind = NodeKind.Queue }] })));
}
