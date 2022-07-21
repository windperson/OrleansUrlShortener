targetScope = 'subscription'

param deploy_region string
param resource_group string
param orleans_storage_account_prefix string
param orleans_storage_account string = take('${orleans_storage_account_prefix}${uniqueString(utcNow())}', 24)

resource urlShortenResourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: resource_group
  location: deploy_region
}

module webapp 'webapp.bicep' = {
  name: 'webappDeployment'
  scope: urlShortenResourceGroup
  params: {
    location: deploy_region
    storageAccountUrl: 'https://${orleans_storage_account}.table.${environment().suffixes.storage}'
  }
}

module storage 'storage.bicep' = {
  name: 'storageDeployment'
  scope: urlShortenResourceGroup
  params: {
    storageAccountName: orleans_storage_account
    storageLocation: deploy_region
    allowedManagedIdentity: webapp.outputs.webAppManagedIdentity
  }
}
