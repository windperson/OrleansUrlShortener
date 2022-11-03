using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using Orleans;
using Orleans.Runtime;

using UrlShortener.Frontend.Options;

namespace UrlShortener.Frontend.HealthChecks;

public class ClusterHealthCheck : IHealthCheck
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<ClusterHealthCheck> _logger;
    private readonly SiloDeployOption _options;

    public ClusterHealthCheck(IClusterClient clusterClient, IOptions<SiloDeployOption> siloDeployOptions, ILogger<ClusterHealthCheck> logger)
    {
        _clusterClient = clusterClient;
        _options = siloDeployOptions.Value;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = new CancellationToken())
    {
        var managerGrain = _clusterClient.GetGrain<IManagementGrain>(0);
        try
        {
            var count = (await managerGrain.GetHosts(onlyActive: true)).Count;

            return count >= _options.MinSiloCount
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded($"currently only {count} silo(s)");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Error while checking cluster health");
            return HealthCheckResult.Unhealthy("Error while checking cluster health", exception);
        }
    }
}