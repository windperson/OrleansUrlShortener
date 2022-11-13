using Microsoft.Extensions.Diagnostics.HealthChecks;

using Orleans;

using UrlShortener.Backend.Grains;

namespace UrlShortener.Frontend.HealthChecks;

public class GrainHealthCheck : IHealthCheck
{
    private readonly IClusterClient _clusterClient;

    public GrainHealthCheck(IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
    }
        
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new())
    {
        try
        {
            await _clusterClient.GetGrain<ILocalHealthCheckGrain>(0).PingAsync();
        }
        catch (Exception error)
        {
            return HealthCheckResult.Unhealthy("Grain health check failed", error);
        }

        return HealthCheckResult.Healthy();
    }
}
