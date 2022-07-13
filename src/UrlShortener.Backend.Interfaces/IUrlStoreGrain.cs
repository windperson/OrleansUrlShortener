using Orleans;

namespace UrlShortener.Backend.Interfaces;

public interface IUrlStoreGrain : IGrainWithStringKey
{
    Task SetUrl(string shortenedRouteSegment, string fullUrl);
    Task<string> GetUrl();
}