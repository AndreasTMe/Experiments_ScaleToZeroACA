#Requires -Version 7.2
<#
.SYNOPSIS
Deploys the base infrastructure or the Container Apps.

.EXAMPLE
./scripts/deploy.ps1 -Phase base -SubscriptionId <id> -BudgetEmail <address>

.EXAMPLE
./scripts/deploy.ps1 -Phase apps -SubscriptionId <id> -ImageTag <tag> -PollingInterval 10 -CooldownPeriod 60

.EXAMPLE
./scripts/deploy.ps1 -Phase apps -SubscriptionId <id> -ImageTag <tag> -DryRun
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidateSet('base', 'apps')] [string] $Phase,

    [Parameter(Mandatory)] [ValidatePattern('^[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}$')] [string] $SubscriptionId,

    # Base phase only, and required there.
    [string] $BudgetEmail,

    # Apps phase only, and required there. Each variant runs scale-to-zero-worker:<variant>-<ImageTag>.
    [string] $ImageTag,

    # Apps phase only. Left out, the template's values apply: min replicas 0, polling and cooldown at the platform
    # defaults.
    [Nullable[int]] $MinReplicas,
    [Nullable[int]] $PollingInterval,
    [Nullable[int]] $CooldownPeriod,

    [ValidateLength(2, 8)] [string] $NamePrefix = 'stz',
    [string] $Location = 'uksouth',

    # Runs what-if instead of the deployment. It changes nothing.
    [switch] $Preview,

    # Prints the az commands and runs none of them.
    [switch] $DryRun
)

. (Join-Path $PSScriptRoot 'lib/Common.ps1')

$resourceGroup = Get-ResourceGroupName $NamePrefix $Location

if ($Phase -eq 'base') {
    if (-not $BudgetEmail) { throw '-BudgetEmail is required for -Phase base.' }
    if ($ImageTag -or $null -ne $MinReplicas -or $null -ne $PollingInterval -or $null -ne $CooldownPeriod) {
        throw '-ImageTag, -MinReplicas, -PollingInterval and -CooldownPeriod apply to -Phase apps only.'
    }
} else {
    if (-not $ImageTag) { throw '-ImageTag is required for -Phase apps.' }
    if ($BudgetEmail) { throw '-BudgetEmail applies to -Phase base only.' }
}

Assert-Subscription -SubscriptionId $SubscriptionId -DryRun:$DryRun

if ($Phase -eq 'base') {
    $arguments = @(
        'deployment', 'sub', ($Preview ? 'what-if' : 'create'),
        '--name', "$NamePrefix-base",
        '--location', $Location,
        '--parameters', (Join-Path $RepoRoot 'infra/main.bicepparam'),
        '--parameters', "namePrefix=$NamePrefix", "location=$Location", "budgetEmail=$BudgetEmail"
    )
    Invoke-Tool az $arguments -DryRun:$DryRun | Out-Host
    return
}

$exists = Invoke-Tool az @('group', 'exists', '--name', $resourceGroup) -DryRun:$DryRun -DryRunResult 'true'
if ($exists.Trim() -ne 'true') {
    throw "Resource group $resourceGroup doesn't exist. Deploy -Phase base first."
}

$overrides = @("namePrefix=$NamePrefix", "imageTag=$ImageTag")
if ($null -ne $MinReplicas) { $overrides += "minReplicas=$MinReplicas" }
if ($null -ne $PollingInterval) { $overrides += "pollingInterval=$PollingInterval" }
if ($null -ne $CooldownPeriod) { $overrides += "cooldownPeriod=$CooldownPeriod" }

$arguments = @(
    'deployment', 'group', ($Preview ? 'what-if' : 'create'),
    '--name', "$NamePrefix-apps",
    '--resource-group', $resourceGroup,
    '--parameters', (Join-Path $RepoRoot 'infra/apps.bicepparam'),
    '--parameters'
) + $overrides
Invoke-Tool az $arguments -DryRun:$DryRun | Out-Host
