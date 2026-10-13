#requires -Version 5.1
<# Compatibility entry point for Windows Workbench. No legacy tweaks are executed. #>
[CmdletBinding()]
param([switch]$Assess)
$ErrorActionPreference='Stop'
$package=Join-Path $PSScriptRoot 'artifacts\WindowsWorkbench'
if($Assess){
    $application=Join-Path $package 'WindowsWorkbench.Cli.exe'
    if(-not (Test-Path -LiteralPath $application)){throw 'Build the app with tools\Build.ps1 -Publish first. See README.md.'}
    & $application assess
    exit $LASTEXITCODE
}
$application=Join-Path $package 'WindowsWorkbench.exe'
if(-not (Test-Path -LiteralPath $application)){throw 'Build the app with tools\Build.ps1 -Publish first. See README.md.'}
# This explicit launcher opens the interactive desktop app; first launch is read-only.
Start-Process -FilePath $application -WorkingDirectory $package
