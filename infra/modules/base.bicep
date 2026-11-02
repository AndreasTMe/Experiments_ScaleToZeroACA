// Everything in the resource group that the apps need: registry, Service Bus, environment and the pull identity.
param location string
param namePrefix string
param enableLogAnalytics bool
param tags object

// The four variant queues plus `done`, where every worker answers.
var queueNames = [
  'jit'
  'r2r'
  'aot'
  'jit-chiseled'
  'done'
]

var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'
var suffix = uniqueString(resourceGroup().id)

resource pullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: 'id-${namePrefix}-pull'
  location: location
  tags: tags
}

resource registry 'Microsoft.ContainerRegistry/registries@2025-11-01' = {
  name: 'acr${namePrefix}${suffix}'
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
    // Set explicitly: in the ABAC mode AcrPull isn't honoured, and ABAC is planned to become the default.
    roleAssignmentMode: 'LegacyRegistryPermissions'
  }
}

resource acrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, pullIdentity.id, acrPullRoleId)
  scope: registry
  properties: {
    principalId: pullIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
  }
}

resource namespace 'Microsoft.ServiceBus/namespaces@2026-01-01' = {
  name: 'sb-${namePrefix}-${suffix}'
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    // The apps authenticate with SAS connection strings.
    disableLocalAuth: false
  }
}

// The same values as local/emulator-config.json. Basic has no duplicate detection, sessions or auto-forwarding, so
// the duplicate detection window and the forwarding targets are left out; the emulator leaves them unused too.
resource queues 'Microsoft.ServiceBus/namespaces/queues@2026-01-01' = [
  for queueName in queueNames: {
    parent: namespace
    name: queueName
    properties: {
      maxDeliveryCount: 10
      lockDuration: 'PT1M'
      defaultMessageTimeToLive: 'PT1H'
      deadLetteringOnMessageExpiration: false
      requiresSession: false
      requiresDuplicateDetection: false
    }
  }
]

resource workspace 'Microsoft.OperationalInsights/workspaces@2026-03-01' = if (enableLogAnalytics) {
  name: 'log-${namePrefix}'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    workspaceCapping: {
      dailyQuotaGb: 1
    }
  }
}

resource environment 'Microsoft.App/managedEnvironments@2026-07-01' = {
  name: 'cae-${namePrefix}'
  location: location
  tags: tags
  properties: {
    environmentMode: 'ConsumptionOnly'
    appLogsConfiguration: enableLogAnalytics
      ? {
          destination: 'log-analytics'
          logAnalyticsConfiguration: {
            customerId: workspace!.properties.customerId
            sharedKey: workspace!.listKeys().primarySharedKey
          }
        }
      : {
          destination: 'none'
        }
  }
}

output registryName string = registry.name
output registryLoginServer string = registry.properties.loginServer
output serviceBusNamespaceName string = namespace.name
output queueNames string[] = [for (queueName, i) in queueNames: queues[i].name]
output environmentName string = environment.name
output environmentId string = environment.id
output pullIdentityId string = pullIdentity.id
output logAnalyticsWorkspaceName string = enableLogAnalytics ? workspace!.name : ''
