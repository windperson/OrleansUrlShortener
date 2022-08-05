param location string = resourceGroup().location
param frontendWebAppName string
param storageAccountUrl string
param webappSku string = 'S1'

var urlStoreGrainAppsettings = {
  name: 'UrlStoreGrain:ServiceUrl'
  value: storageAccountUrl
}

resource frontendAppServicePlan 'Microsoft.Web/serverfarms@2020-12-01' = {
  name: 'urlshortener-frontend-app-plan'
  location: location
  sku: {
    name: webappSku
    capacity: 1
  }
}

resource frontend 'Microsoft.Web/sites@2018-11-01' = {
  name: frontendWebAppName
  location: location
  tags: {
    'hidden-related:${resourceGroup().id}/providers/Microsoft.Web/serverfarms/appServicePlan': 'Resource'
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: frontendAppServicePlan.id

    siteConfig: {
      netFrameworkVersion: 'v6.0'
    }
  }
}

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2022-01-31-preview' = {
  name: frontendWebAppName
  location: location
}

resource frontendAppSetting 'Microsoft.Web/sites/config@2022-03-01' = {
  name: 'web'
  parent: frontend
  properties: {
    appSettings: [
      {
        name: 'APPINSIGHTS_CONNECTION_STRING'
        value: frontendAppInsight.properties.ConnectionString
      }
      {
        name: 'UrlStoreGrain:ManagedIdentityClientId'
        value: managedIdentity.properties.clientId
      }
      (!empty(storageAccountUrl)) ? urlStoreGrainAppsettings : {}
    ]
  }
}

resource webappMetaData 'Microsoft.Web/sites/config@2021-03-01' = {
  name: 'metadata'
  kind: 'string'
  parent: frontend
  properties: {
    // Trick to enable use .NET Core or .NET 5+ : https://www.coderperfect.com/how-to-configure-runtime-stack-to-azure-app-service-with-bicep-bicep-version-0-4/
    CURRENT_STACK: 'dotnetcore'
  }
}

resource frontendAppInsight 'Microsoft.Insights/components@2020-02-02-preview' = {
  name: 'urlshortener-frontend-app-insight'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }
}

output webAppManagedIdentity string = managedIdentity.properties.principalId
