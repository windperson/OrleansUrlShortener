param location string = resourceGroup().location
param frontendWebAppName string
param storageAccountUrl string
param webappSku string = 'S1'
param vnetSubnetId string

resource frontendAppServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
    name: '${frontendWebAppName}-plan'
    location: location
    sku: {
        name: webappSku
        capacity: 2
    }
    kind: 'linux'
    properties: {
        reserved: true
    }
}

resource frontend 'Microsoft.Web/sites@2022-03-01' = {
    name: frontendWebAppName
    location: location
    tags: {
        'hidden-related:${resourceGroup().id}/providers/Microsoft.Web/serverfarms/appServicePlan': 'Resource'
    }
    kind: 'app,linux'
    identity: {
        type: 'UserAssigned'
        userAssignedIdentities: {
            '${managedIdentity.id}': {}
        }
    }
    properties: {
        clientAffinityEnabled: false
        httpsOnly: true
        serverFarmId: frontendAppServicePlan.id
        virtualNetworkSubnetId: vnetSubnetId
        siteConfig: {
            linuxFxVersion: 'DOTNETCORE|6.0'
            alwaysOn: true
            http20Enabled: true
            vnetPrivatePortsCount: 2
            healthCheckPath: '/healthz'
        }
    }
}

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2022-01-31-preview' = {
    name: frontendWebAppName
    location: location
}

module appInsight 'operationalInsight.bicep' = {
    name: '${frontendWebAppName}AppInsight'
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
                name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
                value: '~3'
            }
            {
                name: 'XDT_MicrosoftApplicationInsights_Mode'
                value: 'default'
            }
            {
                name: 'UrlStoreGrain__ManagedIdentityClientId'
                value: managedIdentity.properties.clientId
            }
            {
                name: 'UrlStoreGrain__ServiceUrl'
                value: storageAccountUrl
            }
            {
                name: 'AzureTableCluster__ServiceUrl'
                value: storageAccountUrl
            }
            {
                name: 'AzureTableCluster__ManagedIdentityClientId'
                value: managedIdentity.properties.clientId
            }
            {
                // Extend app service restart container time limit so Orleans Silos could have enough time to sync
                // https://learn.microsoft.com/en-us/troubleshoot/azure/app-service/faqs-app-service-linux#my-custom-container-takes-a-long-time-to-start--and-the-platform-restarts-the-container-before-it-finishes-starting-up-
                name: 'WEBSITES_CONTAINER_START_TIME_LIMIT'
                value: '330'
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
    kind: 'app,linux'
    identity: {
        type: 'UserAssigned'
        userAssignedIdentities: {
            '${managedIdentity_staging.id}': {}
        }
    }
    properties: {
        clientAffinityEnabled: false
        httpsOnly: true
        serverFarmId: frontendAppServicePlan.id
        virtualNetworkSubnetId: vnetSubnetId
        siteConfig: {
            linuxFxVersion: 'DOTNETCORE|6.0'
            alwaysOn: true
            http20Enabled: true
            vnetPrivatePortsCount: 2
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
                    name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
                    value: '~3'
                }
                {
                    name: 'XDT_MicrosoftApplicationInsights_Mode'
                    value: 'default'
                }
                {
                    name: 'UrlStoreGrain__ManagedIdentityClientId'
                    value: managedIdentity_staging.properties.clientId
                }
                {
                    name: 'UrlStoreGrain__ServiceUrl'
                    value: storageAccountUrl
                }
                {
                    name: 'UrlStoreGrain__TableName'
                    value: 'UrlStoreGrainOnStaging'
                }
                {
                    name: 'AzureTableCluster__ServiceUrl'
                    value: storageAccountUrl
                }
                {
                    name: 'AzureTableCluster__ManagedIdentityClientId'
                    value: managedIdentity_staging.properties.clientId
                }
                {
                    name: 'AzureTableCluster__TableName'
                    value: 'AzureTableClusterOnStaging'
                }
                {
                    // Extend app service restart container time limit so Orleans Silos could have enough time to sync
                    // https://learn.microsoft.com/en-us/troubleshoot/azure/app-service/faqs-app-service-linux#my-custom-container-takes-a-long-time-to-start--and-the-platform-restarts-the-container-before-it-finishes-starting-up-
                    name: 'WEBSITES_CONTAINER_START_TIME_LIMIT'
                    value: '330'
                }
            ]
        }
    }
}

// Web App Staging Slot Config
resource webAppStagingSlotConfig 'Microsoft.Web/sites/config@2021-03-01' = {
    name: 'slotConfigNames'
    parent: frontend
    properties: {
        appSettingNames: [
            'UrlStoreGrain__ServiceUrl'
            'UrlStoreGrain__ManagedIdentityClientId'
            'UrlStoreGrain__TableName'
            'AzureTableCluster__ServiceUrl'
            'AzureTableCluster__ManagedIdentityClientId'
            'AzureTableCluster__TableName'
            'WEBSITES_CONTAINER_START_TIME_LIMIT'
        ]
    }
}

output webAppManagedIdentity array = [ managedIdentity.properties.principalId, managedIdentity_staging.properties.principalId ]
