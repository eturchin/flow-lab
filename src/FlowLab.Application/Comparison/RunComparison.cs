using FlowLab.Domain;

namespace FlowLab.Application;

public static class RunComparison
{
    public static ComparisonDto Compare(RunDto baseline, RunDto candidate)
    {
        if (baseline.ScenarioId != candidate.ScenarioId || baseline.Status != RunStatus.Completed ||
            candidate.Status != RunStatus.Completed || baseline.Result is null || candidate.Result is null)
        {
            throw new ArgumentException("Выберите два завершённых запуска одного сценария.");
        }

        var a = baseline.Result;
        var b = candidate.Result;
        return new(baseline.Id, candidate.Id,
        [
            Change("Пропускная способность, req/s", a.Throughput, b.Throughput),
            Change("p50, мс", a.P50, b.P50),
            Change("p95, мс", a.P95, b.P95),
            Change("p99, мс", a.P99, b.P99),
            Change("Ошибки, %", a.ErrorPercent, b.ErrorPercent),
            Change("Средняя очередь", a.AverageQueue, b.AverageQueue),
            Change("Повторы", a.Retries, b.Retries)
        ]);
    }
    public static MetricChange Change(string name, double a, double b) => new(name, a, b, b - a, a == 0 ? (b == 0 ? 0 : null) : (b - a) / a * 100);
}
