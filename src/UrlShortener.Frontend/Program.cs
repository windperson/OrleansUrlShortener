using System.Net;

using Azure.Identity;

using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging.Console;

using Orleans;
using Orleans.Configuration;

using UrlShortener.Backend.Interfaces;
using UrlShortener.Frontend.HealthChecks;
using UrlShortener.Frontend.Options;
using UrlShortener.Infra.Silo;
using UrlShortener.Infra.Silo.Options;

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

        var isInContainer = ContainerRunHelper.IsRunningInContainer();
        if (isInContainer)
        {
            builder.Host.UseConsoleLifetime();
            // either use the following code or set the environment variable "ASPNETCORE_LOGGING__CONSOLE__DISABLECOLORS" to true in starting container
            // builder.Logging.ClearProviders();
            // builder.Logging.AddSimpleConsole(i => i.ColorBehavior = LoggerColorBehavior.Disabled);
        }

        var appConfigStoreConn = builder.Configuration.GetConnectionString("AppConfigStore");
        if (!string.IsNullOrEmpty(appConfigStoreConn))
        {
            logger.LogInformation("Found AppConfig connection string, adding Azure App Configuration as configuration source");
            if (appConfigStoreConn.StartsWith("http"))
            {
                var userAssignedManagedIdentity = Environment.GetEnvironmentVariable("AppConfigStore__ManagedIdentityClientId");
                builder.Configuration.AddAzureAppConfiguration(option => option.Connect(new Uri(appConfigStoreConn), new ManagedIdentityCredential(userAssignedManagedIdentity)));
            }
            else
            {
                builder.Configuration.AddAzureAppConfiguration(appConfigStoreConn);
            }
        }

        #region Configure Orleans Silo

        builder.Host.UseOrleans((hostBuilderContext, siloBuilder) =>
        {
            var siloIpPortOption = hostBuilderContext.GetOptions<SiloNetworkIpPortOption>("SiloNetworkIpPort");

            var clusterOptions = hostBuilderContext.GetOptions<ClusterOptions>("OrleansCluster");
            if (string.IsNullOrEmpty(clusterOptions.ClusterId) || string.IsNullOrEmpty(clusterOptions.ServiceId))
            {
                logger.LogInformation("No Orleans Cluster Id or Service Id found in configuration, use some built-in system environment variables to construct clusterOptions");
                clusterOptions = ClusterOptionsHelper.CreateClusterOptions("cluster-", "OrleansUrlShortener");
            }

            // Configure silo
            if (SiloNetworkIpPortOptionHelper.HasAzureWebAppSiloNetworkIpPortOption(out var siloNetworkIpPortOption))
            {
                siloBuilder.UseAzureAppServiceRunningConfiguration(siloNetworkIpPortOption, clusterOptions, logger);
            }
            else if (SiloIpAddressIsAny(siloIpPortOption.SiloIpAddress) || siloIpPortOption.ListenOnAnyHostAddress)
            {
                if (isInContainer)
                {
                    var containerIp = ContainerRunHelper.GetFirstAccesibleContainerIpAddress();
                    if (containerIp != null)
                    {
                        siloIpPortOption.SiloIpAddress = containerIp;
                    }
                }
                else
                {
                    // Fail back to use localhost address
                    siloIpPortOption.SiloIpAddress = IPAddress.Loopback;
                }

                siloBuilder.UseAzureAppServiceRunningConfiguration(siloIpPortOption, clusterOptions, logger);
            }
            else if (hostBuilderContext.HostingEnvironment.IsDevelopment())
            {
                siloBuilder.UseLocalSingleSilo();
            }

            var azureTableClusterOption = hostBuilderContext.GetOptions<AzureTableClusterOption>("AzureTableCluster");
            if (!string.IsNullOrEmpty(azureTableClusterOption.ServiceUrl))
            {
                siloBuilder.UseAzureTableClusteringInfoStorage(azureTableClusterOption);
            }

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

            // Must declare HostSelf false for Orleans Silo Host load DashboardGrain properly on Azure Web App
            siloBuilder.UseDashboard(dashboardOptions =>
            {
                dashboardOptions.HostSelf = false;
            });
        });

        #endregion

        var appInsightConnectionString = builder.Configuration.GetValue<string>(appInsightKey);
        // We use Application insight's configuration to know if we are running on Azure Web App or locally
        if (!string.IsNullOrEmpty(appInsightConnectionString))
        {
            builder.Services.SetAzureAppInsightRoleName("Frontend Web API");
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

        if (app.Environment.IsProduction() && !isInContainer)
        {
            app.UseHsts();
        }

        // Azure App Service (Linux) doesn't use HTTPS inside container, https redirection is automatically handled by Azure App Service
        if (!isInContainer)
        {
            app.UseHttpsRedirection();
        }

        #region Web Url Endpoints

        app.MapGet("/", async (HttpContext context) =>
        {
            //remove postfix query string that incur by Facebook sharing 
            var baseUrlBuilder = new UriBuilder(new Uri(context.Request.GetDisplayUrl())) { Query = "" };
            var baseUrl = baseUrlBuilder.Uri.ToString();

            context.Response.ContentType = "text/html";
            var dashboardUrl = $"{baseUrl}{orleansDashboardPath}";

            await context.Response.WriteAsync(
                "<html lang=\"en\"><head><title>.NET 6 Orleans url shortener</title><meta http-equiv=\"content-language\" content=\"en-us\"/></head>" +
                $"<body>Type <code>\"{baseUrl}shorten/{{your original url}}\"</code> in address bar to get your shorten url.<br/><br/>" +
                $" Orleans Dashboard: <a href=\"{dashboardUrl}\" target=\"_blank\">{dashboardUrl}</a>" +
                "<div>ver <b>1.0</b></div></body></html>");
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

    private static bool SiloIpAddressIsAny(IPAddress address)
    {
        // Refactored conditional statement into a separate boolean helper method, to better clarify the intent of the code.
        return address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any);
    }
}