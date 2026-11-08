// One Container App per worker variant, in the resource group the base template created. Deployed separately from
// the base, so the images can be pushed to the registry in between.
import { resourceNames } from 'names.bicep'

@description('Azure region for the apps. Defaults to the resource group\'s region.')
param location string = resourceGroup().location

@description('The same name prefix the base template was deployed with.')
@minLength(2)
@maxLength(8)
param namePrefix string = 'stz'

@description('Image tag suffix. Each variant runs <login server>/scale-to-zero-worker:<variant>-<imageTag>.')
@minLength(1)
param imageTag string

@description('Minimum replicas for every app.')
@minValue(0)
param minReplicas int = 0

@description('KEDA polling interval in seconds. Leave unset to get the platform default.')
@minValue(1)
param pollingInterval int?

@description('Cooldown in seconds before scaling back to zero. Leave unset to get the platform default.')
@minValue(0)
param cooldownPeriod int?

var names = resourceNames(namePrefix, resourceGroup().id)

var variants = [
  'jit'
  'r2r'
  'aot'
  'jit-chiseled'
  'jit-identity'
]

// The one variant that signs in to Service Bus with a managed identity, and scales with one, instead of SAS.
var identityVariant = 'jit-identity'

var doneQueueName = 'done'

var tags = {
  experiment: 'scale-to-zero-aca'
}

// Only the settings that were passed end up in the request, so unset ones fall back to the platform defaults.
var scaleOverrides = union(
  pollingInterval == null ? {} : { pollingInterval: pollingInterval },
  cooldownPeriod == null ? {} : { cooldownPeriod: cooldownPeriod }
)

resource pullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' existing = {
  name: names.pullIdentity
}

resource workerIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' existing = {
  name: names.workerIdentity
}

resource scalerIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' existing = {
  name: names.scalerIdentity
}

resource registry 'Microsoft.ContainerRegistry/registries@2025-11-01' existing = {
  name: names.registry
}

resource namespace 'Microsoft.ServiceBus/namespaces@2026-01-01' existing = {
  name: names.serviceBusNamespace
}

resource environment 'Microsoft.App/managedEnvironments@2026-07-01' existing = {
  name: names.environment
}

// The worker uses one client for its own queue and for `done`. A queue-level connection string only works for its own
// queue, so this rule is on the namespace, with Listen and Send and no Manage.
resource workerRule 'Microsoft.ServiceBus/namespaces/authorizationRules@2026-01-01' = {
  parent: namespace
  name: 'worker'
  properties: {
    rights: [
      'Listen'
      'Send'
    ]
  }
}

// KEDA needs Manage to read the queue's message count, and documents only namespace connection strings.
resource scalerRule 'Microsoft.ServiceBus/namespaces/authorizationRules@2026-01-01' = {
  parent: namespace
  name: 'scaler'
  properties: {
    rights: [
      'Manage'
      'Listen'
      'Send'
    ]
  }
}

resource apps 'Microsoft.App/containerApps@2026-07-01' = [
  for variant in variants: {
    name: 'ca-${namePrefix}-${variant}'
    location: location
    tags: union(tags, { variant: variant })
    identity: {
      type: 'UserAssigned'
      userAssignedIdentities: union(
        { '${pullIdentity.id}': {} },
        variant == identityVariant ? { '${workerIdentity.id}': {}, '${scalerIdentity.id}': {} } : {}
      )
    }
    properties: {
      environmentId: environment.id
      configuration: union(
        {
          activeRevisionsMode: 'Single'
          registries: [
            {
              server: registry.properties.loginServer
              identity: pullIdentity.id
            }
          ]
        },
        variant == identityVariant
          ? {
              // The scale rule's identity is used by the platform only, so the worker's code can't get its tokens.
              identitySettings: [
                {
                  identity: workerIdentity.id
                  lifecycle: 'Main'
                }
                {
                  identity: scalerIdentity.id
                  lifecycle: 'None'
                }
              ]
            }
          : {
              secrets: [
                {
                  name: 'servicebus-connection'
                  value: workerRule.listKeys().primaryConnectionString
                }
                {
                  name: 'scaler-connection'
                  value: scalerRule.listKeys().primaryConnectionString
                }
              ]
            }
      )
      template: {
        containers: [
          {
            name: 'worker'
            image: '${registry.properties.loginServer}/scale-to-zero-worker:${variant}-${imageTag}'
            resources: {
              cpu: json('0.25')
              memory: '0.5Gi'
            }
            env: concat(
              variant == identityVariant
                ? [
                    {
                      // The public cloud's suffix. The namespace's serviceBusEndpoint is a URL with a port, which
                      // the client doesn't take.
                      name: 'SERVICEBUS_NAMESPACE'
                      value: '${namespace.name}.servicebus.windows.net'
                    }
                    {
                      name: 'AZURE_CLIENT_ID'
                      value: workerIdentity.properties.clientId
                    }
                  ]
                : [
                    {
                      name: 'SERVICEBUS_CONNECTION_STRING'
                      secretRef: 'servicebus-connection'
                    }
                  ],
              [
                {
                  name: 'WORKER_VARIANT'
                  value: variant
                }
                {
                  name: 'WORKER_QUEUE'
                  value: variant
                }
                {
                  name: 'WORKER_DONE_QUEUE'
                  value: doneQueueName
                }
              ]
            )
          }
        ]
        scale: union(
          {
            minReplicas: minReplicas
            maxReplicas: 1
            rules: [
              {
                name: 'servicebus-queue'
                // A rule takes identity instead of auth, with the resource ID of an identity the app has.
                custom: union(
                  {
                    type: 'azure-servicebus'
                    metadata: {
                      namespace: namespace.name
                      queueName: variant
                      messageCount: '1'
                    }
                  },
                  variant == identityVariant
                    ? { identity: scalerIdentity.id }
                    : {
                        auth: [
                          {
                            secretRef: 'scaler-connection'
                            triggerParameter: 'connection'
                          }
                        ]
                      }
                )
              }
            ]
          },
          scaleOverrides
        )
      }
    }
  }
]

output appNames string[] = [for (variant, i) in variants: apps[i].name]
output images string[] = [for (variant, i) in variants: apps[i].properties.template.containers[0].image]
