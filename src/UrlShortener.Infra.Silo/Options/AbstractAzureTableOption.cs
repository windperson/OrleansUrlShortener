namespace UrlShortener.Infra.Silo.Options;

public abstract class AbstractAzureTableOption
{
    public string ServiceUrl { get; set; } = string.Empty;
    public string ManagedIdentityClientId { get; set; } = string.Empty;
    public abstract string TableName { get; set; }
}