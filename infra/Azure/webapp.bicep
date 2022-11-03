param location string = resourceGroup().location
param frontendWebAppName string
param storageAccountUrl string
param webappSku string = 'S1'
param vnetSubnetId string

resource frontendAppServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
    name: 'urlshortener-frontend-app-plan'
    location: location
    sku: {
        name: webappSku
        capacity: 2
    }
}

resource frontend 'Microsoft.Web/sites@2022-03-01' = {
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
        clientAffinityEnabled: false
        serverFarmId: frontendAppServicePlan.id
        virtualNetworkSubnetId: vnetSubnetId
        siteConfig: {
            http20Enabled: true
            vnetPrivatePortsCount: 2
            netFrameworkVersion: 'v6.0'
            healthCheckPath: '/healthz'
        }
    }
}

resource webappMetaData 'Microsoft.Web/sites/config@2022-03-01' = {
    name: 'metadata'
    kind: 'string'
    parent: frontend
    properties: {
        // Trick to enable use .NET Core or .NET 5+ : https://www.coderperfect.com/how-to-configure-runtime-stack-to-azure-app-service-with-bicep-bicep-version-0-4/
        CURRENT_STACK: 'dotnetcore'
    }
}

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2022-01-31-preview' = {
    name: frontendWebAppName
    location: location
}

module appInsight 'operationalInsight.bicep' = {
    name: 'frontendAppInsight'
    params: {
        location: location
        webAppName: frontendWebAppName
        webAppResourceId: frontend.id
    }
}

resource frontendAppConfig 'Microsoft.Web/sites/config@2022-03-01' = {
    name: 'web'
    parent: frontend
    properties: {
        appSettings: [
            {
                name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
                value: appInsight.outputs.appInsightConnectionString
            }
            {
                name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
                value: appInsight.outputs.appInsightInstrumentKey

            }
            {
                name: 'UrlStoreGrain:ManagedIdentityClientId'
                value: managedIdentity.properties.clientId
            }
            {
                name: 'UrlStoreGrain:ServiceUrl'
                value: storageAccountUrl
            }
            {
                name: 'AzureTableCluster:ServiceUrl'
                value: storageAccountUrl
            }
            {
                name: 'AzureTableCluster:ManagedIdentityClientId'
                value: managedIdentity.properties.clientId
            }
        ]
    }
}

// Staging Deployment slot for frontend
resource managedIdentity_staging 'Microsoft.ManagedIdentity/userAssignedIdentities@2022-01-31-preview' = {
    name: '${frontendWebAppName}-staging'
    location: location
}

resource stagingSlot 'Microsoft.Web/sites/slots@2022-03-01' = {
    name: 'staging'
    location: location
    parent: frontend
    properties: {
        serverFarmId: frontendAppServicePlan.id
        virtualNetworkSubnetId: vnetSubnetId
        siteConfig: {
            http20Enabled: true
            vnetPrivatePortsCount: 2
            netFrameworkVersion: 'v6.0'
            healthCheckPath: '/healthz'
            appSettings: [
                {
                    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
                    value: appInsight.outputs.appInsightConnectionString
                }
                {
                    name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
                    value: appInsight.outputs.appInsightInstrumentKey
                }
                {
                    name: 'UrlStoreGrain:ManagedIdentityClientId'
                    value: managedIdentity_staging.properties.clientId
                }
                {
                    name: 'UrlStoreGrain:ServiceUrl'
                    value: storageAccountUrl
                }
                {
                    name: 'UrlStoreGrain:TableName'
                    value: 'UrlStoreGrainOnStaging'
                }
                {
                    name: 'AzureTableCluster:ServiceUrl'
                    value: storageAccountUrl
                }
                {
                    name: 'AzureTableCluster:ManagedIdentityClientId'
                    value: managedIdentity_staging.properties.clientId
                }
                {
                    name: 'AzureTableCluster:TableName'
                    value: 'AzureTableClusterOnStaging'
                }
            ]
        }
    }
    identity: {
        type: 'UserAssigned'
        userAssignedIdentities: {
            '${managedIdentity_staging.id}': {}
        }
    }
}

// Web App Staging Slot Config
resource webAppStagingSlotConfig 'Microsoft.Web/sites/config@2021-03-01' = {
    name: 'slotConfigNames'
    parent: frontend
    properties: {
        appSettingNames: [
            'UrlStoreGrain:ServiceUrl'
            'UrlStoreGrain:ManagedIdentityClientId'
            'UrlStoreGrain:TableName'
            'AzureTableCluster:ServiceUrl'
            'AzureTableCluster:ManagedIdentityClientId'
            'AzureTableCluster:TableName'
        ]
    }
}

output webAppManagedIdentity array = [ managedIdentity.properties.principalId , managedIdentity_staging.properties.principalId ]
