#requires -version 5.1
<#
.SYNOPSIS
  Windows 11 debloat and speed optimization script based on win11_debloat_guide.md.

.DESCRIPTION
  Runs a mostly automatic Windows 11 optimization pass with minimal prompting.
  The script discovers hardware, Windows build, installed apps, services, printers,
  BitLocker state, network adapters, and tool availability before deciding what to do.

  Profiles:
    ReportOnly - collect before/after-style report without changing the machine.
    Balanced   - apply broadly safe privacy, UI, services, temp cleanup, and AppX cleanup.
    Full       - Balanced plus external tool installs, DNS/latency tweaks, and more cleanup.

  More fragile operations from the guide are present as explicit switches so a machine is
  not accidentally put into a hard-to-debug state.
#>

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [ValidateSet('ReportOnly', 'Balanced', 'Full')]
    [string]$Preset,

    [switch]$AssumeYes,
    [switch]$SkipRestorePoint,
    [switch]$SkipAppRemoval,
    [switch]$SkipExternalTools,
    [switch]$InstallTools,
    [switch]$RunHealthChecks,
    [switch]$RunBenchmarks,
    [switch]$InstallWindowsUpdates,

    [ValidateSet('Keep', 'Cloudflare', 'Google', 'Quad9', 'AdGuard')]
    [string]$DnsProvider = 'Keep',

    [switch]$BlockTelemetryHosts,
    [switch]$RunComponentResetBase,
    [switch]$EnableBcdLatencyTweaks,
    [switch]$SetManualPageFile,
    [switch]$AutoReboot,
    [switch]$NoSelfElevate,
    [switch]$NoGui,
    [switch]$NoRebootPrompt
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$Script:ScriptCmdlet = $PSCmdlet
$Script:OriginalBoundParameters = @{}
foreach ($entry in $PSBoundParameters.GetEnumerator()) {
    $Script:OriginalBoundParameters[$entry.Key] = $entry.Value
}
$Script:NeedsReboot = $false
$Script:StartupCancelled = $false
$Script:StartupUiShown = $false
$Script:Actions = New-Object System.Collections.Generic.List[object]
$Script:Recommendations = New-Object System.Collections.Generic.List[string]
$Script:SessionId = Get-Date -Format 'yyyyMMdd-HHmmss'
$Script:ScriptRoot = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }
$Script:ReportRoot = Join-Path $Script:ScriptRoot 'reports'
$Script:LogPath = Join-Path $Script:ReportRoot "win11-optimizer-$Script:SessionId.log"
$Script:MarkdownReportPath = Join-Path $Script:ReportRoot "win11-optimizer-$Script:SessionId.md"
$Script:JsonReportPath = Join-Path $Script:ReportRoot "win11-optimizer-$Script:SessionId.json"

$Script:BloatApps = @(
    'Microsoft.3DBuilder',
    'Microsoft.BingFinance',
    'Microsoft.BingNews',
    'Microsoft.BingSports',
    'Microsoft.BingWeather',
    'Microsoft.GamingApp',
    'Microsoft.GetHelp',
    'Microsoft.Getstarted',
    'Microsoft.Messaging',
    'Microsoft.Microsoft3DViewer',
    'Microsoft.MicrosoftOfficeHub',
    'Microsoft.MicrosoftSolitaireCollection',
    'Microsoft.MicrosoftStickyNotes',
    'Microsoft.MixedReality.Portal',
    'Microsoft.NetworkSpeedTest',
    'Microsoft.News',
    'Microsoft.Office.OneNote',
    'Microsoft.People',
    'Microsoft.Print3D',
    'Microsoft.SkypeApp',
    'Microsoft.Teams',
    'Microsoft.Todos',
    'Microsoft.WindowsAlarms',
    'Microsoft.WindowsFeedbackHub',
    'Microsoft.WindowsMaps',
    'Microsoft.WindowsSoundRecorder',
    'Microsoft.Xbox.TCUI',
    'Microsoft.XboxApp',
    'Microsoft.XboxGameOverlay',
    'Microsoft.XboxGamingOverlay',
    'Microsoft.XboxIdentityProvider',
    'Microsoft.XboxSpeechToTextOverlay',
    'Microsoft.YourPhone',
    'Microsoft.ZuneMusic',
    'Microsoft.ZuneVideo',
    'Clipchamp.Clipchamp',
    'MicrosoftCorporationII.MicrosoftFamily',
    'MicrosoftTeams'
)

$Script:ProtectedAppx = @(
    'Microsoft.WindowsStore',
    'Microsoft.DesktopAppInstaller',
    'Microsoft.UI.Xaml',
    'Microsoft.VCLibs',
    'Microsoft.NET.Native',
    'Microsoft.WindowsAppRuntime'
)

$Script:CoreDisableServices = @(
    @{ Name = 'DiagTrack'; Reason = 'Connected User Experiences and Telemetry' },
    @{ Name = 'dmwappushservice'; Reason = 'WAP Push Message Routing telemetry path' },
    @{ Name = 'RetailDemo'; Reason = 'Retail demo mode' },
    @{ Name = 'WerSvc'; Reason = 'Windows Error Reporting' },
    @{ Name = 'WMPNetworkSvc'; Reason = 'Windows Media Player Network Sharing' },
    @{ Name = 'lfsvc'; Reason = 'Geolocation Service' },
    @{ Name = 'MapsBroker'; Reason = 'Downloaded Maps Manager' },
    @{ Name = 'XblAuthManager'; Reason = 'Xbox Live Auth Manager' },
    @{ Name = 'XblGameSave'; Reason = 'Xbox Live Game Save' },
    @{ Name = 'XboxGipSvc'; Reason = 'Xbox Accessory Management' },
    @{ Name = 'XboxNetApiSvc'; Reason = 'Xbox Live Networking' },
    @{ Name = 'wisvc'; Reason = 'Windows Insider Service' },
    @{ Name = 'Fax'; Reason = 'Fax service' },
    @{ Name = 'PushToInstall'; Reason = 'Windows PushToInstall' },
    @{ Name = 'SEMgrSvc'; Reason = 'Payments and NFC/SE Manager' },
    @{ Name = 'SharedAccess'; Reason = 'Internet Connection Sharing' }
)

$Script:TelemetryTasks = @(
    '\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser',
    '\Microsoft\Windows\Application Experience\ProgramDataUpdater',
    '\Microsoft\Windows\Autochk\Proxy',
    '\Microsoft\Windows\Customer Experience Improvement Program\Consolidator',
    '\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip',
    '\Microsoft\Windows\Customer Experience Improvement Program\Uploader',
    '\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector',
    '\Microsoft\Windows\Feedback\Siuf\DmClient',
    '\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload',
    '\Microsoft\Windows\Maps\MapsToastTask',
    '\Microsoft\Windows\Maps\MapsUpdateTask',
    '\Microsoft\Windows\Shell\FamilySafetyMonitor',
    '\Microsoft\Windows\Shell\FamilySafetyRefreshTask',
    '\Microsoft\Windows\Windows Error Reporting\QueueReporting'
)

$Script:RecommendedTools = @(
    @{ Name = 'Everything'; Id = 'voidtools.Everything'; SearchNames = @('Everything'); Reason = 'Fast file search replacement for Windows Search' },
    @{ Name = 'Autoruns'; Id = 'Microsoft.Sysinternals.Autoruns'; SearchNames = @('Autoruns', 'Sysinternals Autoruns'); Reason = 'Startup and autorun inspection' },
    @{ Name = 'RAMMap'; Id = 'Microsoft.Sysinternals.RAMMap'; SearchNames = @('RAMMap', 'Sysinternals RAMMap'); Reason = 'Standby memory inspection and flushing' },
    @{ Name = 'WizTree'; Id = 'AntibodySoftware.WizTree'; SearchNames = @('WizTree'); Reason = 'Fast disk usage analysis' },
    @{ Name = 'WinDirStat'; Id = 'WinDirStat.WinDirStat'; SearchNames = @('WinDirStat'); Reason = 'Visual disk usage analysis' },
    @{ Name = 'Bulk Crap Uninstaller'; Id = 'Klocman.BulkCrapUninstaller'; SearchNames = @('Bulk Crap Uninstaller', 'BCUninstaller'); Reason = 'Batch uninstall and leftover cleanup' },
    @{ Name = 'Process Lasso'; Id = 'BitSum.ProcessLasso'; SearchNames = @('Process Lasso'); Reason = 'CPU priority and responsiveness management' },
    @{ Name = 'LatencyMon'; Id = 'Resplendence.LatencyMon'; SearchNames = @('LatencyMon'); Reason = 'DPC latency verification' },
    @{ Name = 'CrystalDiskMark'; Id = 'CrystalDewWorld.CrystalDiskMark'; SearchNames = @('CrystalDiskMark'); Reason = 'Disk benchmark verification' },
    @{ Name = 'DriverStore Explorer'; Id = 'lostindark.DriverStoreExplorer'; SearchNames = @('DriverStore Explorer', 'RAPR'); Reason = 'Old driver package cleanup' },
    @{ Name = 'O&O ShutUp10++'; Id = 'OO-Software.ShutUp10'; SearchNames = @('O&O ShutUp10++', 'ShutUp10'); Reason = 'Privacy and telemetry GUI baseline' },
    @{ Name = 'Snappy Driver Installer Origin'; Id = 'GlennDelahoy.SnappyDriverInstallerOrigin'; SearchNames = @('Snappy Driver Installer Origin'); Reason = 'Optional driver discovery and hygiene' }
)

$Script:TelemetryHosts = @(
    'telemetry.microsoft.com',
    'vortex.data.microsoft.com',
    'settings-win.data.microsoft.com',
    'watson.telemetry.microsoft.com',
    'oca.telemetry.microsoft.com',
    'sqm.telemetry.microsoft.com',
    'compatexchange.cloudapp.net',
    'cs1.wpc.v0cdn.net'
)

function Test-IsAdmin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Convert-BoundParametersToArgs {
    $argsList = New-Object System.Collections.Generic.List[string]
    foreach ($key in $Script:OriginalBoundParameters.Keys) {
        if ($key -eq 'NoSelfElevate') { continue }
        $value = $Script:OriginalBoundParameters[$key]
        if ($value -is [System.Management.Automation.SwitchParameter]) {
            if ($value.IsPresent) { $argsList.Add("-$key") }
        }
        elseif ($null -ne $value) {
            $escaped = [string]$value -replace '"', '\"'
            $argsList.Add("-$key")
            $argsList.Add("""$escaped""")
        }
    }
    return $argsList
}

if (-not $NoSelfElevate -and -not (Test-IsAdmin)) {
    Write-Host 'Administrator rights are required. Relaunching PowerShell elevated...' -ForegroundColor Yellow
    $argList = New-Object System.Collections.Generic.List[string]
    $argList.Add('-NoProfile')
    $argList.Add('-ExecutionPolicy')
    $argList.Add('Bypass')
    $argList.Add('-File')
    $argList.Add("""$PSCommandPath""")
    foreach ($arg in (Convert-BoundParametersToArgs)) { $argList.Add($arg) }
    Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $argList
    exit
}

function Initialize-Run {
    if (-not (Test-Path -LiteralPath $Script:ReportRoot)) {
        New-Item -ItemType Directory -Path $Script:ReportRoot -Force | Out-Null
    }

    Start-Transcript -Path $Script:LogPath -Force | Out-Null
}

function Stop-Run {
    try { Stop-Transcript | Out-Null } catch { }
}

function Add-Action {
    param(
        [string]$Category,
        [string]$Name,
        [string]$Status,
        [string]$Detail = ''
    )

    $Script:Actions.Add([pscustomobject]@{
        Time     = (Get-Date).ToString('s')
        Category = $Category
        Name     = $Name
        Status   = $Status
        Detail   = $Detail
    }) | Out-Null

    $color = switch ($Status) {
        'Applied' { 'Green' }
        'Skipped' { 'DarkYellow' }
        'Failed' { 'Red' }
        'Detected' { 'Cyan' }
        'ReportOnly' { 'Gray' }
        default { 'White' }
    }
    $line = "[{0}] {1}: {2}" -f $Status, $Category, $Name
    if ($Detail) { $line = "$line - $Detail" }
    Write-Host $line -ForegroundColor $color
}

function Add-Recommendation {
    param([string]$Text)
    if (-not [string]::IsNullOrWhiteSpace($Text)) {
        $Script:Recommendations.Add($Text) | Out-Null
    }
}

function Invoke-Action {
    param(
        [string]$Category,
        [string]$Name,
        [scriptblock]$ScriptBlock,
        [string]$Detail = ''
    )

    if ($Preset -eq 'ReportOnly') {
        Add-Action -Category $Category -Name $Name -Status 'ReportOnly' -Detail $Detail
        return
    }

    try {
        if ($Script:ScriptCmdlet.ShouldProcess($Name, $Category)) {
            & $ScriptBlock
            Add-Action -Category $Category -Name $Name -Status 'Applied' -Detail $Detail
        }
        else {
            Add-Action -Category $Category -Name $Name -Status 'Skipped' -Detail 'WhatIf/ShouldProcess skipped'
        }
    }
    catch {
        Add-Action -Category $Category -Name $Name -Status 'Failed' -Detail $_.Exception.Message
    }
}

function Set-ScriptSwitch {
    param(
        [string]$Name,
        [bool]$Value
    )

    $switchValue = [System.Management.Automation.SwitchParameter]::new($Value)
    Set-Variable -Scope Script -Name $Name -Value $switchValue
}

function Test-CanShowStartupUi {
    if ($NoGui) { return $false }
    if ($Preset -or $AssumeYes) { return $false }
    if (-not [Environment]::UserInteractive) { return $false }
    return $true
}

function Show-StartupOptionsUi {
    if (-not (Test-CanShowStartupUi)) { return $false }

    try {
        Add-Type -AssemblyName System.Windows.Forms
        Add-Type -AssemblyName System.Drawing
    }
    catch {
        Write-Host "Startup UI is not available: $($_.Exception.Message)" -ForegroundColor Yellow
        return $false
    }

    [System.Windows.Forms.Application]::EnableVisualStyles()

    $form = New-Object System.Windows.Forms.Form
    $form.Text = 'Windows 11 Optimizer'
    $form.StartPosition = 'CenterScreen'
    $form.FormBorderStyle = 'FixedDialog'
    $form.MaximizeBox = $false
    $form.MinimizeBox = $false
    $form.ClientSize = New-Object System.Drawing.Size(660, 600)
    $form.Font = New-Object System.Drawing.Font('Segoe UI', 9)

    $title = New-Object System.Windows.Forms.Label
    $title.Text = 'Choose the few things Windows cannot safely infer'
    $title.Font = New-Object System.Drawing.Font('Segoe UI Semibold', 12)
    $title.AutoSize = $true
    $title.Location = New-Object System.Drawing.Point(18, 16)
    $form.Controls.Add($title)

    $subtitle = New-Object System.Windows.Forms.Label
    $subtitle.Text = 'Everything else is detected automatically and the run will not stop for more questions.'
    $subtitle.AutoSize = $true
    $subtitle.Location = New-Object System.Drawing.Point(20, 45)
    $form.Controls.Add($subtitle)

    $presetLabel = New-Object System.Windows.Forms.Label
    $presetLabel.Text = 'Run profile'
    $presetLabel.AutoSize = $true
    $presetLabel.Location = New-Object System.Drawing.Point(22, 82)
    $form.Controls.Add($presetLabel)

    $presetCombo = New-Object System.Windows.Forms.ComboBox
    $presetCombo.DropDownStyle = 'DropDownList'
    $presetCombo.Items.AddRange([object[]]@('Full', 'Balanced', 'ReportOnly'))
    $presetCombo.SelectedItem = 'Full'
    $presetCombo.Location = New-Object System.Drawing.Point(130, 78)
    $presetCombo.Size = New-Object System.Drawing.Size(180, 26)
    $form.Controls.Add($presetCombo)

    $dnsLabel = New-Object System.Windows.Forms.Label
    $dnsLabel.Text = 'DNS provider'
    $dnsLabel.AutoSize = $true
    $dnsLabel.Location = New-Object System.Drawing.Point(340, 82)
    $form.Controls.Add($dnsLabel)

    $dnsCombo = New-Object System.Windows.Forms.ComboBox
    $dnsCombo.DropDownStyle = 'DropDownList'
    $dnsCombo.Items.AddRange([object[]]@('Cloudflare', 'Keep', 'Google', 'Quad9', 'AdGuard'))
    $dnsCombo.SelectedItem = 'Cloudflare'
    $dnsCombo.Location = New-Object System.Drawing.Point(440, 78)
    $dnsCombo.Size = New-Object System.Drawing.Size(180, 26)
    $form.Controls.Add($dnsCombo)

    $normalGroup = New-Object System.Windows.Forms.GroupBox
    $normalGroup.Text = 'Automatic choices'
    $normalGroup.Location = New-Object System.Drawing.Point(20, 120)
    $normalGroup.Size = New-Object System.Drawing.Size(610, 165)
    $form.Controls.Add($normalGroup)

    function New-UiCheckBox {
        param(
            [string]$Text,
            [int]$X,
            [int]$Y,
            [bool]$Checked = $false,
            [int]$Width = 560
        )

        $checkBox = New-Object System.Windows.Forms.CheckBox
        $checkBox.Text = $Text
        $checkBox.AutoSize = $false
        $checkBox.Size = New-Object System.Drawing.Size($Width, 24)
        $checkBox.Location = New-Object System.Drawing.Point($X, $Y)
        $checkBox.Checked = $Checked
        return $checkBox
    }

    $restoreCheck = New-UiCheckBox -Text 'Create a system restore point first' -X 16 -Y 28 -Checked $true
    $appRemovalCheck = New-UiCheckBox -Text 'Remove common bundled AppX/UWP bloat' -X 16 -Y 56 -Checked $true
    $toolInstallCheck = New-UiCheckBox -Text 'Install the external tool stack with winget' -X 16 -Y 84 -Checked $true
    $healthCheck = New-UiCheckBox -Text 'Run DISM RestoreHealth and SFC health checks' -X 16 -Y 112 -Checked $true
    $normalGroup.Controls.AddRange([System.Windows.Forms.Control[]]@($restoreCheck, $appRemovalCheck, $toolInstallCheck, $healthCheck))

    $riskGroup = New-Object System.Windows.Forms.GroupBox
    $riskGroup.Text = 'Must confirm up front'
    $riskGroup.Location = New-Object System.Drawing.Point(20, 300)
    $riskGroup.Size = New-Object System.Drawing.Size(610, 205)
    $form.Controls.Add($riskGroup)

    $hostsCheck = New-UiCheckBox -Text 'Edit hosts file to block Microsoft telemetry endpoints' -X 16 -Y 28
    $resetBaseCheck = New-UiCheckBox -Text 'Run DISM ResetBase (cannot uninstall current Windows updates afterward)' -X 16 -Y 56
    $pageFileCheck = New-UiCheckBox -Text 'Set a fixed pagefile size based on detected RAM' -X 16 -Y 84
    $bcdCheck = New-UiCheckBox -Text 'Apply BCDEdit latency tweaks for HPET/dynamic tick testing' -X 16 -Y 112
    $updatesCheck = New-UiCheckBox -Text 'Install Windows updates with PSWindowsUpdate (can take a long time)' -X 16 -Y 140
    $benchCheck = New-UiCheckBox -Text 'Run WinSAT benchmarks after changes' -X 16 -Y 168
    $riskGroup.Controls.AddRange([System.Windows.Forms.Control[]]@($hostsCheck, $resetBaseCheck, $pageFileCheck, $bcdCheck, $updatesCheck, $benchCheck))

    $rebootGroup = New-Object System.Windows.Forms.GroupBox
    $rebootGroup.Text = 'Finish behavior'
    $rebootGroup.Location = New-Object System.Drawing.Point(20, 515)
    $rebootGroup.Size = New-Object System.Drawing.Size(360, 60)
    $form.Controls.Add($rebootGroup)

    $autoRebootCheck = New-UiCheckBox -Text 'Automatically reboot at the end if needed' -X 16 -Y 24 -Width 320
    $rebootGroup.Controls.Add($autoRebootCheck)

    $runButton = New-Object System.Windows.Forms.Button
    $runButton.Text = 'Run'
    $runButton.Location = New-Object System.Drawing.Point(440, 530)
    $runButton.Size = New-Object System.Drawing.Size(90, 32)
    $runButton.DialogResult = [System.Windows.Forms.DialogResult]::OK
    $form.AcceptButton = $runButton
    $form.Controls.Add($runButton)

    $cancelButton = New-Object System.Windows.Forms.Button
    $cancelButton.Text = 'Cancel'
    $cancelButton.Location = New-Object System.Drawing.Point(540, 530)
    $cancelButton.Size = New-Object System.Drawing.Size(90, 32)
    $cancelButton.DialogResult = [System.Windows.Forms.DialogResult]::Cancel
    $form.CancelButton = $cancelButton
    $form.Controls.Add($cancelButton)

    $presetCombo.Add_SelectedIndexChanged({
        $isReportOnly = ([string]$presetCombo.SelectedItem -eq 'ReportOnly')
        foreach ($control in @($restoreCheck, $appRemovalCheck, $toolInstallCheck, $healthCheck, $hostsCheck, $resetBaseCheck, $pageFileCheck, $bcdCheck, $updatesCheck, $benchCheck, $autoRebootCheck, $dnsCombo)) {
            $control.Enabled = -not $isReportOnly
        }
        if ($isReportOnly) {
            $dnsCombo.SelectedItem = 'Keep'
        }
        elseif (-not $dnsCombo.SelectedItem) {
            $dnsCombo.SelectedItem = 'Cloudflare'
        }
    })

    $result = $form.ShowDialog()
    if ($result -ne [System.Windows.Forms.DialogResult]::OK) {
        $Script:StartupCancelled = $true
        return $true
    }

    $script:Preset = [string]$presetCombo.SelectedItem
    $script:DnsProvider = [string]$dnsCombo.SelectedItem
    Set-ScriptSwitch -Name 'SkipRestorePoint' -Value (-not $restoreCheck.Checked)
    Set-ScriptSwitch -Name 'SkipAppRemoval' -Value (-not $appRemovalCheck.Checked)
    Set-ScriptSwitch -Name 'SkipExternalTools' -Value (-not $toolInstallCheck.Checked)
    Set-ScriptSwitch -Name 'InstallTools' -Value $toolInstallCheck.Checked
    Set-ScriptSwitch -Name 'RunHealthChecks' -Value $healthCheck.Checked
    Set-ScriptSwitch -Name 'BlockTelemetryHosts' -Value $hostsCheck.Checked
    Set-ScriptSwitch -Name 'RunComponentResetBase' -Value $resetBaseCheck.Checked
    Set-ScriptSwitch -Name 'SetManualPageFile' -Value $pageFileCheck.Checked
    Set-ScriptSwitch -Name 'EnableBcdLatencyTweaks' -Value $bcdCheck.Checked
    Set-ScriptSwitch -Name 'InstallWindowsUpdates' -Value $updatesCheck.Checked
    Set-ScriptSwitch -Name 'RunBenchmarks' -Value $benchCheck.Checked
    Set-ScriptSwitch -Name 'AutoReboot' -Value $autoRebootCheck.Checked
    Set-ScriptSwitch -Name 'NoRebootPrompt' -Value $true
    Set-ScriptSwitch -Name 'AssumeYes' -Value $true
    $Script:StartupUiShown = $true
    return $true
}

function Select-RunProfile {
    if (Show-StartupOptionsUi) { return }

    if ($Preset) { return }

    if ($AssumeYes) {
        $script:Preset = 'Full'
        if ($DnsProvider -eq 'Keep') { $script:DnsProvider = 'Cloudflare' }
        Set-ScriptSwitch -Name 'InstallTools' -Value $true
        Set-ScriptSwitch -Name 'RunHealthChecks' -Value $true
        Set-ScriptSwitch -Name 'NoRebootPrompt' -Value $true
        return
    }

    Write-Host ''
    Write-Host 'Choose one automatic run profile:' -ForegroundColor Cyan
    Write-Host '  1. Balanced   - recommended: safe debloat, privacy, services, AppX cleanup, reports'
    Write-Host '  2. Full       - Balanced plus tool installs, DNS, network latency and extra cleanup'
    Write-Host '  3. ReportOnly - no changes, just inventory and recommendations'
    Write-Host ''
    $choice = Read-Host 'Profile [1]'

    switch ($choice) {
        '2' { $script:Preset = 'Full' }
        '3' { $script:Preset = 'ReportOnly' }
        default { $script:Preset = 'Balanced' }
    }
}

function Convert-KbToGb {
    param([Nullable[double]]$Kb)
    if ($null -eq $Kb) { return $null }
    return [math]::Round(($Kb / 1MB), 2)
}

function Convert-BytesToGb {
    param([Nullable[double]]$Bytes)
    if ($null -eq $Bytes) { return $null }
    return [math]::Round(($Bytes / 1GB), 2)
}

function Get-CommandText {
    param([scriptblock]$ScriptBlock)
    try {
        return (& $ScriptBlock 2>&1 | Out-String).Trim()
    }
    catch {
        return $_.Exception.Message
    }
}

function Test-CommandExists {
    param([string]$Name)
    return [bool](Get-Command -Name $Name -ErrorAction SilentlyContinue)
}

function Invoke-NativeCommand {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [switch]$AllowNonZeroExit
    )

    & $FilePath @Arguments | Out-Host
    $exitCode = $LASTEXITCODE
    if (-not $AllowNonZeroExit -and $null -ne $exitCode -and $exitCode -ne 0) {
        throw "$FilePath exited with code $exitCode"
    }
}

function Install-WingetPackage {
    param(
        [hashtable]$Tool
    )

    $errors = New-Object System.Collections.Generic.List[string]
    $ids = @($Tool.Id)
    if ($Tool.ContainsKey('AlternateIds')) {
        $ids += @($Tool.AlternateIds)
    }

    foreach ($id in ($ids | Where-Object { $_ } | Select-Object -Unique)) {
        try {
            Invoke-NativeCommand -FilePath 'winget' -Arguments @(
                'install',
                '--id', $id,
                '--exact',
                '--silent',
                '--accept-package-agreements',
                '--accept-source-agreements'
            )
            return
        }
        catch {
            $errors.Add("id ${id}: $($_.Exception.Message)") | Out-Null
        }
    }

    $names = @($Tool.Name)
    if ($Tool.ContainsKey('SearchNames')) {
        $names += @($Tool.SearchNames)
    }

    foreach ($name in ($names | Where-Object { $_ } | Select-Object -Unique)) {
        try {
            Invoke-NativeCommand -FilePath 'winget' -Arguments @(
                'install',
                '--name', $name,
                '--silent',
                '--accept-package-agreements',
                '--accept-source-agreements'
            )
            return
        }
        catch {
            $errors.Add("name ${name}: $($_.Exception.Message)") | Out-Null
        }
    }

    throw "winget could not install $($Tool.Name). Attempts: $($errors -join '; ')"
}

function Get-ActivePowerPlanText {
    return Get-CommandText { powercfg /getactivescheme }
}

function Get-HibernationState {
    $output = Get-CommandText { powercfg /a }
    $hiberboot = $null
    try {
        $hiberboot = (Get-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power' -Name 'HiberbootEnabled' -ErrorAction SilentlyContinue).HiberbootEnabled
    }
    catch { }

    return [pscustomobject]@{
        FastStartupEnabled = $hiberboot
        PowercfgA          = $output
    }
}

function Test-IsLaptop {
    $hasBattery = [bool](Get-CimInstance Win32_Battery -ErrorAction SilentlyContinue)
    $laptopChassisTypes = @(8, 9, 10, 11, 12, 14, 18, 21, 30, 31, 32)
    $chassis = @(Get-CimInstance Win32_SystemEnclosure -ErrorAction SilentlyContinue | ForEach-Object { $_.ChassisTypes } | ForEach-Object { $_ })
    $isLaptopChassis = [bool]($chassis | Where-Object { $laptopChassisTypes -contains [int]$_ })
    return ($hasBattery -or $isLaptopChassis)
}

function Test-OnAcPower {
    $battery = Get-CimInstance Win32_Battery -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $battery) { return $true }
    return @(2, 6, 7, 8, 9, 11).Contains([int]$battery.BatteryStatus)
}

function Get-OsDiskInfo {
    $systemDrive = $env:SystemDrive.TrimEnd(':')
    $partition = Get-Partition -DriveLetter $systemDrive -ErrorAction SilentlyContinue | Select-Object -First 1
    $disk = $null
    $physical = $null

    if ($partition) {
        $disk = Get-Disk -Number $partition.DiskNumber -ErrorAction SilentlyContinue
        $physicalDisks = @(Get-PhysicalDisk -ErrorAction SilentlyContinue)
        if ($disk) {
            $physical = $physicalDisks | Where-Object {
                $_.DeviceId -eq [string]$partition.DiskNumber -or $_.FriendlyName -eq $disk.FriendlyName
            } | Select-Object -First 1
        }
    }

    $busType = if ($disk) { [string]$disk.BusType } else { 'Unknown' }
    $mediaType = if ($physical) { [string]$physical.MediaType } else { 'Unknown' }
    $isSsd = ($busType -match 'NVMe|SSD') -or ($mediaType -match 'SSD')

    return [pscustomobject]@{
        DriveLetter = $systemDrive
        DiskNumber  = if ($disk) { $disk.Number } else { $null }
        Friendly    = if ($disk) { $disk.FriendlyName } else { $null }
        BusType     = $busType
        MediaType   = $mediaType
        IsSsd       = $isSsd
    }
}

function Test-EverythingInstalled {
    $service = Get-Service -Name 'Everything*' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($service) { return $true }

    $paths = @(
        "$env:ProgramFiles\Everything\Everything.exe",
        "${env:ProgramFiles(x86)}\Everything\Everything.exe"
    )
    return [bool]($paths | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1)
}

function Get-PhysicalPrinterCount {
    try {
        $virtualNames = 'Microsoft Print to PDF|Microsoft XPS Document Writer|OneNote|Fax|PDF|XPS'
        $virtualPorts = 'PORTPROMPT|FILE:|nul:|SHRFAX'
        $printers = @(Get-Printer -ErrorAction SilentlyContinue)
        return @($printers | Where-Object {
            $_.Name -notmatch $virtualNames -and $_.PortName -notmatch $virtualPorts
        }).Count
    }
    catch {
        return $null
    }
}

function Test-BitLockerInUse {
    try {
        if (Test-CommandExists 'Get-BitLockerVolume') {
            return [bool](Get-BitLockerVolume -ErrorAction SilentlyContinue | Where-Object {
                $_.ProtectionStatus -eq 'On' -or $_.VolumeStatus -match 'Encryption|Encrypted'
            } | Select-Object -First 1)
        }
    }
    catch { }

    $manageBde = Get-CommandText { manage-bde -status }
    return ($manageBde -match 'Protection Status:\s+Protection On' -or $manageBde -match 'Percentage Encrypted:\s+(?!0\.0%)')
}

function Test-BluetoothPresent {
    try {
        return [bool](Get-PnpDevice -Class Bluetooth -ErrorAction SilentlyContinue | Where-Object { $_.Status -ne 'Unknown' } | Select-Object -First 1)
    }
    catch {
        return $false
    }
}

function Test-BiometricPresent {
    try {
        return [bool](Get-PnpDevice -Class Biometric -ErrorAction SilentlyContinue | Select-Object -First 1)
    }
    catch {
        return $false
    }
}

function Get-ActiveNetworkAdapters {
    try {
        return @(Get-NetAdapter -Physical -ErrorAction SilentlyContinue | Where-Object { $_.Status -eq 'Up' })
    }
    catch {
        return @()
    }
}

function Get-DetectionState {
    $os = Get-CimInstance Win32_OperatingSystem
    $computer = Get-CimInstance Win32_ComputerSystem
    $osDisk = Get-OsDiskInfo
    $activeAdapters = @(Get-ActiveNetworkAdapters)
    $sleepStates = Get-CommandText { powercfg /a }

    $detection = [pscustomobject]@{
        ComputerName        = $env:COMPUTERNAME
        UserName            = [Environment]::UserName
        WindowsCaption      = $os.Caption
        WindowsVersion      = $os.Version
        WindowsBuild        = $os.BuildNumber
        Manufacturer        = $computer.Manufacturer
        Model               = $computer.Model
        TotalRamGb          = [math]::Round(($computer.TotalPhysicalMemory / 1GB), 2)
        IsLaptop            = Test-IsLaptop
        OnAcPower           = Test-OnAcPower
        OsDisk              = $osDisk
        EverythingInstalled = Test-EverythingInstalled
        PhysicalPrinters    = Get-PhysicalPrinterCount
        BitLockerInUse      = Test-BitLockerInUse
        BluetoothPresent    = Test-BluetoothPresent
        BiometricPresent    = Test-BiometricPresent
        ModernStandbyS0     = ($sleepStates -match 'Standby \(S0 Low Power Idle\)')
        ActiveAdapters      = @($activeAdapters | Select-Object Name, InterfaceDescription, InterfaceGuid, LinkSpeed, MacAddress)
        WingetAvailable     = Test-CommandExists 'winget'
    }

    Add-Action -Category 'Detect' -Name 'Windows and hardware state' -Status 'Detected' -Detail ("{0} build {1}; RAM {2} GB; laptop={3}; SSD={4}" -f $detection.WindowsCaption, $detection.WindowsBuild, $detection.TotalRamGb, $detection.IsLaptop, $detection.OsDisk.IsSsd)
    Add-Action -Category 'Detect' -Name 'Conditional features' -Status 'Detected' -Detail ("Everything={0}; printers={1}; BitLocker={2}; Bluetooth={3}; biometrics={4}" -f $detection.EverythingInstalled, $detection.PhysicalPrinters, $detection.BitLockerInUse, $detection.BluetoothPresent, $detection.BiometricPresent)

    return $detection
}

function Update-DetectionAfterToolInstall {
    param([object]$Detection)

    $beforeEverything = [bool]$Detection.EverythingInstalled
    $afterEverything = Test-EverythingInstalled
    $Detection.EverythingInstalled = $afterEverything
    $Detection.WingetAvailable = Test-CommandExists 'winget'

    if ($beforeEverything -ne $afterEverything) {
        Add-Action -Category 'Detect' -Name 'Post-tool detection refresh' -Status 'Detected' -Detail "EverythingInstalled: $beforeEverything -> $afterEverything"
    }
}

function Get-ServiceSummary {
    $names = @($Script:CoreDisableServices | ForEach-Object { $_.Name })
    $names += @('WSearch', 'SysMain', 'Spooler', 'BDESVC', 'RemoteRegistry', 'WbioSrvc', 'wlidsvc')
    $services = @(Get-CimInstance Win32_Service -ErrorAction SilentlyContinue | Where-Object { $names -contains $_.Name })
    return [pscustomobject]@{
        TargetCount  = $services.Count
        RunningCount = @($services | Where-Object { $_.State -eq 'Running' }).Count
        AutoCount    = @($services | Where-Object { $_.StartMode -eq 'Auto' }).Count
        DisabledCount = @($services | Where-Object { $_.StartMode -eq 'Disabled' }).Count
    }
}

function Get-AppxBloatSummary {
    $installedCount = 0
    $provisionedCount = 0

    try {
        $installed = @(Get-AppxPackage -AllUsers -ErrorAction SilentlyContinue | Where-Object { $Script:BloatApps -contains $_.Name })
        $installedCount = $installed.Count
    }
    catch { }

    try {
        $provisioned = @(Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object { $Script:BloatApps -contains $_.DisplayName })
        $provisionedCount = $provisioned.Count
    }
    catch { }

    return [pscustomobject]@{
        InstalledBloatPackages   = $installedCount
        ProvisionedBloatPackages = $provisionedCount
    }
}

function Get-StartupSummary {
    try {
        $startup = @(Get-CimInstance Win32_StartupCommand -ErrorAction SilentlyContinue)
        return [pscustomobject]@{
            Count = $startup.Count
            Items = @($startup | Select-Object Name, Command, Location)
        }
    }
    catch {
        return [pscustomobject]@{ Count = $null; Items = @() }
    }
}

function Get-TaskSummary {
    $enabled = 0
    foreach ($taskPath in $Script:TelemetryTasks) {
        $task = Get-ScheduledTaskByFullPath -FullPath $taskPath
        if ($task -and $task.State -ne 'Disabled') { $enabled++ }
    }
    return [pscustomobject]@{
        TelemetryTasksEnabled = $enabled
        TargetTasks           = $Script:TelemetryTasks.Count
    }
}

function Get-DiskSummary {
    $logical = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='$env:SystemDrive'" -ErrorAction SilentlyContinue
    return [pscustomobject]@{
        Drive    = $env:SystemDrive
        SizeGb   = if ($logical) { Convert-BytesToGb $logical.Size } else { $null }
        FreeGb   = if ($logical) { Convert-BytesToGb $logical.FreeSpace } else { $null }
        FreePct  = if ($logical -and $logical.Size -gt 0) { [math]::Round(($logical.FreeSpace / $logical.Size) * 100, 1) } else { $null }
    }
}

function Get-MemorySummary {
    $os = Get-CimInstance Win32_OperatingSystem
    $mma = $null
    try { $mma = Get-MMAgent -ErrorAction SilentlyContinue } catch { }
    return [pscustomobject]@{
        TotalGb           = Convert-KbToGb $os.TotalVisibleMemorySize
        FreeGb            = Convert-KbToGb $os.FreePhysicalMemory
        UsedGb            = Convert-KbToGb ($os.TotalVisibleMemorySize - $os.FreePhysicalMemory)
        MemoryCompression = if ($mma) { $mma.MemoryCompression } else { $null }
    }
}

function Get-NetworkSummary {
    $adapters = @(Get-ActiveNetworkAdapters)
    $dns = @()
    foreach ($adapter in $adapters) {
        try {
            $dns += Get-DnsClientServerAddress -InterfaceIndex $adapter.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue |
                Select-Object InterfaceAlias, ServerAddresses
        }
        catch { }
    }

    return [pscustomobject]@{
        ActiveAdapterCount = $adapters.Count
        DnsServers         = $dns
    }
}

function Get-SystemSnapshot {
    param([string]$Label)

    $os = Get-CimInstance Win32_OperatingSystem
    $pageFile = @()
    try { $pageFile = @(Get-CimInstance Win32_PageFileUsage -ErrorAction SilentlyContinue | Select-Object Name, AllocatedBaseSize, CurrentUsage) } catch { }

    return [pscustomobject]@{
        Label        = $Label
        Timestamp    = (Get-Date).ToString('s')
        LastBoot     = $os.LastBootUpTime
        UptimeHours  = [math]::Round(((Get-Date) - $os.LastBootUpTime).TotalHours, 2)
        ProcessCount = @(Get-Process -ErrorAction SilentlyContinue).Count
        Startup      = Get-StartupSummary
        Memory       = Get-MemorySummary
        Disk         = Get-DiskSummary
        Services     = Get-ServiceSummary
        AppxBloat    = Get-AppxBloatSummary
        Tasks        = Get-TaskSummary
        Network      = Get-NetworkSummary
        PowerPlan    = Get-ActivePowerPlanText
        Hibernation  = Get-HibernationState
        PageFile     = $pageFile
    }
}

function Ensure-RegKey {
    param([string]$Path)
    if (-not (Test-Path -Path $Path)) {
        New-Item -Path $Path -Force | Out-Null
    }
}

function Set-RegDword {
    param(
        [string]$Path,
        [string]$Name,
        [int]$Value
    )
    Ensure-RegKey -Path $Path
    New-ItemProperty -Path $Path -Name $Name -Value $Value -PropertyType DWord -Force | Out-Null
}

function Set-RegString {
    param(
        [string]$Path,
        [string]$Name,
        [string]$Value
    )
    Ensure-RegKey -Path $Path
    New-ItemProperty -Path $Path -Name $Name -Value $Value -PropertyType String -Force | Out-Null
}

function Disable-ServiceByName {
    param(
        [string]$Name,
        [string]$Reason = '',
        [switch]$Wildcard
    )

    $services = @(
        if ($Wildcard) {
            Get-Service -Name "$Name*" -ErrorAction SilentlyContinue
        }
        else {
            Get-Service -Name $Name -ErrorAction SilentlyContinue
        }
    )

    if (-not $services -or @($services).Count -eq 0) {
        Add-Action -Category 'Services' -Name $Name -Status 'Skipped' -Detail 'Service not present'
        return
    }

    foreach ($service in @($services)) {
        Invoke-Action -Category 'Services' -Name $service.Name -Detail $Reason -ScriptBlock {
            if ($service.Status -ne 'Stopped') {
                Stop-Service -Name $service.Name -Force -ErrorAction SilentlyContinue
            }
            Set-Service -Name $service.Name -StartupType Disabled -ErrorAction SilentlyContinue
        }
    }
}

function Set-ServiceManualByName {
    param(
        [string]$Name,
        [string]$Reason = ''
    )

    $service = Get-Service -Name $Name -ErrorAction SilentlyContinue
    if (-not $service) {
        Add-Action -Category 'Services' -Name $Name -Status 'Skipped' -Detail 'Service not present'
        return
    }

    Invoke-Action -Category 'Services' -Name "$Name -> Manual" -Detail $Reason -ScriptBlock {
        Set-Service -Name $Name -StartupType Manual -ErrorAction SilentlyContinue
    }
}

function Get-ScheduledTaskByFullPath {
    param([string]$FullPath)

    $taskName = Split-Path -Path $FullPath -Leaf
    $taskPath = Split-Path -Path $FullPath -Parent
    if (-not $taskPath.EndsWith('\')) { $taskPath = "$taskPath\" }

    try {
        return Get-ScheduledTask -TaskPath $taskPath -TaskName $taskName -ErrorAction SilentlyContinue
    }
    catch {
        return $null
    }
}

function Disable-ScheduledTaskByFullPath {
    param([string]$FullPath)

    $taskName = Split-Path -Path $FullPath -Leaf
    $taskPath = Split-Path -Path $FullPath -Parent
    if (-not $taskPath.EndsWith('\')) { $taskPath = "$taskPath\" }

    $task = Get-ScheduledTaskByFullPath -FullPath $FullPath
    if (-not $task) {
        Add-Action -Category 'ScheduledTasks' -Name $FullPath -Status 'Skipped' -Detail 'Task not present'
        return
    }

    Invoke-Action -Category 'ScheduledTasks' -Name $FullPath -ScriptBlock {
        Disable-ScheduledTask -TaskPath $taskPath -TaskName $taskName -ErrorAction SilentlyContinue | Out-Null
    }
}

function New-SystemRestorePoint {
    if ($SkipRestorePoint -or $Preset -eq 'ReportOnly') {
        Add-Action -Category 'Preflight' -Name 'System restore point' -Status 'Skipped' -Detail 'Skipped by profile or parameter'
        return
    }

    Invoke-Action -Category 'Preflight' -Name 'System restore point' -Detail 'Pre-Debloat restore point' -ScriptBlock {
        Checkpoint-Computer -Description "Pre-Debloat $Script:SessionId" -RestorePointType 'MODIFY_SETTINGS'
    }
}

function Set-UltimatePerformancePlan {
    param([object]$Detection)

    if ($Detection.IsLaptop -and -not $Detection.OnAcPower -and $Preset -ne 'Full') {
        Add-Action -Category 'Power' -Name 'Ultimate Performance power plan' -Status 'Skipped' -Detail 'Laptop is on battery; kept current plan'
        return
    }

    Invoke-Action -Category 'Power' -Name 'Ultimate Performance power plan' -ScriptBlock {
        $schemeOutput = Get-CommandText { powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61 }
        $guid = ([regex]::Match($schemeOutput, '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}')).Value

        if (-not $guid) {
            $list = Get-CommandText { powercfg /list }
            $line = ($list -split "`r?`n" | Where-Object { $_ -match 'Ultimate Performance' } | Select-Object -First 1)
            if ($line) {
                $guid = ([regex]::Match($line, '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}')).Value
            }
        }

        if ($guid) {
            powercfg /setactive $guid | Out-Null
        }
        else {
            throw 'Could not find or create Ultimate Performance power scheme'
        }
    }
}

function Disable-FastStartup {
    param([object]$Detection)

    Invoke-Action -Category 'Power' -Name 'Disable Windows Fast Startup' -ScriptBlock {
        Set-RegDword -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power' -Name 'HiberbootEnabled' -Value 0
        if (-not $Detection.IsLaptop) {
            powercfg /h off | Out-Null
        }
        else {
            Add-Recommendation 'Laptop detected: hibernation was kept available while Fast Startup was disabled.'
        }
        $Script:NeedsReboot = $true
    }
}

function Apply-BuiltInTweaks {
    param([object]$Detection)

    Set-UltimatePerformancePlan -Detection $Detection
    Disable-FastStartup -Detection $Detection

    Invoke-Action -Category 'UI' -Name 'Visual effects performance baseline' -ScriptBlock {
        Set-RegDword -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects' -Name 'VisualFXSetting' -Value 2
        Set-RegString -Path 'HKCU:\Control Panel\Desktop' -Name 'FontSmoothing' -Value '2'
    }

    Invoke-Action -Category 'UI' -Name 'Menu and hover response delays' -ScriptBlock {
        Set-RegString -Path 'HKCU:\Control Panel\Desktop' -Name 'MenuShowDelay' -Value '0'
        Set-RegString -Path 'HKCU:\Control Panel\Mouse' -Name 'MouseHoverTime' -Value '0'
        Set-RegDword -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer' -Name 'NoLowDiskSpaceChecks' -Value 1
    }

    Invoke-Action -Category 'Updates' -Name 'Disable Delivery Optimization P2P' -ScriptBlock {
        Set-RegDword -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization' -Name 'DODownloadMode' -Value 0
    }

    Invoke-Action -Category 'Gaming' -Name 'Disable Xbox GameDVR and Game Bar capture' -ScriptBlock {
        Set-RegDword -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR' -Name 'AppCaptureEnabled' -Value 0
        Set-RegDword -Path 'HKCU:\System\GameConfigStore' -Name 'GameDVR_Enabled' -Value 0
        Set-RegDword -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR' -Name 'AllowGameDVR' -Value 0
    }

    if (-not $Detection.IsLaptop -and $Detection.ModernStandbyS0) {
        Invoke-Action -Category 'Power' -Name 'Disable Modern Standby S0 on desktop' -Detail 'Reboot required' -ScriptBlock {
            Set-RegDword -Path 'HKLM:\System\CurrentControlSet\Control\Power' -Name 'PlatformAoAcOverride' -Value 0
            $Script:NeedsReboot = $true
        }
    }
    else {
        Add-Action -Category 'Power' -Name 'Modern Standby S0 override' -Status 'Skipped' -Detail 'Not a desktop with detected S0 Modern Standby'
    }
}

function Apply-PrivacyTweaks {
    Invoke-Action -Category 'Privacy' -Name 'Telemetry policy minimum' -ScriptBlock {
        Set-RegDword -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' -Name 'AllowTelemetry' -Value 0
        Set-RegDword -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection' -Name 'AllowTelemetry' -Value 0
    }

    Invoke-Action -Category 'Privacy' -Name 'Disable Cortana policy' -ScriptBlock {
        Set-RegDword -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search' -Name 'AllowCortana' -Value 0
        Set-RegDword -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search' -Name 'AllowCortanaAboveLock' -Value 0
    }

    Invoke-Action -Category 'Privacy' -Name 'Disable advertising ID and tailored experiences' -ScriptBlock {
        Set-RegDword -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo' -Name 'Enabled' -Value 0
        Set-RegDword -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Privacy' -Name 'TailoredExperiencesWithDiagnosticDataEnabled' -Value 0
    }

    Invoke-Action -Category 'Privacy' -Name 'Disable Windows consumer app suggestions' -ScriptBlock {
        Set-RegDword -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\CloudContent' -Name 'DisableWindowsConsumerFeatures' -Value 1
        Set-RegDword -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SystemPaneSuggestionsEnabled' -Value 0
        Set-RegDword -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SilentInstalledAppsEnabled' -Value 0
        Set-RegDword -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SoftLandingEnabled' -Value 0
    }

    Invoke-Action -Category 'Privacy' -Name 'Disable Activity History publishing' -ScriptBlock {
        Set-RegDword -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Name 'EnableActivityFeed' -Value 0
        Set-RegDword -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Name 'PublishUserActivities' -Value 0
    }

    Invoke-Action -Category 'Privacy' -Name 'Disable feedback request cadence' -ScriptBlock {
        Set-RegDword -Path 'HKCU:\Software\Microsoft\Siuf\Rules' -Name 'NumberOfSIUFInPeriod' -Value 0
        Set-RegDword -Path 'HKCU:\Software\Microsoft\Siuf\Rules' -Name 'PeriodInNanoSeconds' -Value 0
    }
}

function Apply-ServiceTweaks {
    param([object]$Detection)

    foreach ($svc in $Script:CoreDisableServices) {
        Disable-ServiceByName -Name $svc.Name -Reason $svc.Reason
    }

    Disable-ServiceByName -Name 'RemoteRegistry' -Reason 'Remote registry editing is rarely needed'

    if ($Detection.EverythingInstalled) {
        Disable-ServiceByName -Name 'WSearch' -Reason 'Everything is installed, so Windows Search indexing can be disabled'
    }
    else {
        Set-ServiceManualByName -Name 'WSearch' -Reason 'Everything not detected; reduced to Manual instead of disabling search'
        Add-Recommendation 'Everything was not detected. Windows Search was not fully disabled; install Everything then rerun Full profile if you want indexing off.'
    }

    if ($Detection.OsDisk.IsSsd) {
        Disable-ServiceByName -Name 'SysMain' -Reason 'System drive detected as SSD/NVMe'
    }
    else {
        Add-Action -Category 'Services' -Name 'SysMain' -Status 'Skipped' -Detail 'System drive was not detected as SSD/NVMe'
    }

    if ($Preset -eq 'Full') {
        if ($Detection.PhysicalPrinters -eq 0) {
            Disable-ServiceByName -Name 'Spooler' -Reason 'No physical printers detected'
        }
        else {
            Add-Action -Category 'Services' -Name 'Spooler' -Status 'Skipped' -Detail 'Printer detected or printer state unknown'
        }

        if (-not $Detection.BitLockerInUse) {
            Disable-ServiceByName -Name 'BDESVC' -Reason 'No BitLocker-protected volumes detected'
        }
        else {
            Add-Action -Category 'Services' -Name 'BDESVC' -Status 'Skipped' -Detail 'BitLocker is in use'
        }

        if (-not $Detection.BluetoothPresent) {
            Disable-ServiceByName -Name 'BluetoothUserService' -Wildcard -Reason 'No Bluetooth devices detected'
        }
        else {
            Add-Action -Category 'Services' -Name 'BluetoothUserService' -Status 'Skipped' -Detail 'Bluetooth present'
        }

        if (-not $Detection.BiometricPresent) {
            Disable-ServiceByName -Name 'WbioSrvc' -Reason 'No biometric hardware detected'
        }
        else {
            Add-Action -Category 'Services' -Name 'WbioSrvc' -Status 'Skipped' -Detail 'Biometric device present'
        }
    }
    else {
        Add-Recommendation 'Full profile can also disable Spooler, BDESVC, BluetoothUserService, and WbioSrvc when hardware checks say they are unused.'
    }

    Add-Recommendation 'wlidsvc and Themes were intentionally left enabled because they commonly affect Microsoft account sign-in and the Windows shell.'
}

function Apply-ScheduledTaskTweaks {
    foreach ($task in $Script:TelemetryTasks) {
        Disable-ScheduledTaskByFullPath -FullPath $task
    }
}

function Remove-BloatAppx {
    if ($SkipAppRemoval) {
        Add-Action -Category 'AppX' -Name 'Bloat package removal' -Status 'Skipped' -Detail 'Skipped by parameter'
        return
    }

    foreach ($app in $Script:BloatApps) {
        if ($Script:ProtectedAppx | Where-Object { $app -like "$($_)*" }) {
            Add-Action -Category 'AppX' -Name $app -Status 'Skipped' -Detail 'Protected dependency/store package'
            continue
        }

        Invoke-Action -Category 'AppX' -Name $app -ScriptBlock {
            $packages = @(Get-AppxPackage -Name $app -AllUsers -ErrorAction SilentlyContinue)
            foreach ($package in $packages) {
                try {
                    Remove-AppxPackage -Package $package.PackageFullName -AllUsers -ErrorAction SilentlyContinue
                }
                catch {
                    Remove-AppxPackage -Package $package.PackageFullName -ErrorAction SilentlyContinue
                }
            }

            $provisioned = @(Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -eq $app })
            foreach ($prov in $provisioned) {
                Remove-AppxProvisionedPackage -Online -PackageName $prov.PackageName -ErrorAction SilentlyContinue | Out-Null
            }
        }
    }
}

function Clear-TempFiles {
    $targets = @(
        'C:\Windows\Temp',
        $env:TEMP
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -Unique

    foreach ($target in $targets) {
        Invoke-Action -Category 'Disk' -Name "Clear temp files: $target" -ScriptBlock {
            Get-ChildItem -LiteralPath $target -Force -ErrorAction SilentlyContinue |
                Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

function Invoke-DismCleanup {
    Invoke-Action -Category 'Disk' -Name 'DISM component store cleanup' -ScriptBlock {
        Invoke-NativeCommand -FilePath 'DISM.exe' -Arguments @('/Online', '/Cleanup-Image', '/StartComponentCleanup')
    }

    if ($RunComponentResetBase) {
        Invoke-Action -Category 'Disk' -Name 'DISM ResetBase cleanup' -Detail 'Windows Updates cannot be uninstalled after this' -ScriptBlock {
            Invoke-NativeCommand -FilePath 'DISM.exe' -Arguments @('/Online', '/Cleanup-Image', '/StartComponentCleanup', '/ResetBase')
        }
    }
    else {
        Add-Recommendation 'DISM /ResetBase was not run. Use -RunComponentResetBase only after Windows Updates are known stable.'
    }
}

function Set-TrimEnabled {
    Invoke-Action -Category 'Disk' -Name 'Enable SSD TRIM' -ScriptBlock {
        Invoke-NativeCommand -FilePath 'fsutil.exe' -Arguments @('behavior', 'set', 'DisableDeleteNotify', '0')
    }
}

function Apply-MemoryTweaks {
    param([object]$Detection)

    if ($Detection.TotalRamGb -ge 32 -and $Preset -eq 'Full') {
        Invoke-Action -Category 'Memory' -Name 'Disable memory compression for high-RAM system' -ScriptBlock {
            Disable-MMAgent -MemoryCompression
            $Script:NeedsReboot = $true
        }
    }
    else {
        Add-Action -Category 'Memory' -Name 'Memory compression' -Status 'Skipped' -Detail 'Kept enabled unless Full profile and RAM >= 32 GB'
    }

    if ($SetManualPageFile) {
        Invoke-Action -Category 'Memory' -Name 'Set fixed page file based on RAM' -ScriptBlock {
            $ramGb = [double]$Detection.TotalRamGb
            $sizeMb = if ($ramGb -ge 32) { 2048 } elseif ($ramGb -ge 16) { 8192 } else { 16384 }
            $computer = Get-CimInstance Win32_ComputerSystem
            $computer.AutomaticManagedPagefile = $false
            Set-CimInstance -InputObject $computer | Out-Null

            $existing = Get-CimInstance Win32_PageFileSetting -ErrorAction SilentlyContinue | Select-Object -First 1
            if (-not $existing) {
                $existing = New-CimInstance -ClassName Win32_PageFileSetting -Property @{ Name = "$env:SystemDrive\pagefile.sys" }
            }
            $existing.InitialSize = $sizeMb
            $existing.MaximumSize = $sizeMb
            Set-CimInstance -InputObject $existing | Out-Null
            $Script:NeedsReboot = $true
        }
    }
    else {
        Add-Recommendation 'Pagefile was left system-managed. Use -SetManualPageFile to apply the guide sizing rule automatically.'
    }
}

function Get-DnsServersForProvider {
    param([string]$Provider)
    switch ($Provider) {
        'Cloudflare' { return @('1.1.1.1', '1.0.0.1') }
        'Google' { return @('8.8.8.8', '8.8.4.4') }
        'Quad9' { return @('9.9.9.9', '149.112.112.112') }
        'AdGuard' { return @('94.140.14.14', '94.140.15.15') }
        default { return @() }
    }
}

function Apply-NetworkTweaks {
    param([object]$Detection)

    $effectiveDnsProvider = $DnsProvider
    if ($Preset -eq 'Full' -and $effectiveDnsProvider -eq 'Keep') {
        $effectiveDnsProvider = 'Cloudflare'
    }

    $activeAdapters = @(Get-ActiveNetworkAdapters)
    if ($effectiveDnsProvider -ne 'Keep') {
        $servers = Get-DnsServersForProvider -Provider $effectiveDnsProvider
        foreach ($adapter in $activeAdapters) {
            Invoke-Action -Category 'Network' -Name "Set $effectiveDnsProvider DNS on $($adapter.Name)" -ScriptBlock {
                Set-DnsClientServerAddress -InterfaceIndex $adapter.ifIndex -ServerAddresses $servers -ErrorAction Stop
            }
        }

        Invoke-Action -Category 'Network' -Name 'Flush DNS cache' -ScriptBlock {
            Clear-DnsClientCache
            ipconfig /flushdns | Out-Host
        }
    }
    else {
        Add-Action -Category 'Network' -Name 'DNS provider' -Status 'Skipped' -Detail 'Kept current DNS'
    }

    if ($Preset -eq 'Full') {
        foreach ($adapter in $activeAdapters) {
            $guid = [string]$adapter.InterfaceGuid
            Invoke-Action -Category 'Network' -Name "Disable Nagle algorithm on $($adapter.Name)" -ScriptBlock {
                $path = "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{$guid}"
                if (-not (Test-Path -Path $path)) {
                    $path = "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\$guid"
                }
                Set-RegDword -Path $path -Name 'TcpAckFrequency' -Value 1
                Set-RegDword -Path $path -Name 'TCPNoDelay' -Value 1
            }

            $advancedSettings = @(
                @{ Name = 'Energy Efficient Ethernet'; Value = 'Disabled' },
                @{ Name = 'Green Ethernet'; Value = 'Disabled' },
                @{ Name = 'Power Saving Mode'; Value = 'Disabled' },
                @{ Name = 'Interrupt Moderation'; Value = 'Disabled' },
                @{ Name = 'Flow Control'; Value = 'Disabled' }
            )

            foreach ($setting in $advancedSettings) {
                $property = Get-NetAdapterAdvancedProperty -Name $adapter.Name -DisplayName $setting.Name -ErrorAction SilentlyContinue
                if ($property) {
                    Invoke-Action -Category 'Network' -Name "$($adapter.Name): $($setting.Name)" -Detail "Set to $($setting.Value)" -ScriptBlock {
                        Set-NetAdapterAdvancedProperty -Name $adapter.Name -DisplayName $setting.Name -DisplayValue $setting.Value -NoRestart -ErrorAction Stop
                    }
                }
            }
        }

        Invoke-Action -Category 'Network' -Name 'QoS reservable bandwidth policy' -ScriptBlock {
            Set-RegDword -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Psched' -Name 'NonBestEffortLimit' -Value 0
        }
    }
}

function Apply-CpuTweaks {
    Invoke-Action -Category 'CPU' -Name 'Disable core parking on AC power for current plan' -ScriptBlock {
        powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR CPMINCORES 100 | Out-Null
        powercfg /setactive SCHEME_CURRENT | Out-Null
    }

    if ($EnableBcdLatencyTweaks) {
        Invoke-Action -Category 'CPU' -Name 'BCDEdit latency tweaks' -Detail 'Boot configuration changed; reboot required' -ScriptBlock {
            bcdedit /deletevalue useplatformclock | Out-Host
            bcdedit /set useplatformtick yes | Out-Host
            bcdedit /set disabledynamictick yes | Out-Host
            $Script:NeedsReboot = $true
        }
    }
    else {
        Add-Recommendation 'BCDEdit HPET/dynamic tick changes were not applied. Use -EnableBcdLatencyTweaks only if you are intentionally testing latency.'
    }
}

function Add-TelemetryHostsBlock {
    if (-not $BlockTelemetryHosts) {
        Add-Recommendation 'Hosts-file telemetry blocking was not applied because it can interfere with Microsoft services. Use -BlockTelemetryHosts if desired.'
        return
    }

    Invoke-Action -Category 'Hosts' -Name 'Add Microsoft telemetry host blocks' -ScriptBlock {
        $hostsPath = Join-Path $env:SystemRoot 'System32\drivers\etc\hosts'
        $existing = Get-Content -LiteralPath $hostsPath -ErrorAction SilentlyContinue
        $linesToAdd = New-Object System.Collections.Generic.List[string]
        $linesToAdd.Add('')
        $linesToAdd.Add("# Win11 optimizer telemetry blocks added $Script:SessionId")
        foreach ($hostName in $Script:TelemetryHosts) {
            if (-not ($existing -match "(^|\s)$([regex]::Escape($hostName))(\s|$)")) {
                $linesToAdd.Add("0.0.0.0 $hostName")
            }
        }

        if ($linesToAdd.Count -gt 2) {
            Add-Content -LiteralPath $hostsPath -Value $linesToAdd
        }
    }
}

function Install-RecommendedTools {
    if ($SkipExternalTools -or (-not $InstallTools -and $Preset -ne 'Full')) {
        Add-Action -Category 'Tools' -Name 'Recommended external tools' -Status 'Skipped' -Detail 'Use Full profile or -InstallTools'
        return
    }

    if (-not (Test-CommandExists 'winget')) {
        Add-Action -Category 'Tools' -Name 'winget' -Status 'Skipped' -Detail 'winget is not available'
        Add-Recommendation 'Install App Installer from Microsoft Store to enable winget-based tool installation.'
        return
    }

    foreach ($tool in $Script:RecommendedTools) {
        Invoke-Action -Category 'Tools' -Name "Install $($tool.Name)" -Detail $tool.Reason -ScriptBlock {
            Install-WingetPackage -Tool $tool
        }
    }

    Add-Recommendation 'Chris Titus WinUtil and Sophia Script are not piped into PowerShell automatically. They are powerful remote scripts and should be launched deliberately after reviewing their upstream projects.'
}

function Invoke-HealthChecks {
    if (-not $RunHealthChecks -and $Preset -ne 'Full') {
        Add-Action -Category 'Health' -Name 'DISM RestoreHealth and SFC' -Status 'Skipped' -Detail 'Use Full profile or -RunHealthChecks'
        return
    }

    Invoke-Action -Category 'Health' -Name 'DISM RestoreHealth' -ScriptBlock {
        Invoke-NativeCommand -FilePath 'DISM.exe' -Arguments @('/Online', '/Cleanup-Image', '/RestoreHealth')
    }

    Invoke-Action -Category 'Health' -Name 'SFC scannow' -ScriptBlock {
        Invoke-NativeCommand -FilePath 'sfc.exe' -Arguments @('/scannow')
    }
}

function Invoke-WindowsUpdatePass {
    if (-not $InstallWindowsUpdates) {
        Add-Action -Category 'Updates' -Name 'Windows Update install pass' -Status 'Skipped' -Detail 'Not confirmed in startup options'
        return
    }

    Invoke-Action -Category 'Updates' -Name 'Install PSWindowsUpdate module' -ScriptBlock {
        if (-not (Get-PackageProvider -Name NuGet -ErrorAction SilentlyContinue)) {
            Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force -Scope AllUsers | Out-Null
        }
        Install-Module -Name PSWindowsUpdate -Force -AllowClobber -Scope AllUsers
        Import-Module PSWindowsUpdate -Force
    }

    Invoke-Action -Category 'Updates' -Name 'Install available Windows updates' -Detail 'Reboot is deferred until the optimizer finishes' -ScriptBlock {
        Import-Module PSWindowsUpdate -Force
        Get-WindowsUpdate -AcceptAll -Install -IgnoreReboot -Verbose
        $Script:NeedsReboot = $true
    }
}

function Invoke-Benchmarks {
    if (-not $RunBenchmarks) {
        Add-Action -Category 'Benchmark' -Name 'WinSAT disk/formal' -Status 'Skipped' -Detail 'Use -RunBenchmarks'
        return
    }

    Invoke-Action -Category 'Benchmark' -Name 'WinSAT disk read' -ScriptBlock {
        Invoke-NativeCommand -FilePath 'winsat.exe' -Arguments @('disk', '-seq', '-read', '-drive', 'c')
    }

    Invoke-Action -Category 'Benchmark' -Name 'WinSAT formal' -ScriptBlock {
        Invoke-NativeCommand -FilePath 'winsat.exe' -Arguments @('formal')
    }
}

function Format-Value {
    param($Value)
    if ($null -eq $Value) { return '' }
    if ($Value -is [bool]) { return [string]$Value }
    if ($Value -is [array]) { return ($Value -join ', ') }
    return [string]$Value
}

function Escape-MarkdownCell {
    param($Value)
    $text = Format-Value $Value
    return ($text -replace '\|', '\|' -replace "`r?`n", '<br>')
}

function Add-MetricRow {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [string]$Metric,
        $Before,
        $After
    )

    $Lines.Add(('| {0} | {1} | {2} |' -f (Escape-MarkdownCell $Metric), (Escape-MarkdownCell $Before), (Escape-MarkdownCell $After))) | Out-Null
}

function New-MarkdownReport {
    param(
        [object]$Detection,
        [object]$Before,
        [object]$After
    )

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('# Windows 11 Optimization Report') | Out-Null
    $lines.Add('') | Out-Null
    $lines.Add(('- Run ID: `{0}`' -f $Script:SessionId)) | Out-Null
    $lines.Add(('- Preset: `{0}`' -f $Preset)) | Out-Null
    $lines.Add(('- Started: `{0}`' -f $Before.Timestamp)) | Out-Null
    $lines.Add(('- Finished: `{0}`' -f $After.Timestamp)) | Out-Null
    $lines.Add(('- Log: `{0}`' -f $Script:LogPath)) | Out-Null
    $lines.Add(('- Reboot needed: `{0}`' -f $Script:NeedsReboot)) | Out-Null
    $lines.Add('') | Out-Null

    $lines.Add('## Auto-detected') | Out-Null
    $lines.Add('') | Out-Null
    $lines.Add('| Item | Value |') | Out-Null
    $lines.Add('|---|---|') | Out-Null
    $lines.Add("| Windows | $(Escape-MarkdownCell "$($Detection.WindowsCaption) $($Detection.WindowsVersion) build $($Detection.WindowsBuild)") |") | Out-Null
    $lines.Add("| Device | $(Escape-MarkdownCell "$($Detection.Manufacturer) $($Detection.Model)") |") | Out-Null
    $lines.Add("| RAM | $(Escape-MarkdownCell "$($Detection.TotalRamGb) GB") |") | Out-Null
    $lines.Add("| Laptop / AC power | $(Escape-MarkdownCell "$($Detection.IsLaptop) / $($Detection.OnAcPower)") |") | Out-Null
    $lines.Add("| OS disk | $(Escape-MarkdownCell "$($Detection.OsDisk.Friendly), $($Detection.OsDisk.BusType), $($Detection.OsDisk.MediaType), SSD=$($Detection.OsDisk.IsSsd)") |") | Out-Null
    $lines.Add("| Everything installed | $(Escape-MarkdownCell $Detection.EverythingInstalled) |") | Out-Null
    $lines.Add("| Physical printers | $(Escape-MarkdownCell $Detection.PhysicalPrinters) |") | Out-Null
    $lines.Add("| BitLocker in use | $(Escape-MarkdownCell $Detection.BitLockerInUse) |") | Out-Null
    $lines.Add("| Bluetooth / biometrics | $(Escape-MarkdownCell "$($Detection.BluetoothPresent) / $($Detection.BiometricPresent)") |") | Out-Null
    $lines.Add("| Active network adapters | $(Escape-MarkdownCell (($Detection.ActiveAdapters | ForEach-Object { $_.Name }) -join ', ')) |") | Out-Null
    $lines.Add('') | Out-Null

    $lines.Add('## Before / After') | Out-Null
    $lines.Add('') | Out-Null
    $lines.Add('| Metric | Before | After |') | Out-Null
    $lines.Add('|---|---:|---:|') | Out-Null
    Add-MetricRow -Lines $lines -Metric 'Process count' -Before $Before.ProcessCount -After $After.ProcessCount
    Add-MetricRow -Lines $lines -Metric 'Startup entries' -Before $Before.Startup.Count -After $After.Startup.Count
    Add-MetricRow -Lines $lines -Metric 'Free RAM (GB)' -Before $Before.Memory.FreeGb -After $After.Memory.FreeGb
    Add-MetricRow -Lines $lines -Metric 'Used RAM (GB)' -Before $Before.Memory.UsedGb -After $After.Memory.UsedGb
    Add-MetricRow -Lines $lines -Metric 'C: free disk (GB)' -Before $Before.Disk.FreeGb -After $After.Disk.FreeGb
    Add-MetricRow -Lines $lines -Metric 'Target services running' -Before $Before.Services.RunningCount -After $After.Services.RunningCount
    Add-MetricRow -Lines $lines -Metric 'Target services disabled' -Before $Before.Services.DisabledCount -After $After.Services.DisabledCount
    Add-MetricRow -Lines $lines -Metric 'Installed bloat AppX packages' -Before $Before.AppxBloat.InstalledBloatPackages -After $After.AppxBloat.InstalledBloatPackages
    Add-MetricRow -Lines $lines -Metric 'Provisioned bloat AppX packages' -Before $Before.AppxBloat.ProvisionedBloatPackages -After $After.AppxBloat.ProvisionedBloatPackages
    Add-MetricRow -Lines $lines -Metric 'Telemetry scheduled tasks enabled' -Before $Before.Tasks.TelemetryTasksEnabled -After $After.Tasks.TelemetryTasksEnabled
    $lines.Add('') | Out-Null

    $lines.Add('## Actions') | Out-Null
    $lines.Add('') | Out-Null
    $lines.Add('| Time | Category | Action | Status | Detail |') | Out-Null
    $lines.Add('|---|---|---|---|---|') | Out-Null
    foreach ($action in $Script:Actions) {
        $lines.Add(('| {0} | {1} | {2} | {3} | {4} |' -f
            (Escape-MarkdownCell $action.Time),
            (Escape-MarkdownCell $action.Category),
            (Escape-MarkdownCell $action.Name),
            (Escape-MarkdownCell $action.Status),
            (Escape-MarkdownCell $action.Detail))) | Out-Null
    }
    $lines.Add('') | Out-Null

    if ($Script:Recommendations.Count -gt 0) {
        $lines.Add('## Notes And Follow-ups') | Out-Null
        $lines.Add('') | Out-Null
        foreach ($item in ($Script:Recommendations | Select-Object -Unique)) {
            $lines.Add("- $item") | Out-Null
        }
        $lines.Add('') | Out-Null
    }

    $lines.Add('## External Tools') | Out-Null
    $lines.Add('') | Out-Null
    $lines.Add('The Full profile attempts winget installs for Everything, Autoruns, RAMMap, WizTree, WinDirStat, BCUninstaller, Process Lasso, LatencyMon, CrystalDiskMark, DriverStore Explorer, O&O ShutUp10++, and Snappy Driver Installer Origin when winget is available.') | Out-Null
    $lines.Add('Remote PowerShell launchers such as WinUtil and Sophia Script are intentionally not auto-executed by this script.') | Out-Null

    Set-Content -LiteralPath $Script:MarkdownReportPath -Value $lines -Encoding UTF8
}

function New-JsonReport {
    param(
        [object]$Detection,
        [object]$Before,
        [object]$After
    )

    $actionArray = @($Script:Actions.ToArray())
    $recommendationArray = @($Script:Recommendations.ToArray() | Select-Object -Unique)

    $report = [pscustomobject]@{
        RunId           = $Script:SessionId
        Preset          = $Preset
        NeedsReboot     = $Script:NeedsReboot
        Detection       = $Detection
        Before          = $Before
        After           = $After
        Actions         = $actionArray
        Recommendations = $recommendationArray
        LogPath         = $Script:LogPath
        MarkdownPath    = $Script:MarkdownReportPath
    }

    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $Script:JsonReportPath -Encoding UTF8
}

function Show-CompletionSummary {
    param(
        [object]$Before,
        [object]$After
    )

    Write-Host ''
    Write-Host 'Optimization summary' -ForegroundColor Cyan
    Write-Host ('  Preset: {0}' -f $Preset)
    Write-Host ('  Process count: {0} -> {1}' -f $Before.ProcessCount, $After.ProcessCount)
    Write-Host ('  Free RAM: {0} GB -> {1} GB' -f $Before.Memory.FreeGb, $After.Memory.FreeGb)
    Write-Host ('  C: free disk: {0} GB -> {1} GB' -f $Before.Disk.FreeGb, $After.Disk.FreeGb)
    Write-Host ('  Target services running: {0} -> {1}' -f $Before.Services.RunningCount, $After.Services.RunningCount)
    Write-Host ('  Bloat AppX installed/provisioned: {0}/{1} -> {2}/{3}' -f $Before.AppxBloat.InstalledBloatPackages, $Before.AppxBloat.ProvisionedBloatPackages, $After.AppxBloat.InstalledBloatPackages, $After.AppxBloat.ProvisionedBloatPackages)
    Write-Host ('  Report: {0}' -f $Script:MarkdownReportPath)
    Write-Host ('  JSON:   {0}' -f $Script:JsonReportPath)
    Write-Host ('  Log:    {0}' -f $Script:LogPath)

    if ($Script:NeedsReboot) {
        Write-Host ''
        Write-Host 'A reboot is recommended to finish power, service, registry, or boot-time changes.' -ForegroundColor Yellow
        if ($AutoReboot -and $Preset -ne 'ReportOnly') {
            Write-Host 'Auto-reboot was confirmed at startup. Scheduling reboot in 30 seconds...' -ForegroundColor Yellow
            shutdown.exe /r /t 30 /c "Windows 11 optimizer finished and needs a reboot." | Out-Null
            return
        }
        if (-not $NoRebootPrompt -and $Preset -ne 'ReportOnly' -and -not $AssumeYes) {
            $answer = Read-Host 'Reboot now? [y/N]'
            if ($answer -match '^(y|yes)$') {
                Restart-Computer
            }
        }
    }
}

function Invoke-Win11Optimization {
    Select-RunProfile

    if ($Script:StartupCancelled) {
        Write-Host 'Optimization cancelled before changes were made.' -ForegroundColor Yellow
        return
    }

    if ($Preset -eq 'Full') {
        if (-not $Script:StartupUiShown -and $DnsProvider -eq 'Keep') { $script:DnsProvider = 'Cloudflare' }
        if (-not $SkipExternalTools) { $script:InstallTools = $true }
        if (-not $Script:StartupUiShown -and -not $Script:OriginalBoundParameters.ContainsKey('RunHealthChecks')) { $script:RunHealthChecks = $true }
    }

    Initialize-Run
    try {
        Write-Host ''
        Write-Host "Windows 11 optimizer starting with profile: $Preset" -ForegroundColor Cyan
        Write-Host "Reports will be written to: $Script:ReportRoot" -ForegroundColor Cyan
        Write-Host ''
        Add-Action -Category 'Preflight' -Name 'Startup options' -Status 'Detected' -Detail ("UI={0}; DNS={1}; tools={2}; updates={3}; autoReboot={4}" -f $Script:StartupUiShown, $DnsProvider, [bool]$InstallTools, [bool]$InstallWindowsUpdates, [bool]$AutoReboot)

        $detection = Get-DetectionState
        $before = Get-SystemSnapshot -Label 'Before'

        New-SystemRestorePoint
        Apply-BuiltInTweaks -Detection $detection
        Apply-PrivacyTweaks
        Install-RecommendedTools
        Update-DetectionAfterToolInstall -Detection $detection
        Apply-ServiceTweaks -Detection $detection
        Apply-ScheduledTaskTweaks
        Remove-BloatAppx
        Clear-TempFiles
        Invoke-DismCleanup
        Set-TrimEnabled
        Apply-MemoryTweaks -Detection $detection
        Apply-NetworkTweaks -Detection $detection
        Apply-CpuTweaks
        Add-TelemetryHostsBlock
        Invoke-HealthChecks
        Invoke-WindowsUpdatePass
        Invoke-Benchmarks

        $after = Get-SystemSnapshot -Label 'After'
        New-MarkdownReport -Detection $detection -Before $before -After $after
        New-JsonReport -Detection $detection -Before $before -After $after
        Show-CompletionSummary -Before $before -After $after
    }
    finally {
        Stop-Run
    }
}

Invoke-Win11Optimization
