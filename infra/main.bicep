targetScope = 'resourceGroup'

@description('Location for all resources.')
param location string = resourceGroup().location

@description('Environment name prefix used across all resource names.')
param environmentName string

@description('Unique suffix to avoid global name conflicts.')
param resourceToken string = uniqueString(resourceGroup().id)

@description('Resource ID of the Azure Functions backend to link. Leave empty to skip backend linking.')
param functionAppResourceId string = ''

var abbrs = {
  staticWebApp: 'stapp'
}

var tags = {
  'azd-env-name': environmentName
}

// Azure Static Web App (Standard tier required for Bring Your Own Functions)
resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: '${abbrs.staticWebApp}${environmentName}${resourceToken}'
  location: location
  tags: union(tags, { 'azd-service-name': 'web' })
  sku: { name: 'Standard', tier: 'Standard' }
  properties: {}
}

// Optionally link an Azure Functions app as the backend
resource swaBackend 'Microsoft.Web/staticSites/linkedBackends@2023-12-01' = if (!empty(functionAppResourceId)) {
  parent: staticWebApp
  name: 'backend'
  properties: {
    backendResourceId: functionAppResourceId
    region: location
  }
}

output STATIC_WEB_APP_HOSTNAME string = staticWebApp.properties.defaultHostname
