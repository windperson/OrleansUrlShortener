targetScope = 'resourceGroup'

param deploy_region string = resourceGroup().location
param orleans_storage_account_prefix string
param frontend_webapp_prefix string = 'urlshortener-'
param web_app_sku string = 'S1'
param acr_prefix string
param frontend_webapp_docker_image string
param backend_aci_docker_image string

@description('Deploy the ACI resource, note: this must be done after the ARC is deployed and image has been pushed')
param deployAci bool = false

param backend_aci_containergroup_name string = 'urlshortener-backend'

var webapp_name = '${frontend_webapp_prefix}${uniqueString(resourceGroup().id)}'

var orleans_storage_account = take('${orleans_storage_account_prefix}${uniqueString(resourceGroup().id)}', 24)

var container_registry_name = take('${acr_prefix}${uniqueString(resourceGroup().id)}', 50)

module webAppManagedIdentity 'webapp_mgmtIdentity.bicep' = {
  name: 'webappManagedIdentityDeployment'
  params: {
    webAppName: webapp_name
    location: deploy_region
  }
}

var webAppManagedIdentity_resouceId = webAppManagedIdentity.outputs.resourceId
var webAppManagedIdentity_clientId = webAppManagedIdentity.outputs.clientId
var webAppManagedIdentity_principalId = webAppManagedIdentity.outputs.principalId

module aciManagedIdentity 'aci_mgmtIdentity.bicep' = {
  name: 'aciManagedIdentityDeployment'
  params: {
    containerGroupName: backend_aci_containergroup_name
    location: deploy_region
  }
}
var aciManagedIdentity_resouceId = aciManagedIdentity.outputs.resourceId
var aciManagedIdentity_clientId = aciManagedIdentity.outputs.clientId
var aciManagedIdentity_principalId = aciManagedIdentity.outputs.principalId

module vnet 'vnet.bicep' = {
  name: 'vnetDeployment'
  params: {
    location: deploy_region
    webAppName: webapp_name
  }
}

module webapp 'webapp.bicep' = {
  name: 'webappDeployment'
  params: {
    location: deploy_region
    frontendWebAppName: webapp_name
    webappSku: web_app_sku
    vnetSubnetId: vnet.outputs.vnetSubNetId
    acrUri: '${container_registry_name}.azurecr.io'
    containerImageNameTag: '${frontend_webapp_docker_image}:latest'
    userAssignedIdnetityClientId: webAppManagedIdentity_clientId
    userAssignedIdnetityResourceId: webAppManagedIdentity_resouceId
    appConfigStoreUrl: app_config_store.outputs.appConfigEndpoint
  }
}

var aciOpenPorts = [ { port: 8880, protocol: 'TCP' }
                     { port: 8881, protocol: 'TCP' } ]

module aci 'aci.bicep' = if (deployAci) {
  name: '${backend_aci_containergroup_name}Deployment'
  params: {
    containerGroupName: backend_aci_containergroup_name
    location: deploy_region
    acrUri: '${container_registry_name}.azurecr.io'
    image: '${backend_aci_docker_image}:latest'
    userAssignedIdnetityClientId: aciManagedIdentity_clientId
    userAssignedIdnetityResourceId: aciManagedIdentity_resouceId
    subnetId: vnet.outputs.aciSubNetId
    operationalInsightConnStr: webapp.outputs.AppInsightConnectionString
    appConfigStoreUrl: app_config_store.outputs.appConfigEndpoint
    openPorts: aciOpenPorts
  }
}

module storage 'storage.bicep' = {
  name: 'storageDeployment'
  params: {
    storageAccountName: orleans_storage_account
    storageLocation: deploy_region
    allowedManagedIdentities: [ webAppManagedIdentity_principalId, aciManagedIdentity_principalId ]
  }
}
var storageAccountUrl = 'https://${orleans_storage_account}.table.${environment().suffixes.storage}'

module container_registy 'acr.bicep' = {
  name: 'acrDeployment'
  params: {
    location: deploy_region
    acrName: container_registry_name
    allowedManagedIdentities: [ webAppManagedIdentity_principalId, aciManagedIdentity_principalId ]
    webhookUrls: [
      {
        name: 'urlshortener0frontend'
        url: webapp.outputs.DeployWebhookUrl
        scope: '${frontend_webapp_docker_image}:latest'
        action: 'push'
      } ]
  }
}

//because the for loop statement can't be used inside the module, we need to create a variable to store the open ports integer array
var openPorts = [for a in aciOpenPorts: (a.port)]
module app_config_store 'appconfigstore.bicep' = {
  name: 'appconfigstoreDeployment'
  params: {
    location: deploy_region
    configStoreName: 'urlshortener-config-${uniqueString(resourceGroup().id)}'
    allowedManagedIdentities: [ webAppManagedIdentity_principalId, aciManagedIdentity_principalId ]
    keyValueNames: [
      'OrleansCluster:ClusterId'
      'OrleansCluster:ServiceId'
      'AzureTableCluster:ServiceUrl'
      'UrlStoreGrain:ServiceUrl'
      'ACI:OpenPorts'
    ]
    keyValueValues: [
      { Value: 'urlshortener-cluster' }
      { Value: 'urlshortener-service' }
      { Value: storageAccountUrl }
      { Value: storageAccountUrl }
      { Value: string(openPorts), ContentType: 'application/json' }
    ]
  }
}
