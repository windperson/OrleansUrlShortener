using Microsoft.Extensions.Diagnostics.HealthChecks;

using Orleans.Runtime;

namespace UrlShortener.Frontend.HealthChecks;

public class SiloHealthCheck : IHealthCheck
{
    private readonly IEnumerable<IHealthCheckParticipant> _participants;
    private static long s_lastCheckTime = DateTime.UtcNow.ToBinary();

    public SiloHealthCheck(IEnumerable<IHealthCheckParticipant> participants)
    {
        _participants = participants;
    }
    
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
    {
        var thisCheckTime = DateTime.FromBinary(Interlocked.Exchange(ref s_lastCheckTime, DateTime.UtcNow.ToBinary()));

        foreach (var participant in _participants)
        {
            if(!participant.CheckHealth(thisCheckTime, out var reason))
            {
                return Task.FromResult(HealthCheckResult.Degraded(reason));
            }
        }
        
        return Task.FromResult(HealthCheckResult.Healthy());
    }
}