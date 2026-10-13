#requires -Version 5.1
[CmdletBinding()]
param([switch]$SkipBuild)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$localSdk=Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$dotnet=if(Test-Path -LiteralPath $localSdk){$localSdk}else{(Get-Command dotnet -ErrorAction Stop).Source}
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
Push-Location $projectRoot
try {
    if(-not $SkipBuild){& (Join-Path $PSScriptRoot 'Build.ps1')}
    foreach($project in @('tests\Optimizer.Core.Tests','tests\Optimizer.Windows.Tests')) {
        & $dotnet run --project $project -c Release --no-build
        if($LASTEXITCODE -ne 0){throw "Safety checks failed: $project"}
    }
    & (Join-Path $PSScriptRoot 'Test-Cli.ps1')
    if($LASTEXITCODE -ne 0){throw 'CLI smoke checks failed.'}
    Write-Output 'Checks complete. Provider tests use simulated state; no Windows optimization settings were changed.'
} finally {Pop-Location}
