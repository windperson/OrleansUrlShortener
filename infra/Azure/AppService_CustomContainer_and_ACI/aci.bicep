@description('Container group name')
param containerGroupName string = 'aci-containergroup'

@description('Container name')
param containerName string = 'aci-container'

@description('Azure Container Registry URI, e.g. myregistry.azurecr.io')
param acrUri string

@description('Container image:tag to deploy.')
param image string

@description('Port to open on the container.')
param port int = 80

@description('The number of CPU cores to allocate to the container. Must be an integer.')
param cpuCores int = 1

@description('The amount of memory to allocate to the container in gigabytes. Must be a positive decimal number. For example, 1.5')
param memoryInGb string = '1.5'

@description('The url of the Orlean\'s grain storage')
param storageAccountUrl string

param userAssignedIdnetityClientId string
param userAssignedIdnetityResourceId string

// param networkProfileName string = 'aci-vnet_networkProfile'
// param interfaceConfigName string = 'aci-vnet_interfaceConfig'
// param interfaceIpConfig string = 'aci-vnet_interfaceIpConfig'
param subnetId string

@description('The connection string to the Azure Operational Insights workspace.')
param operationalInsightConnectionString string

param location string = resourceGroup().location

var containerImage = '${acrUri}/${image}'

// resource networkProfile 'Microsoft.Network/networkProfiles@2020-11-01' = {
//   name: networkProfileName
//   location: location
//   properties: {
//     containerNetworkInterfaceConfigurations: [
//       {
//         name: interfaceConfigName
//         properties: {
//           ipConfigurations: [
//             {
//               name: interfaceIpConfig
//               properties: {
//                 subnet: {
//                   id: subnetId
//                 }
//               }
//             }
//           ]
//         }
//       }
//     ]
//   }
// }

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
          image: containerImage
          ports: [
            {
              port: port
              protocol: 'TCP'
            }
          ]
          resources: {
            requests: {
              cpu: cpuCores
              memoryInGB: json(memoryInGb)
            }
          }
          environmentVariables: [
            {
              name: 'ACI__PORT'
              value: '${port}'
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: operationalInsightConnectionString
            }
            {
              name: 'UrlStoreGrain__ManagedIdentityClientId'
              value: userAssignedIdnetityClientId
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
              value: userAssignedIdnetityClientId
            }
          ]
        }
      }
    ]
    osType: 'Linux'
    subnetIds: [ { id: subnetId, name: 'webapp-regional-vnet-subnet' } ]
    restartPolicy: 'OnFailure'
    // imageRegistryCredentials: [ {
    //     server: acrUri
    //     username: managedIdentity.properties.clientId
    //     password: ''
    //   } ]
  }
}
