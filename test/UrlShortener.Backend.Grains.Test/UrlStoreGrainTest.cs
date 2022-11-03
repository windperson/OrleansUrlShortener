using Orleans.Hosting;
using Orleans.TestingHost;

using UrlShortener.Backend.Interfaces;

namespace UrlShortener.Backend.Grains.Test;

public class UrlStoreGrainTest
{
    [Fact]
    public async Task TestUrlStoreGrainNormalOperation()
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
    public async Task TestUrlStoreGrain_AutoPrefix_HttpScheme()
    {
        // Arrange
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<SiloBuilder>();
        var cluster = builder.Build();
        await cluster.DeployAsync();

        // Act
        var urlStoreGrain = cluster.GrainFactory.GetGrain<IUrlStoreGrain>("a_token");
        await urlStoreGrain.SetUrl("a_token", @"www.github.com");

        var targetGrain = cluster.GrainFactory.GetGrain<IUrlStoreGrain>("a_token");
        var targetUrl = await targetGrain.GetUrl();

        await cluster.StopAllSilosAsync();

        // Assert
        Assert.Equal(@"http://www.github.com", targetUrl);
    }

    [Fact]
    public async Task TestUrlStoreGrain_Handle_Sanitized_Url()
    {
        // Arrange
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<SiloBuilder>();
        var cluster = builder.Build();
        await cluster.DeployAsync();

        // Act
        var urlStoreGrainA = cluster.GrainFactory.GetGrain<IUrlStoreGrain>("a_token");
        await urlStoreGrainA.SetUrl("a_token", @"http/www.google.com");
        var urlA = await urlStoreGrainA.GetUrl();

        var urlStoreGrainB = cluster.GrainFactory.GetGrain<IUrlStoreGrain>("b_token");
        await urlStoreGrainB.SetUrl("b_token", @"https/www.google.com");
        var urlB = await urlStoreGrainB.GetUrl();

        var urlStoreGrainC = cluster.GrainFactory.GetGrain<IUrlStoreGrain>("c_token");
        await urlStoreGrainC.SetUrl("c_token", @"http:/www.google.com");
        var urlC = await urlStoreGrainC.GetUrl();

        var urlStoreGrainD = cluster.GrainFactory.GetGrain<IUrlStoreGrain>("d_token");
        await urlStoreGrainD.SetUrl("d_token", @"https:/www.google.com");
        var urlD = await urlStoreGrainD.GetUrl();
        await cluster.StopAllSilosAsync();

        // Assert
        Assert.Equal(@"http://www.google.com", urlA);
        Assert.Equal(@"https://www.google.com", urlB);
        Assert.Equal(@"http://www.google.com", urlC);
        Assert.Equal(@"https://www.google.com", urlD);
    }

    [Fact]
    public async Task TestUrlStoreGrain_UrlEmptyOrNull_ThrowException()
    {
        // Arrange
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<SiloBuilder>();
        var cluster = builder.Build();
        await cluster.DeployAsync();

        // Act
        var urlStoreGrain01 = cluster.GrainFactory.GetGrain<IUrlStoreGrain>("a_token");
        var exception01 = await Assert.ThrowsAsync<ArgumentException>(() => urlStoreGrain01.SetUrl("a_token", string.Empty));
        var urlStoreGrain02 = cluster.GrainFactory.GetGrain<IUrlStoreGrain>("a_token");
        var exception02 = await Assert.ThrowsAsync<ArgumentException>(() => urlStoreGrain02.SetUrl("a_token", null!));
        await cluster.StopAllSilosAsync();

        // Assert
        Assert.Equal("The URL is null or empty (Parameter 'fullUrl') (Parameter 'fullUrl')", exception01.Message);
        Assert.Equal("The URL is null or empty (Parameter 'fullUrl') (Parameter 'fullUrl')", exception02.Message);
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