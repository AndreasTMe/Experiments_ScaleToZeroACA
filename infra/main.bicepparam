// UK South. The budget email is passed when deploying, for example:
//   az deployment sub create --location uksouth --parameters infra/main.bicepparam --parameters budgetEmail=<address>
using 'main.bicep'

param location = 'uksouth'
param namePrefix = 'stz'
param enableLogAnalytics = false
