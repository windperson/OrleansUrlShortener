namespace UrlShortener.Infra.Silo.Options;

public class UrlStoreGrainOption : AbstractAzureTableOption
{
    public override string TableName { get; set; } = "urlgrains";
}