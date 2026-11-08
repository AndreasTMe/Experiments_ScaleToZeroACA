// Everything in the resource group that the apps need: registry, Service Bus, environment and the identities.

import { resourceNames } from '../names.bicep'

param location string
param namePrefix string
param enableLogAnalytics bool
param tags object

// The five variant queues plus `done`, where every worker answers.
var queueNames = [
  'jit'
  'r2r'
  'aot'
  'jit-chiseled'
  'jit-identity'
  'done'
]

// The one variant that signs in with a managed identity instead of a connection string.
var identityQueueName = 'jit-identity'
var doneQueueName     = 'done'

var acrPullRoleId             = '7f951dda-4ed3-4680-a7ca-43fe172d538d'
var serviceBusDataOwnerId     = '090c5cfd-751d-490a-894a-3ce6f1109419'
var serviceBusDataReceiverId  = '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0'
var serviceBusDataSenderId    = '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39'
var names = resourceNames(namePrefix, resourceGroup().id)

resource pullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: names.pullIdentity
  location: location
  tags: tags
}

resource registry 'Microsoft.ContainerRegistry/registries@2025-11-01' = {
  name: names.registry
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
  name: names.serviceBusNamespace
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    // All apps but jit-identity authenticate with SAS connection strings, and so do the runner and every scale rule
    // but jit-identity's.
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

// The jit-identity worker's own identity, and a second one for its scale rule, which mirror the worker and scaler SAS
// rules in apps.bicep. They're here rather than with the apps so the role assignments have taken effect by the time
// the apps start.
resource workerIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: names.workerIdentity
  location: location
  tags: tags
}

resource scalerIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: names.scalerIdentity
  location: location
  tags: tags
}

// The queues are referred to by their place in the loop. An `existing` reference to a queue the same template
// deploys makes the resource appear twice in the compiled template, which ARM rejects.
var identityQueueIndex = indexOf(queueNames, identityQueueName)
var doneQueueIndex     = indexOf(queueNames, doneQueueName)

// Each role is on the one queue it's needed for: the worker receives from its own queue and answers on `done`.
resource workerReceiver 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(queues[identityQueueIndex].id, workerIdentity.id, serviceBusDataReceiverId)
  scope: queues[identityQueueIndex]
  properties: {
    principalId: workerIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', serviceBusDataReceiverId)
  }
}

resource workerSender 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(queues[doneQueueIndex].id, workerIdentity.id, serviceBusDataSenderId)
  scope: queues[doneQueueIndex]
  properties: {
    principalId: workerIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', serviceBusDataSenderId)
  }
}

// Reading the message count is a management read, which the receiver and sender roles don't grant. KEDA names no role;
// Microsoft's KEDA workload identity guide for Service Bus assigns Data Owner, the RBAC match for SAS Manage.
resource scalerOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(queues[identityQueueIndex].id, scalerIdentity.id, serviceBusDataOwnerId)
  scope: queues[identityQueueIndex]
  properties: {
    principalId: scalerIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', serviceBusDataOwnerId)
  }
}

resource workspace 'Microsoft.OperationalInsights/workspaces@2026-03-01' = if (enableLogAnalytics) {
  name: names.workspace
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    // The cap is an int, so 1 GB a day is the smallest. The runs ingest a few MB.
    workspaceCapping: {
      dailyQuotaGb: 1
    }
  }
}

resource environment 'Microsoft.App/managedEnvironments@2026-07-01' = {
  name: names.environment
  location: location
  tags: tags
  // Without Log Analytics, appLogsConfiguration is left out. The reference lists 'none' as a destination, but the
  // 2026-07-01 API rejects it and asks for no destination at all.
  properties: union(
    { environmentMode: 'ConsumptionOnly' },
    enableLogAnalytics
      ? {
          appLogsConfiguration: {
            destination: 'log-analytics'
            logAnalyticsConfiguration: {
              customerId: workspace!.properties.customerId
              sharedKey: workspace!.listKeys().primarySharedKey
            }
          }
        }
      : {}
  )
}

output registryName string = registry.name
output registryLoginServer string = registry.properties.loginServer
output serviceBusNamespaceName string = namespace.name
output queueNames string[] = [for (queueName, i) in queueNames: queues[i].name]
output environmentName string = environment.name
output environmentId string = environment.id
output pullIdentityId string = pullIdentity.id
output workerIdentityId string = workerIdentity.id
output scalerIdentityId string = scalerIdentity.id
output logAnalyticsWorkspaceName string = enableLogAnalytics ? workspace!.name : ''
