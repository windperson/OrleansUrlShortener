using System.Net;

using Microsoft.Extensions.Logging.Console;

using Orleans;
using Orleans.Configuration;

using UrlShortener.Infra.Silo;
using UrlShortener.Infra.Silo.Options;

namespace UrlShortener.Backend.SiloHost;

public class Program
{
    public static void Main(string[] args)
    {
        const string appInsightKey = "APPLICATIONINSIGHTS_CONNECTION_STRING";

        // Create logger for application startup process
        using var loggerFactory = LoggerFactory.Create(loggingBuilder =>
        {
            loggingBuilder.AddSimpleConsole(i => i.ColorBehavior = LoggerColorBehavior.Disabled);
        });
        var logger = loggerFactory.CreateLogger<Program>();
        var isInContainer = ContainerRunHelper.IsRunningInContainer();

        var builder = Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostBuilderContext, services) =>
            {
                services.AddApplicationInsightsTelemetryWorkerService();

                // Configure logging based on different environment
                services.AddLogging(logBuilder =>
                {
                    if (isInContainer)
                    {
                        // disable console color in container
                        logBuilder.ClearProviders();
                        logBuilder.AddSimpleConsole(i => i.ColorBehavior = LoggerColorBehavior.Disabled);
                    }

                    var appInsightConnectionString = hostBuilderContext.Configuration.GetValue<string>(appInsightKey);
                    if (!string.IsNullOrEmpty(appInsightConnectionString))
                    {
                        logBuilder.AddApplicationInsights(config => config.ConnectionString = appInsightConnectionString,
                            options => options.FlushOnDispose = true);
                    }

                });
            })
            .UseOrleans((hostBuilderContext, siloBuilder) =>
            {
                // Configure silo
                // First configure silo clustering mechanism, if we cannot find the valid configuration, we will use local silo
                var azureTableClusterOption = hostBuilderContext.GetOptions<AzureTableClusterOption>("AzureTableCluster");
                if (!string.IsNullOrEmpty(azureTableClusterOption.ServiceUrl))
                {
                    siloBuilder.UseAzureTableClusteringInfoStorage(azureTableClusterOption);
                }
                else if (hostBuilderContext.HostingEnvironment.IsDevelopment())
                {
                    siloBuilder.UseLocalSingleSilo();
                }

                var clusterOptions = new ClusterOptions { ClusterId = "cluster-single-slot", ServiceId = "OrleansUrlShortener" };

                // GET Azure Container Instance exposed port via a environment variable we set when provision the ACI using Bicep
                var exposedPorts = hostBuilderContext.Configuration.GetValue<string>("ACI:PORTS").Split(",").Select(int.Parse).ToArray();

                var siloIpAddr = IPAddress.Loopback;
                var nodeIpOrFQDN = Environment.GetEnvironmentVariable("Fabric_NodeIPOrFQDN");
                if (!string.IsNullOrEmpty(nodeIpOrFQDN))
                {
                    siloIpAddr = IPAddress.Parse(nodeIpOrFQDN);
                }
                var siloNetworkIpPortOption = new SiloNetworkIpPortOption()
                {
                    SiloIpAddress = siloIpAddr,
                    ListenOnAnyHostAddress = true,
                    SiloPort = exposedPorts[0],
                    GatewayPort = exposedPorts[1]
                };
                siloBuilder.UseAzureContainerInstanceRunningConfiguration(siloNetworkIpPortOption, clusterOptions, logger);

                // Configure Grain Storage mechanism
                var urlStoreGrainOption = hostBuilderContext.GetOptions<UrlStoreGrainOption>("UrlStoreGrain");
                siloBuilder.SetGrainStorageUsingAzureTable("url-store", urlStoreGrainOption);

                var appInsightConnectionString = hostBuilderContext.Configuration.GetValue<string>(appInsightKey);
                if (!string.IsNullOrEmpty(appInsightConnectionString))
                {
                    siloBuilder.UseAzureApplicationInsightLogging(appInsightConnectionString);
                }

                // Add instruments for Orleans Dashboard for system metrics
                siloBuilder.UseOsEnvironmentStatistics(logger);
                siloBuilder.UseDashboard(dashboardOptions =>
                {
                    dashboardOptions.HostSelf = false;
                });
            });

        if (isInContainer)
        {
            builder.UseConsoleLifetime();
        }

        var host = builder.Build();

        host.Run();
    }
}