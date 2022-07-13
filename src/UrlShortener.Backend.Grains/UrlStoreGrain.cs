using Orleans;
using Orleans.Runtime;

using UrlShortener.Backend.Interfaces;

namespace UrlShortener.Backend.Grains;

public class UrlStoreGrain : Grain, IUrlStoreGrain
{
    private readonly IPersistentState<KeyValuePair<string, string>> _cache;

    public UrlStoreGrain(
        [PersistentState(stateName: "actual-url", storageName: "url-store")] IPersistentState<KeyValuePair<string, string>> cache)
    {
        _cache = cache;
    }

    public async Task SetUrl(string shortenedRouteSegment, string fullUrl)
    {
        _cache.State = new KeyValuePair<string, string>(shortenedRouteSegment, fullUrl);
        await _cache.WriteStateAsync();
    }

    public Task<string> GetUrl()
    {
        return Task.FromResult(_cache.State.Value);
    }
}