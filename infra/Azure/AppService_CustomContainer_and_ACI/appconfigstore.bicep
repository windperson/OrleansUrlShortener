@description('Specifies the name of the App Configuration store.')
param configStoreName string

@description('Specifies the Azure location where the app configuration store should be created.')
param location string = resourceGroup().location

@description('Specifies the names of the key-value resources. The name is a combination of key and label with $ as delimiter, ex: [\'myKey\', \'myKey$myLabel\']. label is optional.')
param keyValueNames array 

@description('Specifies the values of the key-value resources, assign with the same order as keyValueNames param, using [{Value: ooxx, ContentType: null or \'application/vnd.foobar+json;charset=utf-8\' }, ... ], The ContentType field is optional.')
param keyValueValues array 

@description('Specifies the managed identities that are allowed to read the App Configuration store.')
param allowedManagedIdentities array


@description('This is the built-in role. See https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles#app-configuration-data-reader ')
resource appConfigDataReaderRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  scope: subscription()
  name: '516239f1-63e1-4d78-a4de-a74fb236a071'
}

resource configStore 'Microsoft.AppConfiguration/configurationStores@2023-03-01' = {
  name: configStoreName
  location: location
  sku: {
    name: 'standard'
  }
}

resource configStoreKeyValue 'Microsoft.AppConfiguration/configurationStores/keyValues@2023-03-01' = [for (item, i) in keyValueNames: {
  parent: configStore
  name: item
  properties: {
    value: keyValueValues[i].Value
    contentType: contains(keyValueValues[i], 'ContentType') ?  keyValueValues[i].ContentType : null
  }
}]

resource DataReaderRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for identity in allowedManagedIdentities: {
  name: guid(configStoreName, 'appConfigDataReader', identity)
  scope: configStore
  properties: {
    principalType: 'ServicePrincipal'
    principalId: identity
    roleDefinitionId:appConfigDataReaderRole.id
  }
}]


output appConfigEndpoint string = configStore.properties.endpoint
