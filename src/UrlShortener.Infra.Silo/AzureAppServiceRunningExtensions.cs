using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Orleans.Configuration;
using Orleans.Hosting;

using UrlShortener.Infra.Silo.Options;

namespace UrlShortener.Infra.Silo;

public static class AzureAppServiceRunningExtensions
{
    public static ISiloBuilder UseAzureAppServiceRunningConfiguration(this ISiloBuilder siloBuilder,
        SiloNetworkIpPortOption siloNetworkIpPortOption, ClusterOptions clusterOptions, ILogger? logger = null)
    {
        logger ??= new NullLogger<WebApplicationBuilder>();

        if (ContainerRunHelper.IsRunningInContainer())
        {
            logger.LogInformation("Silo Instance Running in Container");
            siloNetworkIpPortOption.ListenOnAnyHostAddress = true;

            // NOTE: Be sure to set those Options when Silo running in containerized environment
            // see: https://github.com/dotnet/orleans/issues/7973#issuecomment-1244517617
            siloBuilder.Configure<ClusterMembershipOptions>(options =>
            {
                options.ExtendProbeTimeoutDuringDegradation = true;
                options.EnableIndirectProbes = true;
                // Make Remove dead or defunct silo entry in Cluster membership table faster
                if (int.TryParse(Environment.GetEnvironmentVariable("WEBSITES_CONTAINER_START_TIME_LIMIT"),
                        out var containerStartTimeLimit))
                {
                    var defunctSiloExpiration = TimeSpan.FromSeconds(containerStartTimeLimit * 2);
                    logger.LogInformation("Defunct silos expiration is set to {defunctSiloExpiration }", defunctSiloExpiration);
                    options.DefunctSiloExpiration = defunctSiloExpiration;
                }
            });
        }

        siloBuilder.ConfigMultipleSilosClustering(siloNetworkIpPortOption, clusterOptions, logger);
        return siloBuilder;
    }
}