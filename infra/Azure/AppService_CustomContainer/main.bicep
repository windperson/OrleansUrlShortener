targetScope = 'subscription'

param deploy_region string
param resource_group string
param orleans_storage_account_prefix string
param frontend_webapp_prefix string = 'urlshortener-'
param web_app_sku string = 'S1'
param acr_prefix string
param frontend_webapp_docker_image string

resource urlShortenResourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: resource_group
  location: deploy_region
}

var webapp_name = '${frontend_webapp_prefix}${uniqueString(urlShortenResourceGroup.id)}'

var orleans_storage_account = take('${orleans_storage_account_prefix}${uniqueString(urlShortenResourceGroup.id)}', 24)

var container_registry_name = take('${acr_prefix}${uniqueString(urlShortenResourceGroup.id)}', 50)

module vnet 'vnet.bicep' = {
  name: 'vnetDeployment'
  scope: urlShortenResourceGroup
  params: {
    location: deploy_region
    webAppName: webapp_name
  }
}

module webapp 'webapp.bicep' = {
  name: 'webappDeployment'
  scope: urlShortenResourceGroup
  params: {
    location: deploy_region
    frontendWebAppName: webapp_name
    webappSku: web_app_sku
    storageAccountUrl: 'https://${orleans_storage_account}.table.${environment().suffixes.storage}'
    vnetSubnetId: vnet.outputs.vnetSubNetId
    acrUri: '${container_registry_name}.azurecr.io'
    containerImageNameTag: '${frontend_webapp_docker_image}:latest'
  }
}

module storage 'storage.bicep' = {
  name: 'storageDeployment'
  scope: urlShortenResourceGroup
  params: {
    storageAccountName: orleans_storage_account
    storageLocation: deploy_region
    allowedManagedIdentities: [ webapp.outputs.ProductionWebAppManagedIdentity ]
  }
}

module container_registy 'acr.bicep' = {
  name: 'acrDeployment'
  scope: urlShortenResourceGroup
  params: {
    location: deploy_region
    acrName: container_registry_name
    allowedManagedIdentities: [ webapp.outputs.ProductionWebAppManagedIdentity ]
    webhookUrls: [
      {
        name: 'staging0frontend'
        url: webapp.outputs.DeployWebhookUrl
        scope: '${frontend_webapp_docker_image}:latest'
        action: 'push'
      } ]
  }
}
