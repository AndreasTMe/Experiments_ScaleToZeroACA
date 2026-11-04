# Shared by the deploy, push and teardown scripts. Dot-source it: . (Join-Path $PSScriptRoot 'lib/Common.ps1')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:RepoRoot = Split-Path $PSScriptRoot -Parent | Split-Path -Parent

function Get-ResourceGroupName([string] $NamePrefix, [string] $Location) {
    # Matches infra/main.bicep.
    "rg-$NamePrefix-$Location"
}

function Format-CommandLine([string] $Tool, [string[]] $Arguments) {
    $quoted = foreach ($argument in $Arguments) {
        if ($argument -match '[\s"''&|<>;]' -or $argument -eq '') { "'$($argument -replace "'", "''")'" } else { $argument }
    }
    (@($Tool) + $quoted) -join ' '
}

# Runs a command and returns its output, or with -DryRun only prints it and returns $DryRunResult.
function Invoke-Tool {
    param(
        [Parameter(Mandatory)] [string] $Tool,
        [Parameter(Mandatory)] [string[]] $Arguments,
        [switch] $DryRun,
        [object] $DryRunResult = $null
    )

    $commandLine = Format-CommandLine $Tool $Arguments
    if ($DryRun) {
        Write-Host "[dry run] $commandLine"
        return $DryRunResult
    }

    Write-Host "> $commandLine"
    $output = & $Tool @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $commandLine"
    }
    $output
}

# Stops unless az is signed in to the expected subscription. Never switches subscription itself.
function Assert-Subscription {
    param(
        [Parameter(Mandatory)] [string] $SubscriptionId,
        [switch] $DryRun
    )

    $current = Invoke-Tool az @('account', 'show', '--query', 'id', '-o', 'tsv') -DryRun:$DryRun -DryRunResult $SubscriptionId
    if (-not $current) {
        throw 'az account show returned no subscription. Run az login first.'
    }
    if ($current.Trim() -ne $SubscriptionId) {
        throw "az is on subscription $($current.Trim()), not $SubscriptionId. Run: az account set --subscription $SubscriptionId"
    }
    if (-not $DryRun) {
        Write-Host "Subscription $SubscriptionId confirmed."
    }
}
