param webAppName string
param location string = resourceGroup().location

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2022-05-01' = {
  name: '${webAppName}-vnet'
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.1.0.0/16'
      ]
    }
    subnets: [
      {
        name: 'webApp-inner'
        properties: {
          addressPrefix: '10.1.0.0/26'
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
    ]
  }
}

var subNetId = resourceId('Microsoft.Network/virtualNetworks/subnets', '${webAppName}-vnet', 'webApp-inner')

output vnetSubNetId string = subNetId
