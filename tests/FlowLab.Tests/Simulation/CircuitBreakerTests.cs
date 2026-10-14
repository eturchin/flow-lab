using FlowLab.Simulation;

namespace FlowLab.Tests.Simulation;

public sealed class CircuitBreakerTests
{
    [Fact]
    public void Half_open_allows_one_probe_and_success_closes_breaker()
    {
        var breaker = new CircuitBreaker(2, 100);
        breaker.Complete(false, 0);
        Assert.True(breaker.Allow(1));
        breaker.Complete(false, 1);
        Assert.Equal(CircuitState.Open, breaker.State);
        Assert.False(breaker.Allow(100));
        Assert.True(breaker.Allow(101));
        Assert.Equal(CircuitState.HalfOpen, breaker.State);
        Assert.False(breaker.Allow(102));
        breaker.Complete(true, 103);
        Assert.Equal(CircuitState.Closed, breaker.State);
        Assert.True(breaker.Allow(104));
    }

    [Fact]
    public void Failed_probe_reopens_breaker_and_stale_completions_are_ignored()
    {
        var breaker = new CircuitBreaker(1, 100);
        var oldGeneration = breaker.Generation;
        breaker.Complete(false, 0);
        Assert.True(breaker.Allow(100));
        breaker.Complete(true, 101, oldGeneration);
        Assert.Equal(CircuitState.HalfOpen, breaker.State);
        breaker.Complete(false, 102);
        Assert.Equal(CircuitState.Open, breaker.State);
        Assert.False(breaker.Allow(201));
        Assert.Equal(2, breaker.Opened);
    }
}
