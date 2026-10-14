using FlowLab.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace FlowLab.Web;

public sealed class DatabaseHealthCheck(LabDbContext db) : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try { return await db.Database.CanConnectAsync(cancellationToken) ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy() : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("PostgreSQL недоступен"); }
        catch { return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("PostgreSQL недоступен"); }
    }
}
