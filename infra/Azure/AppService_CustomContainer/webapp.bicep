param location string = resourceGroup().location
param frontendWebAppName string
param storageAccountUrl string
param webappSku string = 'S1'
param vnetSubnetId string
param acrUri string
param containerImageNameTag string

// https://github.com/MicrosoftDocs/azure-docs/issues/36505#issuecomment-627899215
var linuxFxVersion = 'DOCKER|${acrUri}/${containerImageNameTag}'

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
    kind: 'app,linux,container'
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
            linuxFxVersion: linuxFxVersion
            alwaysOn: true
            http20Enabled: true
            vnetPrivatePortsCount: 2
            healthCheckPath: '/healthz'
            acrUseManagedIdentityCreds: true
            acrUserManagedIdentityID: managedIdentity.properties.clientId
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
                name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
                value: 'false'
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

// Get webapp CI/CD webhook url, 
// see those urls:
// https://github.com/Azure/bicep/discussions/3352#discussioncomment-976818
// https://stackoverflow.com/a/74344397/1075882
var publishingCredentials = resourceId('Microsoft.Web/sites/config', frontendWebAppName, 'publishingCredentials')
var deployWebhookUrl = '${list(publishingCredentials, '2022-03-01').properties.scmUri}/docker/hook'

output DeployWebhookUrl string = deployWebhookUrl
output ProductionWebAppManagedIdentity string = managedIdentity.properties.principalId
