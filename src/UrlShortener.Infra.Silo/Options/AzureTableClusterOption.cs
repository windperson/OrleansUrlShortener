namespace UrlShortener.Infra.Silo.Options
{
    public class AzureTableClusterOption : AbstractAzureTableOption
    {
        public override string TableName { get; set; } = "OrleansCluster";
    }
}
