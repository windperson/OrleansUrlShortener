using Azure.Identity;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Statistics;

using UrlShortener.Infra.Silo.Options;

namespace UrlShortener.Infra.Silo;

public static class ConfigureSiloExtensions
{
    public static ISiloBuilder UseLocalSingleSilo(this ISiloBuilder siloBuilder)
    {
        siloBuilder.UseLocalhostClustering();
        return siloBuilder;
    }


    public static ISiloBuilder UseAzureTableClusteringInfoStorage(this ISiloBuilder siloBuilder,
        AzureTableClusterOption azureTableClusterOption)
    {
        if (azureTableClusterOption.ServiceUrl.Contains("http://"))
        {
            //Insecure Azure Table Storage clustering
            siloBuilder.ConfigClusterStorageUsingAzureTable(azureTableClusterOption.TableName,
                azureTableClusterOption.ServiceUrl);
        }
        else
        {
            siloBuilder.UseDefaultAzureCredentialTableStorageClustering(azureTableClusterOption);
        }

        return siloBuilder;
    }

    public static ISiloBuilder UseOsEnvironmentStatistics(this ISiloBuilder siloBuilder, ILogger? logger = null)
    {
        logger ??= new NullLogger<WebApplicationBuilder>();
        if (OperatingSystem.IsWindows())
        {
            logger.LogInformation("Using Windows OS Environment Statistics");
            try
            {
                // Use Windows Performance Counters will fail on Azure web apps (Windows) environment
                siloBuilder.UsePerfCounterEnvironmentStatistics();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to use PerfCounterEnvironmentStatistics");
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            logger.LogInformation("Using Linux OS Environment Statistics");
            siloBuilder.UseLinuxEnvironmentStatistics();
        }

        return siloBuilder;
    }

    public static ISiloBuilder UseAzureApplicationInsightLogging(this ISiloBuilder siloBuilder,
        string appInsightConnectionString)
    {
        siloBuilder.AddApplicationInsightsTelemetryConsumer(appInsightConnectionString);
        siloBuilder.ConfigureLogging(loggingBuilder =>
            loggingBuilder.AddApplicationInsights(
                configuration => { configuration.ConnectionString = appInsightConnectionString; },
                options => { options.FlushOnDispose = true; }));
        return siloBuilder;
    }

    #region Private/Internal Methods

    internal static ISiloBuilder ConfigMultipleSilosClustering(this ISiloBuilder siloBuilder,
        SiloNetworkIpPortOption ipPortOption, ClusterOptions clusterOptions, ILogger? logger = null)
    {
        logger ??= new NullLogger<WebApplicationBuilder>();

        logger.LogInformation(
            "Using ListenOnAll: {ListenOnAnyHostAddress},  IP: {SiloIpAddress}, SiloPort: {SiloPort}, GatewayPort: {GatewayPort}",
            ipPortOption.ListenOnAnyHostAddress, ipPortOption.SiloIpAddress, ipPortOption.SiloPort, ipPortOption.GatewayPort);

        if (ipPortOption.ListenOnAnyHostAddress)
        {
            siloBuilder.ConfigureEndpoints(ipPortOption.SiloIpAddress, ipPortOption.SiloPort, ipPortOption.GatewayPort,
                listenOnAnyHostAddress: ipPortOption.ListenOnAnyHostAddress);
        }
        else
        {
            siloBuilder.ConfigureEndpoints(ipPortOption.SiloIpAddress, ipPortOption.SiloPort, ipPortOption.GatewayPort);
        }

        logger.LogInformation("Using ClusterId: {ClusterId}, ServiceId: {ServiceId}",
            clusterOptions.ClusterId, clusterOptions.ServiceId);
        siloBuilder.Configure<ClusterOptions>(options =>
        {
            options.ClusterId = clusterOptions.ClusterId;
            options.ServiceId = clusterOptions.ServiceId;
        });

        return siloBuilder;
    }

    private static ISiloBuilder UseDefaultAzureCredentialTableStorageClustering(this ISiloBuilder siloBuilder,
        AzureTableClusterOption azureTableClusterOption)
    {
        siloBuilder.UseAzureStorageClustering(options =>
        {
            options.TableName = azureTableClusterOption.TableName;
            options.ConfigureTableServiceClient(new Uri(azureTableClusterOption.ServiceUrl),
                new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    ManagedIdentityClientId = azureTableClusterOption.ManagedIdentityClientId
                }));
        });
        return siloBuilder;
    }

    private static ISiloBuilder ConfigClusterStorageUsingAzureTable(this ISiloBuilder siloBuilder, string tableName,
        string connectionString)
    {
        siloBuilder.UseAzureStorageClustering(options =>
        {
            options.TableName = tableName;
            options.ConfigureTableServiceClient(connectionString);
        });
        return siloBuilder;
    }

    #endregion
}