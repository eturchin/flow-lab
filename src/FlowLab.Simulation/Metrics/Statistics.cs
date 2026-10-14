using FlowLab.Domain;

namespace FlowLab.Simulation;

public static class Statistics
{
    public static double Percentile(IReadOnlyList<double> sorted, double p)
    {
        if (!double.IsFinite(p) || p < 0 || p > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(p));
        }

        if (sorted.Count == 0)
        {
            return 0;
        }

        var index = (sorted.Count - 1) * p;
        var lo = (int)Math.Floor(index);
        var hi = (int)Math.Ceiling(index);
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (index - lo);
    }
}
