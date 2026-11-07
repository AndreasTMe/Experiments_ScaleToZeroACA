// UK South. The budget email comes from the STZ_BUDGET_EMAIL environment variable, which InfraRunner sets, so it isn't
// stored in the repo. A parameters file has to assign every parameter without a default, even when the value is also
// passed inline.
using 'main.bicep'

param location = 'uksouth'
param namePrefix = 'stz'
// On, because the system log stream doesn't keep a removed replica's events long enough to read them per sample.
param enableLogAnalytics = true
param budgetEmail = readEnvironmentVariable('STZ_BUDGET_EMAIL')
