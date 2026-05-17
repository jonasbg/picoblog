using Microsoft.Extensions.Diagnostics.HealthChecks;

public class MarkdownScanHealthCheck : IHealthCheck
{
    private readonly MonitorLoop _monitorLoop;

    public MarkdownScanHealthCheck(MonitorLoop monitorLoop)
    {
        _monitorLoop = monitorLoop;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        if (_monitorLoop.HasCompletedScan)
            return Task.FromResult(HealthCheckResult.Healthy("Markdown scan completed"));

        return Task.FromResult(HealthCheckResult.Unhealthy("Markdown scan has not completed"));
    }
}
