# Azure Infrastructure setup

Use [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/) & [Bicep](https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/) to create Azure deployment infrastructure and necessary services.

## Create demo Azure resources

### Provision Azure infra services using Azure CLI
After you do login and chose the subscription properly in az cli, create a resource group using the following command:

```sh
az group create --name orleans_net6_demo --location [azure_datacenter_region_you_choose]
```

Deploy using the following command at first time:

```sh
az deployment group create --name orleans_net6webapp_linux_demo03 --resource-group orleans_net6_demo --template-file ./main.bicep --parameters ./parameters.json
```

You can add an additional [--confirm-with-what-if](https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/deploy-what-if?tabs=azure-powershell%2CCLI#azure-cli) parameter to see what would happen if you deploy.

Then push the container image to Azure Container Registry (ACR) using the following commands on project root folder, Replace the ***<provisioned_ACR_service_name>*** with the name of the provisioned ACR service on both commands, you can use `az acr list -o table -g orleans_net6_demo` to get the name of the provisioned ACR service.

* Frontend container image:
   
```sh
az acr build --no-logs -t urlshortener/frontend:latest -r <provisioned_ACR_service_name> -f ./src/UrlShortener.Frontend/Dockerfile .
```

* Backend container image:
   
```sh
az acr build --no-logs -t urlshortener/backend_silohost:latest -r <provisioned_ACR_service_name> -f ./src/UrlShortener.Backend.SiloHost/Dockerfile .
```

Then run the bicep deployment again with an additional `deployAci=true` parameters:

```sh
az deployment group create --name orleans_net6webapp_linux_demo03 --resource-group orleans_net6_demo --template-file ./main.bicep --parameters deployAci=true ./parameters.json
```


## Cleanup Azure resources

Delete cloud resources when we don't need them anymore:
```sh
az deployment group delete --name orleans_net6webapp_linux_demo03 --resource-group orleans_net6_demo
```

And also be sure to delete the resource group `orleans_net6_demo` either on Azure Portal or via cli command:
```sh
az group delete --name orleans_net6_demo
```

# Local Azurite storage emulator setup

If you want to run project locally, Use [Azurite storage emulator](https://github.com/Azure/Azurite#npm), you need to install it using npm installation.

Then [install "mkcert" according to your OS](https://github.com/FiloSottile/mkcert#installation), follow the [instructions to create local trust certificates to enable HTTPS on local Azurite storage emulator](https://github.com/Azure/Azurite#https-setup).  
Then Run Azurite with following command:
```sh
azurite --location [data_store_path_you_choose]  --oauth basic --cert 127.0.0.1.pem --key 127.0.0.1-key.pem
```
