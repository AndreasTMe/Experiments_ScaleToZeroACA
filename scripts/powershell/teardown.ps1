#Requires -Version 7.2
<#
.SYNOPSIS
Deletes the resource group and the subscription budget, after a typed confirmation.

.EXAMPLE
./scripts/powershell/teardown.ps1 -SubscriptionId <id>

.EXAMPLE
./scripts/powershell/teardown.ps1 -SubscriptionId <id> -DryRun
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidatePattern('^[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}$')] [string] $SubscriptionId,

    [ValidateLength(2, 8)] [string] $NamePrefix = 'stz',
    [string] $Location = 'uksouth',

    # Prints the az commands and runs none of them. Skips the confirmation.
    [switch] $DryRun
)

. (Join-Path $PSScriptRoot 'lib/Common.ps1')

$resourceGroup = Get-ResourceGroupName $NamePrefix $Location
$budgetName = "budget-$NamePrefix"

Assert-Subscription -SubscriptionId $SubscriptionId -DryRun:$DryRun

if ($DryRun) {
    Write-Host "[dry run] would ask you to type '$resourceGroup' to confirm"
} else {
    Write-Host "This deletes resource group $resourceGroup, everything in it, and budget $budgetName on subscription $SubscriptionId."
    $typed = Read-Host "Type '$resourceGroup' to confirm"
    if ($typed -cne $resourceGroup) {
        Write-Host 'Not confirmed. Nothing was deleted.'
        exit 1
    }
}

$exists = Invoke-Tool az @('group', 'exists', '--name', $resourceGroup) -DryRun:$DryRun -DryRunResult 'true'
if ($exists.Trim() -eq 'true') {
    # Waits for the delete to finish, so the budget keeps watching until the resources are gone.
    Invoke-Tool az @('group', 'delete', '--name', $resourceGroup, '--yes') -DryRun:$DryRun | Out-Host
} else {
    Write-Host "Resource group $resourceGroup doesn't exist."
}

# A subscription budget: without --resource-group, the command works at subscription scope.
Invoke-Tool az @('consumption', 'budget', 'delete', '--budget-name', $budgetName) -DryRun:$DryRun | Out-Host
