using './main.bicep'

param environmentName = readEnvironmentVariable('AZURE_ENV_NAME', 'moderntodo-frontend')
param location = readEnvironmentVariable('AZURE_LOCATION', 'eastus2')
// Optionally set functionAppResourceId to link the backend (from moderntodo-api deployment):
// param functionAppResourceId = '/subscriptions/.../resourceGroups/.../providers/Microsoft.Web/sites/func-...'
