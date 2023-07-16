using System.Net;

using Azure.Identity;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Console;

using Orleans;
using Orleans.Configuration;
using Orleans.Runtime;

using OrleansDashboard.Metrics;
using OrleansDashboard.Metrics.Details;

using UrlShortener.Backend.SiloHost.DashboardImpl;
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
            .ConfigureAppConfiguration(configBuilder =>
            {
                var configuration = configBuilder.Build();
                var appConfigStoreConn = configuration.GetConnectionString("AppConfigStore");
                if (string.IsNullOrEmpty(appConfigStoreConn))
                {
                    return;
                }

                logger.LogInformation("Found AppConfig connection string, adding Azure App Configuration as configuration source");
                if (appConfigStoreConn.StartsWith("http"))
                {
                    var userAssignedManagedIdentity = Environment.GetEnvironmentVariable("AppConfigStore__ManagedIdentityClientId");
                    configBuilder.AddAzureAppConfiguration(option =>
                            option.Connect(new Uri(appConfigStoreConn), new ManagedIdentityCredential(userAssignedManagedIdentity)));
                }
                else
                {
                    configBuilder.AddAzureAppConfiguration(appConfigStoreConn);
                }
            })
            .ConfigureServices((hostBuilderContext, services) =>
            {
                var appInsightConnectionString = hostBuilderContext.Configuration.GetValue<string>(appInsightKey);
                if (!string.IsNullOrEmpty(appInsightConnectionString))
                {
                    services.SetAzureAppInsightRoleName("Backend Silo Host");
                    services.AddApplicationInsightsTelemetryWorkerService(config =>
                    {
                        config.ConnectionString = appInsightConnectionString;
                        config.EnableHeartbeat = true;
                    });
                }

                // Configure logging based on different environment
                services.AddLogging(logBuilder =>
                {
                    if (isInContainer)
                    {
                        // disable console color in container
                        logBuilder.ClearProviders();
                        logBuilder.AddSimpleConsole(i => i.ColorBehavior = LoggerColorBehavior.Disabled);
                    }

                    if (string.IsNullOrEmpty(appInsightConnectionString))
                    {
                        return;
                    }

                    logger.LogInformation("Add Application Insight as a logger sink");
                    logBuilder.AddApplicationInsights(config => config.ConnectionString = appInsightConnectionString,
                        options => options.FlushOnDispose = true);

                });


                RegisterDashboardService(services);
            })
            .UseOrleans((hostBuilderContext, siloBuilder) =>
            {
                // Configure silo
                // First configure silo clustering mechanism according to current configuration,
                // If we cannot find the valid configuration, use local dev silo
                var azureTableClusterOption = hostBuilderContext.GetOptions<AzureTableClusterOption>("AzureTableCluster");
                if (!string.IsNullOrEmpty(azureTableClusterOption.ServiceUrl))
                {
                    siloBuilder.UseAzureTableClusteringInfoStorage(azureTableClusterOption);
                }
                else if (hostBuilderContext.HostingEnvironment.IsDevelopment())
                {
                    siloBuilder.UseLocalSingleSilo();
                }
                var clusterOptions = hostBuilderContext.GetOptions<ClusterOptions>("OrleansCluster");
                if (string.IsNullOrEmpty(clusterOptions.ClusterId) || string.IsNullOrEmpty(clusterOptions.ServiceId))
                {
                    logger.LogInformation("No Orleans Cluster Id or Service Id found in configuration, using local single slot mode clusterOptions value");
                    clusterOptions = new ClusterOptions { ClusterId = "cluster-single-slot", ServiceId = "OrleansUrlShortener" };
                }

                // GET Azure Container Instance exposed port
                var exposedPorts = hostBuilderContext.Configuration.GetSection("ACI:OpenPorts").Get<int[]>();

                if (exposedPorts == null || exposedPorts.Length < 2)
                {
                    throw new InvalidOperationException("Cannot find exposed ports configuration for Orleans silo");
                }

                // Try to infer the silo IP address from known environment variable that should exist on ACI,
                // If not found, try to get the first meaningful container IP address
                var siloIpAddr = IPAddress.Loopback;
                var vnetIp = Environment.GetEnvironmentVariable("Fabric_NET-0-[Delegated]");
                var nodeIpOrFQDN = Environment.GetEnvironmentVariable("Fabric_NodeIPOrFQDN");
                if (!string.IsNullOrEmpty(vnetIp))
                {
                    logger.LogInformation("Use vnet IP as Orleans Silo IP");
                    siloIpAddr = IPAddress.Parse(vnetIp);
                }
                else if (!string.IsNullOrEmpty(nodeIpOrFQDN))
                {
                    logger.LogInformation("Use vnet IP as Orleans Silo IP");
                    siloIpAddr = IPAddress.Parse(nodeIpOrFQDN);
                }
                else if (isInContainer)
                {
                    var containerIp = ContainerRunHelper.GetFirstAccesibleContainerIpAddress();
                    if (containerIp != null)
                    {
                        siloIpAddr = containerIp;
                    }
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

                // Add instruments for Orleans Dashboard to see system metrics data
                siloBuilder.UseOsEnvironmentStatistics(logger);
            });

        if (isInContainer)
        {
            builder.UseConsoleLifetime();
        }

        var host = builder.Build();

        host.Run();
    }

    /// <summary>
    /// Configure Orleans grain profiler and some other DI service for OrleansDashboard functionality
    /// </summary>
    /// <param name="services"></param>
    private static void RegisterDashboardService(IServiceCollection services)
    {
        services.AddSingleton<SiloStatusOracleSiloDetailsProvider>();
        services.AddSingleton<MembershipTableSiloDetailsProvider>();
        services.AddSingleton<IGrainProfiler, GrainProfiler>();
        services.AddSingleton(c => (ILifecycleParticipant<ISiloLifecycle>)c.GetRequiredService<IGrainProfiler>());
        services.AddSingleton<IIncomingGrainCallFilter, GrainProfilerFilter>();
        services.AddSingleton<ISiloDetailsProvider>(c =>
        {
            var membershipTable = c.GetService<IMembershipTable>();

            return membershipTable != null
                ? c.GetRequiredService<MembershipTableSiloDetailsProvider>()
                : c.GetRequiredService<SiloStatusOracleSiloDetailsProvider>();
        });

        services.TryAddSingleton(GrainProfilerFilter.NoopOldGrainMethodFormatter);
        services.TryAddSingleton(GrainProfilerFilter.DefaultGrainMethodFormatter);
    }
}