using Azure.Identity;

using Microsoft.AspNetCore.Http.Extensions;

using Orleans;
using Orleans.Hosting;
using Orleans.Statistics;

using UrlShortener.Backend.Interfaces;
using UrlShortener.Frontend.Options;

namespace UrlShortener.Frontend;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        #region Configure Orleans Silo

        builder.Host.UseOrleans((hostBuilderContext, siloBuilder) =>
        {
            var urlStoreGrainOption = new UrlStoreGrainOption();
            hostBuilderContext.Configuration.GetSection("UrlStoreGrain").Bind(urlStoreGrainOption);

            siloBuilder.UseLocalhostClustering();
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
                     new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = urlStoreGrainOption.ManagedIdentityClientId }));
                });

            var azureApplicationInsightKey = hostBuilderContext.Configuration.GetValue<string>("APPINSIGHTS_INSTRUMENTATIONKEY");
            if (!string.IsNullOrEmpty(azureApplicationInsightKey))
            {
                siloBuilder.AddApplicationInsightsTelemetryConsumer(azureApplicationInsightKey);
                siloBuilder.ConfigureLogging((context, loggingBuilder) => loggingBuilder.AddApplicationInsights(azureApplicationInsightKey));
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

            // must declare for Orleans Silo Host load DashboardGrain properly
            siloBuilder.UseDashboard(dashboardOptions =>
            {
                dashboardOptions.HostSelf = false;
            });
        });

        #endregion

        var azureApplicationInsightKey = builder.Configuration.GetValue<string>("APPINSIGHTS_INSTRUMENTATIONKEY");
        if (!string.IsNullOrEmpty(azureApplicationInsightKey))
        {
            //we use Application insight key configuration to know if we are running on Azure Web App or locally
            builder.Services.AddApplicationInsightsTelemetry(azureApplicationInsightKey);
            builder.Logging.AddApplicationInsights(azureApplicationInsightKey);
            builder.Logging.AddAzureWebAppDiagnostics();
        }

        builder.Logging.AddAzureWebAppDiagnostics();
        var app = builder.Build();

        var orleansDashboardPath = @"orleansDashboard";
        app.UseOrleansDashboard(new OrleansDashboard.DashboardOptions
        {
            BasePath = orleansDashboardPath,
        });

        app.MapGet("/", async (HttpContext context) =>
        {
            //remove postfix query string that incur by Facebook sharing 
            var baseUrlBuilder = new UriBuilder(new Uri(context.Request.GetDisplayUrl()));
            baseUrlBuilder.Query = "";
            var baseUrl = baseUrlBuilder.Uri.ToString();

            await context.Response.WriteAsync($" Type \"{baseUrl}shorten/{{your original url}}\" in address bar to get your shorten url.\r\n\r\n"
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

        app.Run();
    }
}