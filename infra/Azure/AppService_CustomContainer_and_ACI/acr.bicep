param acrName string
param acrSku string = 'Basic'
param location string = resourceGroup().location
param allowedManagedIdentities array
param webhookUrls array

resource az_container_registy 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' = {
  name: acrName
  location: location
  sku: {
    name: acrSku
  }
  properties: {
    publicNetworkAccess: 'Enabled'
  }
}

@description('This is the built-in role. See https://docs.microsoft.com/azure/role-based-access-control/built-in-roles#acrpull ')
resource acrPullRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  scope: subscription()
  name: '7f951dda-4ed3-4680-a7ca-43fe172d538d'
}

resource acrPullRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for identity in allowedManagedIdentities: {
  name: guid(acrName, 'acrPull', identity)
  scope: az_container_registy
  properties: {
    principalType: 'ServicePrincipal'
    principalId: identity
    roleDefinitionId: acrPullRole.id
  }
}]

// image push webhook
resource pushWebhook 'Microsoft.ContainerRegistry/registries/webhooks@2023-01-01-preview' = [for webhook in webhookUrls: {
  name: '${webhook.name}'
  parent: az_container_registy
  location: location
  properties: {
    serviceUri: '${webhook.url}'
    scope: webhook.scope
    actions: [
      webhook.action
    ]
    status: 'enabled'
  }
}]
