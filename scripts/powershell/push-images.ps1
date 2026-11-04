#Requires -Version 7.2
<#
.SYNOPSIS
Pushes the locally built worker images to the registry and prints the pushed digests.

.DESCRIPTION
Expects the images built locally as scale-to-zero-worker:<variant> (see docs/images.md). Each is pushed as
<login server>/scale-to-zero-worker:<variant>-<ImageTag>, which is the image infra/apps.bicep runs.

.EXAMPLE
./scripts/push-images.ps1 -SubscriptionId <id> -ImageTag <tag>

.EXAMPLE
./scripts/push-images.ps1 -SubscriptionId <id> -ImageTag <tag> -Variants aot -DryRun
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidatePattern('^[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}$')] [string] $SubscriptionId,

    [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z0-9_][A-Za-z0-9_.-]{0,100}$')] [string] $ImageTag,

    [ValidateSet('jit', 'r2r', 'aot', 'jit-chiseled')] [string[]] $Variants = @('jit', 'r2r', 'aot', 'jit-chiseled'),

    [ValidateLength(2, 8)] [string] $NamePrefix = 'stz',

    # Prints the az and docker commands and runs none of them.
    [switch] $DryRun
)

. (Join-Path $PSScriptRoot 'lib/Common.ps1')

$repository = 'scale-to-zero-worker'

Assert-Subscription -SubscriptionId $SubscriptionId -DryRun:$DryRun

# The registry name has a generated suffix, so it comes from the base deployment's outputs.
$outputs = @('deployment', 'sub', 'show', '--name', "$NamePrefix-base", '-o', 'tsv', '--query')
$registryName = Invoke-Tool az ($outputs + 'properties.outputs.registryName.value') -DryRun:$DryRun -DryRunResult '<registry>'
$loginServer = Invoke-Tool az ($outputs + 'properties.outputs.registryLoginServer.value') -DryRun:$DryRun -DryRunResult '<registry>.azurecr.io'
$registryName = "$registryName".Trim()
$loginServer = "$loginServer".Trim()
if (-not $registryName -or -not $loginServer) {
    throw "The deployment $NamePrefix-base has no registry outputs. Deploy -Phase base first."
}

# Check every local image first, so a missing one stops the run before anything is pushed.
foreach ($variant in $Variants) {
    Invoke-Tool docker @('image', 'inspect', '--format', '{{.Id}}', "${repository}:$variant") -DryRun:$DryRun | Out-Null
}

Invoke-Tool az @('acr', 'login', '--name', $registryName) -DryRun:$DryRun | Out-Host

$pushed = foreach ($variant in $Variants) {
    $remote = "$loginServer/${repository}:$variant-$ImageTag"
    Invoke-Tool docker @('tag', "${repository}:$variant", $remote) -DryRun:$DryRun | Out-Null
    Invoke-Tool docker @('push', $remote) -DryRun:$DryRun | Out-Host

    $digest = Invoke-Tool az @(
        'acr', 'repository', 'show',
        '--name', $registryName,
        '--image', "${repository}:$variant-$ImageTag",
        '--query', 'digest',
        '-o', 'tsv'
    ) -DryRun:$DryRun -DryRunResult '<digest>'

    [pscustomobject]@{ Variant = $variant; Image = $remote; Digest = "$digest".Trim() }
}

$pushed | Format-Table -AutoSize | Out-Host
