param location string = resourceGroup().location
param workspaceName string = '${webAppName}-logs'
param workspaceSku string = 'PerGB2018'
param logRetentionInDays int = 30
param webAppName string
param webAppResourceId string

resource frontendAppInsight 'Microsoft.Insights/components@2020-02-02' = {
  name: '${webAppName}-insight'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logs.id
    Request_Source: 'rest'
  }
  tags: {
    'hidden-link:${webAppResourceId}': 'Resource'
    displayName: 'AppInsightsComponent'
  }
}

resource logs 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: workspaceName
  location: location
  properties: {
    retentionInDays: logRetentionInDays
    features: {
      searchVersion: 1
    }
    sku: {
      name: workspaceSku
    }
  }
}

output appInsightInstrumentKey string = frontendAppInsight.properties.InstrumentationKey
output appInsightConnectionString string = frontendAppInsight.properties.ConnectionString
