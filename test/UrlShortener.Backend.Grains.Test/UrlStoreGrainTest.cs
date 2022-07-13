using Orleans.Hosting;
using Orleans.TestingHost;

using UrlShortener.Backend.Interfaces;

namespace UrlShortener.Backend.Grains.Test;

public class UrlStoreGrainTest
{
    [Fact]
    public async Task TestUrlStoreGrain()
    {
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<SiloBuilder>();
        var cluster = builder.Build();
        await cluster.DeployAsync();

        var urlStoreGrain = cluster.GrainFactory.GetGrain<IUrlStoreGrain>("a_token");
        await urlStoreGrain.SetUrl("a_token", @"https://www.google.com");
        
        var targetGrain = cluster.GrainFactory.GetGrain<IUrlStoreGrain>("a_token");
        var targetUrl = await targetGrain.GetUrl();

        await cluster.StopAllSilosAsync();

        Assert.Equal(@"https://www.google.com", targetUrl);
    }
}

public class SiloBuilder : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("url-store");
    }
}