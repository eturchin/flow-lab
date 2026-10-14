using FlowLab.Simulation;

namespace FlowLab.Tests.Simulation;

public sealed class StatisticsTests
{
    [Fact]
    public void Percentile_handles_empty_singleton_and_interpolation()
    {
        Assert.Equal(0, Statistics.Percentile([], .99));
        Assert.Equal(7, Statistics.Percentile([7], .99));
        Assert.Equal(25, Statistics.Percentile([10, 20, 30, 40], .5));
        Assert.Equal(38.5, Statistics.Percentile([10, 20, 30, 40], .95), 8);
    }
}
