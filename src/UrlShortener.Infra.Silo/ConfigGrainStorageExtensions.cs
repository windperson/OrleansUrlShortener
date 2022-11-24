using Azure.Identity;

using Orleans.Hosting;

using UrlShortener.Infra.Silo.Options;

namespace UrlShortener.Infra.Silo;

public static class ConfigGrainStorageExtensions
{
    public static ISiloBuilder SetGrainStorageUsingAzureTable(this ISiloBuilder siloBuilder, string providerName,
        UrlStoreGrainOption urlStoreGrainOption)
    {
        if (urlStoreGrainOption.ServiceUrl.Contains("http://") )
        {
            //Insecure Azure Table Storage clustering
            siloBuilder.ConfigGrainStorageUsingAzureTable(providerName, urlStoreGrainOption.TableName,
                urlStoreGrainOption.ServiceUrl);
        }
        else
        {
            siloBuilder.AzureTableGrainStorageUseDefaultAzureCredential(providerName, urlStoreGrainOption);
        }

        return siloBuilder;
    }

    private static ISiloBuilder AzureTableGrainStorageUseDefaultAzureCredential(this ISiloBuilder siloBuilder,
        string providerName, UrlStoreGrainOption urlStoreGrainOption)
    {
        siloBuilder.AddAzureTableGrainStorage(
            name: providerName,
            configureOptions: options =>
            {
                options.TableName =
                    urlStoreGrainOption.TableName; // if not set, default will be "OrleansGrainState" table name
                options.UseJson = true;

                options.ConfigureTableServiceClient(new Uri(urlStoreGrainOption.ServiceUrl),
                    new DefaultAzureCredential(new DefaultAzureCredentialOptions
                    {
                        ManagedIdentityClientId = urlStoreGrainOption.ManagedIdentityClientId
                    }));
            });
        return siloBuilder;
    }

    private static ISiloBuilder ConfigGrainStorageUsingAzureTable(this ISiloBuilder siloBuilder,
        string providerName, string tableName, string connectionString)
    {
        siloBuilder.AddAzureTableGrainStorage(
            name: providerName,
            configureOptions: options =>
            {
                options.TableName = tableName; // if not set, default will be "OrleansGrainState" table name
                options.UseJson = true;

                // use this configuration if you only want to use local http only Azurite Azure Table Storage emulator
                // options.ConfigureTableServiceClient("DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;");
                options.ConfigureTableServiceClient(connectionString);
            });
        return siloBuilder;
    }
}