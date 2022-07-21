using Orleans.Hosting;
using Orleans.TestingHost;

using UrlShortener.Backend.Interfaces;

namespace UrlShortener.Backend.Grains.Test;

public class UrlStoreGrainTest
{
    [Fact]
    public async Task TestUrlStoreGrain()
    {
        // Arrange
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<SiloBuilder>();
        var cluster = builder.Build();
        await cluster.DeployAsync();

        // Act
        var urlStoreGrain = cluster.GrainFactory.GetGrain<IUrlStoreGrain>("a_token");
        await urlStoreGrain.SetUrl("a_token", @"https://www.google.com");
        
        var targetGrain = cluster.GrainFactory.GetGrain<IUrlStoreGrain>("a_token");
        var targetUrl = await targetGrain.GetUrl();

        await cluster.StopAllSilosAsync();

        // Assert
        Assert.Equal(@"https://www.google.com", targetUrl);
    }

    [Fact]
    public async Task TestUrlStoreGrain_No_Given_Url_ThrowKeyNotFoundException()
    {
        // Arrange
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<SiloBuilder>();
        var cluster = builder.Build();
        await cluster.DeployAsync();

        // Act
        var urlStoreGrain = cluster.GrainFactory.GetGrain<IUrlStoreGrain>("not_initialized_token");
        var url = string.Empty;

        async Task CallGetUrlAction()
        {
            url = await urlStoreGrain.GetUrl();
        }

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(CallGetUrlAction);
        await cluster.StopAllSilosAsync();

        // Assert
        Assert.Equal(url, string.Empty);
        Assert.Equal("Url key not exist: not_initialized_token", exception.Message);

    }
}

public class SiloBuilder : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("url-store");
    }
}