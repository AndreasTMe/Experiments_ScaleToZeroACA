// UK South. The budget email comes from the STZ_BUDGET_EMAIL environment variable, which InfraRunner sets, so it isn't
// stored in the repo. A parameters file has to assign every parameter without a default, even when the value is also
// passed inline.
using 'main.bicep'

param location = 'uksouth'
param namePrefix = 'stz'
param enableLogAnalytics = false
param budgetEmail = readEnvironmentVariable('STZ_BUDGET_EMAIL')
