// The apps, into the resource group the base created. The image tag is passed when deploying, for example:
//   az deployment group create --resource-group rg-stz-uksouth --parameters infra/apps.bicepparam --parameters imageTag=<tag>
// Polling interval and cooldown stay unset here, so the platform defaults apply.
using 'apps.bicep'

param namePrefix = 'stz'
param minReplicas = 0
