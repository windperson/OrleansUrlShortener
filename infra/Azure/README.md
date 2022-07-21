# Azure Infrastructure setup

Use [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/) & [Bicep](https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/) to create Azure deployment infrastructure and necessary services.

After you do login and chose the subscription properly in az cli, Deploy using the following commands:

```sh
az deployment sub create --name ithome_demo --location [azure_datacenter_region_you_choose] --template-file ./main.bicep  --parameters deploy_region=[azure_datacenter_region_you_choose] parameters.json
```
You can add an additional [`--confirm-with-what-if`](https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/deploy-what-if?tabs=azure-powershell%2CCLI#azure-cli) parameter to see what would happen if you deploy.

Delete cloud resources when we don't need them anymore:
```sh
az deployment sub delete --name ithome_demo
```

And also delete the resource group you created on `az deployment sub create...` command.

# Local Azurite storage emulator setup

To use [Azurite storage emulator](https://github.com/Azure/Azurite#npm), you need to install it using npm method.

Then [install `mkcert` according to your OS](https://github.com/FiloSottile/mkcert#installation), follow the [instructions to create local trust certificates to enable HTTPS on local Azurite storage emulator](https://github.com/Azure/Azurite#https-setup).

Run Azurite with following command:
```sh
azurite --location [data_store_path_you_choose]  --oauth basic --cert 127.0.0.1.pem --key 127.0.0.1-key.pem
```

