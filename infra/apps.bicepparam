// The apps, into the resource group the base created. The image tag comes from the STZ_IMAGE_TAG environment variable,
// which InfraRunner sets. Polling interval and cooldown stay unset here, so the platform defaults apply.
using 'apps.bicep'

param namePrefix = 'stz'
param minReplicas = 0
param imageTag = readEnvironmentVariable('STZ_IMAGE_TAG')
