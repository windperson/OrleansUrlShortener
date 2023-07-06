param webAppName string
param location string = resourceGroup().location

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2022-11-01' = {
  name: '${webAppName}-vnet'
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        '172.17.0.0/16'
      ]
    }
    subnets: [
      {
        name: 'webApp-inner'
        properties: {
          addressPrefix: '172.17.0.0/24'
          delegations: [
            {
              name: 'webApp-delegation'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
        }
      }
      {
        name: 'aci-inner'
        properties: {
          addressPrefix: '172.17.1.0/24'
          delegations: [
            {
              name: 'aci-delegation'
              properties: {
                serviceName: 'Microsoft.ContainerInstance/containerGroups'
              }
            }
          ]
        }
      }
    ]
  }
}

var webAppSubNetId = resourceId('Microsoft.Network/virtualNetworks/subnets', '${webAppName}-vnet', 'webApp-inner')
var aciSubNetId = resourceId('Microsoft.Network/virtualNetworks/subnets', '${webAppName}-vnet', 'aci-inner')

output vnetSubNetId string = webAppSubNetId
output aciSubNetId string = aciSubNetId
