using Orleans;
using Orleans.Runtime;

using UrlShortener.Backend.Interfaces;

namespace UrlShortener.Backend.Grains;

public class UrlStoreGrain : Grain, IUrlStoreGrain
{
    private readonly IPersistentState<KeyValuePair<string, string>> _cache;

    public UrlStoreGrain(
        [PersistentState(stateName: "actual-url", storageName: "url-store")]
        IPersistentState<KeyValuePair<string, string>> cache)
    {
        _cache = cache;
    }

    public async Task SetUrl(string shortenedRouteSegment, string fullUrl)
    {
        const string httpPrefix = "http://";
        const string httpsPrefix = "https://";
        const string sanitizedPrefixPattern01 = "http/";
        const string sanitizedPrefixPattern02 = "http:/";
        const string sanitizedPrefixPattern03 = "https/";
        const string sanitizedPrefixPattern04 = "https:/";
        fullUrl = fullUrl switch
        {
            not null when fullUrl.TrimStart().StartsWith(httpPrefix) ||
                          fullUrl.TrimStart().StartsWith(httpsPrefix) => fullUrl.TrimEnd(),
            // Handle situations where the origin fullURL string is sanitized by Web Server
            // (e.g. IIS, Apache will make it "http/" instead of "http://")
            not null when fullUrl.TrimStart().StartsWith(sanitizedPrefixPattern01) =>
                httpPrefix + fullUrl.TrimStart()[sanitizedPrefixPattern01.Length..].TrimEnd(),
            not null when fullUrl.TrimStart().StartsWith(sanitizedPrefixPattern02) =>
                httpPrefix + fullUrl.TrimStart()[sanitizedPrefixPattern02.Length..].TrimEnd(),
            not null when fullUrl.TrimStart().StartsWith(sanitizedPrefixPattern03) =>
                httpsPrefix + fullUrl.TrimStart()[sanitizedPrefixPattern03.Length..].TrimEnd(),
            not null when fullUrl.TrimStart().StartsWith(sanitizedPrefixPattern04) =>
                httpsPrefix + fullUrl.TrimStart()[sanitizedPrefixPattern04.Length..].TrimEnd(),
            //Prefix with "http://" if none of the http or https scheme is present
            not null when !string.IsNullOrEmpty(fullUrl.Trim()) => httpPrefix + fullUrl.Trim(),
            _ => throw new ArgumentException("The URL is null or empty", nameof(fullUrl))
        };

        _cache.State = new KeyValuePair<string, string>(shortenedRouteSegment, fullUrl);
        await _cache.WriteStateAsync();
    }

    public Task<string> GetUrl()
    {
        var originalUrl = _cache.State.Value;
        if (string.IsNullOrEmpty(originalUrl))
        {
            throw new KeyNotFoundException("Url key not exist: " + this.GrainReference.GetPrimaryKeyString());
        }

        return Task.FromResult(_cache.State.Value);
    }
}