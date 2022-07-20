using Microsoft.AspNetCore.Http.Extensions;

using Orleans;
using Orleans.Hosting;
using Orleans.Statistics;

using UrlShortener.Backend.Interfaces;

namespace UrlShortener.Frontend;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Host.UseOrleans(siloBuilder =>
        {
            siloBuilder.UseLocalhostClustering();
            siloBuilder.AddMemoryGrainStorage("url-store");
            
            //Add Orleans Dashboard
            if (OperatingSystem.IsWindows())
            {
                siloBuilder.UsePerfCounterEnvironmentStatistics();
            }
            else if (OperatingSystem.IsLinux())
            {
                siloBuilder.UseLinuxEnvironmentStatistics();
            }
            siloBuilder.UseDashboard();
        });

        var app = builder.Build();

        app.MapGet("/", () => "Type \"/shorten/{your original url}\" in address bar to get your shorten url.");

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