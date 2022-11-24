param storageAccountName string
param storageLocation string = resourceGroup().location
param allowedManagedIdentities array

resource storage 'Microsoft.Storage/storageAccounts@2022-05-01' = {
  name: storageAccountName
  location: storageLocation
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
}

@description('This is the built-in role. See https://docs.microsoft.com/en-us/azure/role-based-access-control/built-in-roles#contributor ')
resource contributorRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  scope: subscription()
  name: 'b24988ac-6180-42a0-ab88-20f7382dd24c'
}

@description('This is the built-in role. See https://docs.microsoft.com/en-us/azure/role-based-access-control/built-in-roles#storage-account-contributor ')
resource storageAccountContributorRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  scope: subscription()
  name: '17d1049b-9a84-46fb-8f53-869881c3d3ab'
}

@description('This is the built-in role. See https://docs.microsoft.com/en-us/azure/role-based-access-control/built-in-roles#storage-table-data-contributor ')
resource storageTableDataContributorRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  scope: subscription()
  name: '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
}

resource contributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for identity in allowedManagedIdentities: {
  name: guid(storageAccountName, 'contributor', identity)
  scope: storage
  properties: {
    principalType: 'ServicePrincipal'
    principalId: identity
    roleDefinitionId: contributorRole.id
  }
}]

resource storageAccountContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for identity in allowedManagedIdentities: {
  name: guid(storageAccountName, 'storageAccountContributor', identity)
  scope: storage
  properties: {
    principalType: 'ServicePrincipal'
    principalId: identity
    roleDefinitionId: storageAccountContributorRole.id
  }
}]

resource staogeTableDataContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for identity in allowedManagedIdentities: {
  name: guid(storageAccountName, 'storageTableDataContributor', identity)
  scope: storage
  properties: {
    principalType: 'ServicePrincipal'
    principalId: identity
    roleDefinitionId: storageTableDataContributorRole.id
  }
}]

var key = listKeys(storage.name, storage.apiVersion).keys[0].value

output storageName string = storage.name
output accountKey string = key
