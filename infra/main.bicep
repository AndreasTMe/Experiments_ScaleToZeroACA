// Base infrastructure: the resource group, a budget on the subscription, and everything the apps deploy into.
// The Container Apps themselves are in a separate template, so images can be pushed in between.
targetScope = 'subscription'

@description('Azure region for every resource.')
param location string = 'uksouth'

@description('Short prefix for resource names.')
@minLength(2)
@maxLength(8)
param namePrefix string = 'stz'

@description('Email address that receives the budget alerts. Passed on the command line, not stored in the repo.')
@minLength(3)
param budgetEmail string

@description('Monthly budget for the subscription, in the billing currency.')
@minValue(1)
param budgetAmount int = 10

@description('First day of the budget period. Must be the first of a month.')
param budgetStartDate string = '${utcNow('yyyy-MM')}-01'

@description('Creates a small Log Analytics workspace and sends the environment logs to it. Off by default.')
param enableLogAnalytics bool = false

var tags = {
  experiment: 'scale-to-zero-aca'
}

resource resourceGroup 'Microsoft.Resources/resourceGroups@2025-04-01' = {
  name: 'rg-${namePrefix}-${location}'
  location: location
  tags: tags
}

// A subscription budget, so it also catches charges that land outside the resource group.
resource budget 'Microsoft.Consumption/budgets@2026-06-01' = {
  name: 'budget-${namePrefix}'
  properties: {
    amount: budgetAmount
    category: 'Cost'
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: budgetStartDate
    }
    notifications: {
      actual50: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 50
        thresholdType: 'Actual'
        contactEmails: [
          budgetEmail
        ]
      }
      actual100: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 100
        thresholdType: 'Actual'
        contactEmails: [
          budgetEmail
        ]
      }
      forecast100: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 100
        thresholdType: 'Forecasted'
        contactEmails: [
          budgetEmail
        ]
      }
    }
  }
}

module base 'modules/base.bicep' = {
  name: 'base'
  scope: resourceGroup
  params: {
    location: location
    namePrefix: namePrefix
    enableLogAnalytics: enableLogAnalytics
    tags: tags
  }
}

output resourceGroupName string = resourceGroup.name
output budgetName string = budget.name
output registryName string = base.outputs.registryName
output registryLoginServer string = base.outputs.registryLoginServer
output serviceBusNamespaceName string = base.outputs.serviceBusNamespaceName
output queueNames string[] = base.outputs.queueNames
output environmentName string = base.outputs.environmentName
output environmentId string = base.outputs.environmentId
output pullIdentityId string = base.outputs.pullIdentityId
output logAnalyticsWorkspaceName string = base.outputs.logAnalyticsWorkspaceName
