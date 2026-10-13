#requires -Version 5.1
<#
Read-only regression probes for the audited legacy source. This deliberately does
NOT invoke or dot-source Optimize-Win11.ps1. Only named function definitions are
loaded from its syntax tree; Windows operations in each probe are replaced by
in-memory mocks. Confirmed=true means the documented legacy defect was reproduced,
not that the optimizer passed a safety test. No third-party dependencies.
#>
[CmdletBinding()]
param([string]$SourcePath)

$ErrorActionPreference = 'Stop'
if (-not $SourcePath) { $SourcePath = Join-Path $PSScriptRoot '..\..\legacy\Optimize-Win11.original.ps1' }
$resolvedSource = (Resolve-Path -LiteralPath $SourcePath).Path
$sourceHash = (Get-FileHash -LiteralPath $resolvedSource -Algorithm SHA256).Hash
if ($sourceHash -ne '80200A67C6253C2385F4C24E5CC5A27B54458D2445F4C58734F79CF1A7FE36D3') {
    throw 'Source differs from the audited revision. Re-review extracted functions and mocks before running these probes.'
}
$tokens = $null
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($resolvedSource, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -ne 0) { throw ($parseErrors | Out-String) }
$definitions = @{}
foreach ($node in $ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $true)) {
    $definitions[$node.Name] = $node.Extent.Text
}
$results = New-Object System.Collections.Generic.List[object]
function Test-LegacyBehavior {
    param([string]$Id, [string]$Description, [scriptblock]$Probe)
    try {
        $observed = & $Probe
        $results.Add([pscustomobject]@{ Id=$Id; Description=$Description; Confirmed=[bool]$observed; Error=$null })
    } catch {
        $results.Add([pscustomobject]@{ Id=$Id; Description=$Description; Confirmed=$false; Error=$_.Exception.Message })
    }
}

Test-LegacyBehavior 'DNS-KEEP' 'Full plans a DNS change despite explicit Keep.' {
    . ([scriptblock]::Create($definitions['Apply-NetworkTweaks']))
    $Preset = 'Full'; $DnsProvider = 'Keep'
    $script:probeNames = New-Object System.Collections.Generic.List[string]
    function Get-ActiveNetworkAdapters { [pscustomobject]@{Name='Mock NIC'; InterfaceGuid='mock-guid'} }
    function Get-DnsServersForProvider { param($Provider) @('192.0.2.1') }
    function Get-NetAdapterAdvancedProperty { param($Name,$DisplayName,$ErrorAction) }
    function Invoke-Action { param($Category,$Name,$ScriptBlock,$Detail) $script:probeNames.Add($Name) }
    function Add-Action { param($Category,$Name,$Status,$Detail) }
    Apply-NetworkTweaks -Detection ([pscustomobject]@{})
    $script:probeNames.Contains('Set Cloudflare DNS on Mock NIC')
}

Test-LegacyBehavior 'HEALTH-OPTOUT' 'Full schedules both repairs despite health checks being false.' {
    . ([scriptblock]::Create($definitions['Invoke-HealthChecks']))
    $Preset = 'Full'; $RunHealthChecks = $false
    $script:probeNames = New-Object System.Collections.Generic.List[string]
    function Invoke-Action { param($Category,$Name,$ScriptBlock,$Detail) $script:probeNames.Add($Name) }
    function Add-Action { param($Category,$Name,$Status,$Detail) }
    Invoke-HealthChecks
    ($script:probeNames.Contains('DISM RestoreHealth') -and $script:probeNames.Contains('SFC scannow'))
}

Test-LegacyBehavior 'FALSE-SUCCESS' 'Denied service writes are suppressed and reported Applied.' {
    foreach ($name in @('Invoke-Action','Disable-ServiceByName')) { . ([scriptblock]::Create($definitions[$name])) }
    $Preset = 'Balanced'
    $script:probeStatus = ''
    $script:deniedCalls = 0
    $script:ScriptCmdlet = New-Object psobject
    $script:ScriptCmdlet | Add-Member ScriptMethod ShouldProcess { param($Target,$Action) $true }
    function Get-Service { param($Name,$ErrorAction) [pscustomobject]@{Name='MockService'; Status='Running'} }
    function Stop-Service { [CmdletBinding()] param($Name,[switch]$Force) $script:deniedCalls++; Write-Error 'Simulated access denied' }
    function Set-Service { [CmdletBinding()] param($Name,$StartupType) $script:deniedCalls++; Write-Error 'Simulated access denied' }
    function Add-Action { param($Category,$Name,$Status,$Detail) $script:probeStatus = $Status }
    Disable-ServiceByName -Name MockService
    ($script:deniedCalls -eq 2 -and $script:probeStatus -eq 'Applied')
}

Test-LegacyBehavior 'RESTORE-CONTINUE' 'Restore failure returns normally and execution can continue.' {
    foreach ($name in @('Invoke-Action','New-SystemRestorePoint')) { . ([scriptblock]::Create($definitions[$name])) }
    $Preset = 'Balanced'; $SkipRestorePoint = $false
    $script:SessionId = 'mock-session'; $script:probeStatus = ''
    $script:ScriptCmdlet = New-Object psobject
    $script:ScriptCmdlet | Add-Member ScriptMethod ShouldProcess { param($Target,$Action) $true }
    function Checkpoint-Computer { param($Description,$RestorePointType) throw 'Simulated restore failure' }
    function Add-Action { param($Category,$Name,$Status,$Detail) $script:probeStatus = $Status }
    New-SystemRestorePoint
    ($script:probeStatus -eq 'Failed')
}

Test-LegacyBehavior 'UNKNOWN-HARDWARE' 'Bluetooth and biometric probe failure is returned as false.' {
    foreach ($name in @('Test-BluetoothPresent','Test-BiometricPresent')) { . ([scriptblock]::Create($definitions[$name])) }
    function Get-PnpDevice { param($Class,$ErrorAction) throw 'Simulated inventory unavailable' }
    ((Test-BluetoothPresent) -eq $false -and (Test-BiometricPresent) -eq $false)
}

Test-LegacyBehavior 'PDF-PRINTER' 'A PDF-only setup leads Full to select Print Spooler for disabling.' {
    foreach ($name in @('Get-PhysicalPrinterCount','Apply-ServiceTweaks')) { . ([scriptblock]::Create($definitions[$name])) }
    function Get-Printer { param($ErrorAction) [pscustomobject]@{Name='Microsoft Print to PDF'; PortName='PORTPROMPT:'} }
    $Preset='Full'; $script:CoreDisableServices=@()
    $script:probeNames = New-Object System.Collections.Generic.List[string]
    function Disable-ServiceByName { param($Name,$Reason,[switch]$Wildcard) $script:probeNames.Add($Name) }
    function Set-ServiceManualByName { param($Name,$Reason) }
    function Add-Action { param($Category,$Name,$Status,$Detail) }
    function Add-Recommendation { param($Text) }
    $detection = [pscustomobject]@{PhysicalPrinters=(Get-PhysicalPrinterCount); EverythingInstalled=$false; OsDisk=[pscustomobject]@{IsSsd=$false}; BitLockerInUse=$true; BluetoothPresent=$true; BiometricPresent=$true}
    Apply-ServiceTweaks -Detection $detection
    ($detection.PhysicalPrinters -eq 0 -and $script:probeNames.Contains('Spooler'))
}

Test-LegacyBehavior 'POWER-DUPLICATE' 'Every execution creates another power scheme instead of checking current state first.' {
    . ([scriptblock]::Create($definitions['Set-UltimatePerformancePlan']))
    $Preset='Balanced'; $script:duplicateCalls=0
    function Invoke-Action { param($Category,$Name,$ScriptBlock,$Detail) & $ScriptBlock }
    function Get-CommandText { param($ScriptBlock) & $ScriptBlock }
    function powercfg {
        if ($args[0] -eq '-duplicatescheme') { $script:duplicateCalls++; 'Power Scheme GUID: 11111111-1111-1111-1111-111111111111' }
    }
    $detection=[pscustomobject]@{IsLaptop=$false; OnAcPower=$true}
    Set-UltimatePerformancePlan -Detection $detection
    Set-UltimatePerformancePlan -Detection $detection
    ($script:duplicateCalls -eq 2)
}

Test-LegacyBehavior 'NOOP-APPLIED' 'An absent AppX package is reported Applied without any removal.' {
    foreach ($name in @('Invoke-Action','Remove-BloatAppx')) { . ([scriptblock]::Create($definitions[$name])) }
    $Preset='Balanced'; $SkipAppRemoval=$false
    $script:BloatApps=@('Mock.Absent'); $script:ProtectedAppx=@()
    $script:probeStatus=''; $script:removeCalls=0
    $script:ScriptCmdlet=New-Object psobject
    $script:ScriptCmdlet | Add-Member ScriptMethod ShouldProcess { param($Target,$Action) $true }
    function Get-AppxPackage { param($Name,[switch]$AllUsers,$ErrorAction) }
    function Get-AppxProvisionedPackage { param([switch]$Online,$ErrorAction) }
    function Remove-AppxPackage { param($Package,[switch]$AllUsers,$ErrorAction) $script:removeCalls++ }
    function Remove-AppxProvisionedPackage { param([switch]$Online,$PackageName,$ErrorAction) $script:removeCalls++ }
    function Add-Action { param($Category,$Name,$Status,$Detail) $script:probeStatus=$Status }
    Remove-BloatAppx
    ($script:removeCalls -eq 0 -and $script:probeStatus -eq 'Applied')
}

[pscustomobject]@{
    Runtime=$PSVersionTable.PSVersion.ToString()
    SourceSHA256=$sourceHash
    SyntaxErrors=$parseErrors.Count
    ProbeCount=$results.Count
    ConfirmedCount=@($results | Where-Object Confirmed).Count
    Results=@($results.ToArray())
} | ConvertTo-Json -Depth 5
if (@($results | Where-Object { -not $_.Confirmed }).Count -gt 0) { exit 1 }
