using System.Net;

using Azure.Identity;

using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging.Console;

using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Statistics;

using UrlShortener.Backend.Interfaces;
using UrlShortener.Frontend.HealthChecks;
using UrlShortener.Frontend.Options;

namespace UrlShortener.Frontend;

public class Program
{
    public static void Main(string[] args)
    {
        const string appInsightKey = "APPLICATIONINSIGHTS_CONNECTION_STRING";
        var builder = WebApplication.CreateBuilder(args);

        // Create logger for application startup process
        using var loggerFactory = LoggerFactory.Create(loggingBuilder =>
        {
            loggingBuilder.AddSimpleConsole(i => i.ColorBehavior = LoggerColorBehavior.Disabled);
            loggingBuilder.AddAzureWebAppDiagnostics();
        });
        var logger = loggerFactory.CreateLogger<Program>();
        

        #region Configure Orleans Silo

        builder.Host.UseOrleans((hostBuilderContext, siloBuilder) =>
        {
            var urlStoreGrainOption = new UrlStoreGrainOption();
            hostBuilderContext.Configuration.GetSection("UrlStoreGrain").Bind(urlStoreGrainOption);

            // Azure web app will set these environment variables when it has virtual network integration configured
            // https://learn.microsoft.com/en-us/azure/app-service/reference-app-settings?tabs=kudu%2Cdotnet#networking
            var privateIpStr = Environment.GetEnvironmentVariable("WEBSITE_PRIVATE_IP");
            var privatePort = Environment.GetEnvironmentVariable("WEBSITE_PRIVATE_PORTS")?.Split(',');
            if (IPAddress.TryParse(privateIpStr, out var ipAddress) &&
                privatePort is { Length: >= 2 }
                && int.TryParse(privatePort[0], out var siloPort) && int.TryParse(privatePort[1], out var gatewayPort))
            {
                logger.LogInformation(
                    "Using private IP address {ipAddress} for silo port {siloPort} and gateway port {gatewayPort}", ipAddress,
                    siloPort, gatewayPort);
                string clusterId = $"cluster-{Environment.GetEnvironmentVariable("WEBSITE_DEPLOYMENT_ID")}";
                const string serviceId = "OrleansUrlShortener";
                logger.LogInformation("Using cluster id '{clusterId}' and service id '{serviceId}'", clusterId, serviceId);
                siloBuilder.ConfigureEndpoints(ipAddress, siloPort, gatewayPort);

                var azureTableClusterOption = new AzureTableClusterOption();
                hostBuilderContext.Configuration.GetSection("AzureTableCluster").Bind(azureTableClusterOption);
                siloBuilder.UseAzureStorageClustering(options =>
                    {
                        options.TableName = azureTableClusterOption.TableName;
                        options.ConfigureTableServiceClient(new Uri(azureTableClusterOption.ServiceUrl),
                            new DefaultAzureCredential(new DefaultAzureCredentialOptions
                            {
                                ManagedIdentityClientId = azureTableClusterOption.ManagedIdentityClientId
                            }));
                    })
                    .Configure<ClusterOptions>(options =>
                    {
                        options.ClusterId = clusterId;
                        options.ServiceId = serviceId;
                    });
            }
            else if (hostBuilderContext.HostingEnvironment.IsDevelopment())
            {
                siloBuilder.UseLocalhostClustering();
            }

            siloBuilder.AddAzureTableGrainStorage(
                name: "url-store",
                configureOptions: options =>
                {
                    options.TableName =
                        urlStoreGrainOption.TableName; // if not set, default will be "OrleansGrainState" table name
                    options.UseJson = true;

                    // use this configuration if you only want to use local http only Azurite Azure Table Storage emulator
                    // options.ConfigureTableServiceClient("DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;");
                    options.ConfigureTableServiceClient(new Uri(urlStoreGrainOption.ServiceUrl),
                        new DefaultAzureCredential(new DefaultAzureCredentialOptions
                        {
                            ManagedIdentityClientId = urlStoreGrainOption.ManagedIdentityClientId
                        }));
                });

            var appInsightConnectionString = hostBuilderContext.Configuration.GetValue<string>(appInsightKey);
            if (!string.IsNullOrEmpty(appInsightConnectionString))
            {
                siloBuilder.AddApplicationInsightsTelemetryConsumer(appInsightConnectionString);
                siloBuilder.ConfigureLogging(loggingBuilder =>
                    loggingBuilder.AddApplicationInsights(
                        configuration => { configuration.ConnectionString = appInsightConnectionString; },
                        options => { options.FlushOnDispose = true; }));
            }
            else
            {
                //Add instruments for Orleans Dashboard when running locally
                if (OperatingSystem.IsWindows())
                {
                    siloBuilder.UsePerfCounterEnvironmentStatistics();
                }
                else if (OperatingSystem.IsLinux())
                {
                    siloBuilder.UseLinuxEnvironmentStatistics();
                }
            }

            // must declare HostSelf false for Orleans Silo Host load DashboardGrain properly on Azure Web App
            siloBuilder.UseDashboard(dashboardOptions =>
            {
                dashboardOptions.HostSelf = false;
            });
        });

        #endregion

        var appInsightConnectionString = builder.Configuration.GetValue<string>(appInsightKey);
        if (!string.IsNullOrEmpty(appInsightConnectionString))
        {
            //we use Application insight's configuration to know if we are running on Azure Web App or locally
            builder.Services.AddApplicationInsightsTelemetry(options => options.ConnectionString = appInsightConnectionString);
            builder.Logging.AddApplicationInsights(config => { config.ConnectionString = appInsightConnectionString; },
                options => { options.FlushOnDispose = true; });
            builder.Logging.AddAzureWebAppDiagnostics();
        }

        builder.Services.Configure<SiloDeployOption>(builder.Configuration.GetSection("SiloDeploy"));
        // Add ASP.Net Core Check Healthy Service
        builder.Services.AddHealthChecks()
            .AddCheck<GrainHealthCheck>("Orleans_GrainHealthCheck")
            .AddCheck<SiloHealthCheck>("Orleans_SiloHealthCheck")
            .AddCheck<ClusterHealthCheck>("Orleans_ClusterHealthCheck");

        var app = builder.Build();
        app.MapHealthChecks("/healthz");

        const string orleansDashboardPath = @"orleansDashboard";
        app.UseOrleansDashboard(new OrleansDashboard.DashboardOptions { BasePath = orleansDashboardPath });

        #region Web Url Endpoints

        app.MapGet("/", async (HttpContext context) =>
        {
            //remove postfix query string that incur by Facebook sharing 
            var baseUrlBuilder = new UriBuilder(new Uri(context.Request.GetDisplayUrl())) { Query = "" };
            var baseUrl = baseUrlBuilder.Uri.ToString();

            await context.Response.WriteAsync(
                $" Type \"{baseUrl}shorten/{{your original url}}\" in address bar to get your shorten url.\r\n\r\n"
                + $" Orleans Dashboard: \"{baseUrl}{orleansDashboardPath}\" ");
        });

        app.MapMethods("/shorten/{*path}", new[] { "GET" }, async (HttpRequest req, IGrainFactory grainFactory, string path) =>
        {
            var shortenedRouteSegment = Guid.NewGuid().GetHashCode().ToString("X");
            var urlStoreGrain = grainFactory.GetGrain<IUrlStoreGrain>(shortenedRouteSegment);
            await urlStoreGrain.SetUrl(shortenedRouteSegment, path);
            var resultBuilder = new UriBuilder(req.GetEncodedUrl()) { Path = $"/go/{shortenedRouteSegment}" };
            return Results.Ok(resultBuilder.Uri);
        });

        app.MapGet("/go/{shortenUriSegment}", async (string shortenUriSegment, IGrainFactory grainFactory) =>
        {
            var urlStoreGrain = grainFactory.GetGrain<IUrlStoreGrain>(shortenUriSegment);
            try
            {
                var url = await urlStoreGrain.GetUrl();
                return Results.Redirect(url);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound("Url not found");
            }
        });

        #endregion

        app.Run();
    }
}