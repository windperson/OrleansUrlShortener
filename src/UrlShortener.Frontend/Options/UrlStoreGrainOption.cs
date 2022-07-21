namespace UrlShortener.Frontend.Options;

public class UrlStoreGrainOption
{
    public string ServiceUrl { get; set; } = string.Empty;
    public string ManagedIdentityClientId { get; set; } = string.Empty;
    public string TableName { get; set; } = "urlgrains";
}