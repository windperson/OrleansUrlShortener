@description('Container group name')
param containerGroupName string = 'aci-containergroup'

@description('Container name')
param containerName string = 'aci-container'

@description('Azure Container Registry URI, e.g. myregistry.azurecr.io')
param acrUri string

@description('Container image:tag to deploy.')
param image string

@description('The number of CPU cores to allocate to the container. Must be an integer.')
param cpuCores int = 1

@description('The amount of memory to allocate to the container in gigabytes. Must be a positive decimal number. For example, 1.5')
param memoryInGb string = '1.5'

@description('The exposed ports that ACI will listen on, Must be the [{port: 1234, protocol: \'TCP\'}] format.')
param openPorts array

@description('The commands to Start container akin to docker run command. Must be an array of strings.')
param containerCommand array = [ 'dotnet', 'UrlShortener.Backend.SiloHost.dll' ]

param userAssignedIdnetityClientId string
param userAssignedIdnetityResourceId string

@description('The vnet subnet that connect to the container group.')
param subnetId string

@description('The connection string to the Azure Operational Insights workspace.')
param operationalInsightConnStr string

@description('The connection string to the Azure App Configuration store.')
param appConfigStoreUrl string

param location string = resourceGroup().location

var deployContainerImage = '${acrUri}/${image}'

resource containerGroup 'Microsoft.ContainerInstance/containerGroups@2023-05-01' = {
  name: containerGroupName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdnetityResourceId}': {}
    }
  }
  properties: {
    containers: [
      {
        name: containerName
        properties: {
          image: deployContainerImage
          command: containerCommand
          ports: openPorts
          resources: {
            requests: {
              cpu: cpuCores
              memoryInGB: json(memoryInGb)
            }
          }
          environmentVariables: [
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: operationalInsightConnStr
            }
            {
              name: 'ConnectionStrings__AppConfigStore'
              value: appConfigStoreUrl
            }
            {
              name: 'UrlStoreGrain__ManagedIdentityClientId'
              value: userAssignedIdnetityClientId
            }
            {
              name: 'AzureTableCluster__ManagedIdentityClientId'
              value: userAssignedIdnetityClientId
            }
            {
              name: 'AppConfigStore__ManagedIdentityClientId'
              value: userAssignedIdnetityClientId
            }
          ]
        }
      }
    ]
    osType: 'Linux'
    subnetIds: [ { id: subnetId, name: 'webapp-regional-vnet-subnet' } ]
    restartPolicy: 'OnFailure'
    imageRegistryCredentials: [ {
        server: acrUri
        identity: userAssignedIdnetityResourceId
      } ]
  }
}
