#requires -Version 5.1
<# Generate the shipped, data-only catalog from reviewed research and explicit routes.
   It does not execute any catalog action or write Windows settings. #>
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$actions=New-Object System.Collections.Generic.List[object]
function Add-Preference($id,$title,$category,$summary,$tradeoff,$uri,$source,$profiles) {
    $actions.Add([ordered]@{ id=$id; title=$title; category=$category; summary=$summary; benefit='Current-user preference'; tradeoff=$tradeoff; mode='Preference'; settingsUri=$uri; sourceUrl=$source; profiles=@($profiles) })
}
$explorerSource='https://support.microsoft.com/en-us/windows/experience/fileexplorer/file-explorer-in-windows'
$taskbarSource='https://support.microsoft.com/en-us/windows/experience/personalization/customize-the-taskbar-in-windows'
Add-Preference 'explorer.extensions' 'Show file extensions' 'Windows preferences' 'Make file types visible in Explorer. Captures this account''s previous configuration before changing it.' 'Names become longer. Configuration is verified; open a new Explorer window or sign out to check the visible result.' $null $explorerSource @('Everyday')
Add-Preference 'explorer.taskbar-left' 'Align the taskbar to the left' 'Windows preferences' 'Move taskbar app buttons to the left for this account.' 'Changes your navigation habit. Windows may need a shell refresh or sign-out before the visual result appears.' 'ms-settings:taskbar' $taskbarSource @('Everyday')
Add-Preference 'explorer.task-view' 'Hide the Task View button' 'Windows preferences' 'Hide the taskbar shortcut while retaining virtual desktops and keyboard access.' 'Use Win+Tab to open Task View. A later shell refresh may be necessary.' 'ms-settings:taskbar' $taskbarSource @('Minimal Distraction')
Add-Preference 'appearance.transparency' 'Turn off transparency effects' 'Windows preferences' 'Use more opaque Windows surfaces without changing animations or accessibility settings.' 'An appearance preference, with no guaranteed performance gain. Verify the visible effect after sign-out if necessary.' 'ms-settings:easeofaccess-visualeffects' 'https://support.microsoft.com/en-us/accessibility/windows/make-it-easier-to-focus-on-tasks' @('Minimal Distraction')
Add-Preference 'privacy.suggestions' 'Reduce suggested content in Settings' 'Windows preferences' 'Set this account''s Settings suggestion preference. Other advertising and notification choices remain independent.' 'Build-dependent preference. Reading the saved value does not prove all future suggestions are suppressed.' 'ms-settings:privacy-general' 'https://support.microsoft.com/en-us/windows/privacy/general-privacy-settings-in-windows' @('Privacy','Minimal Distraction')
Add-Preference 'privacy.advertising' 'Turn off the advertising ID preference' 'Windows preferences' 'Disable this account''s advertising ID preference for apps that respect it.' 'Does not block browser tracking or remove all advertising. Undo restores configuration, not an old identifier.' 'ms-settings:privacy-general' 'https://support.microsoft.com/en-us/windows/privacy/general-privacy-settings-in-windows' @('Privacy')

$qRoutes=@{
1='ms-settings:developers';2='ms-settings:developers';3='ms-settings:developers';4='ms-settings:developers';5='ms-settings:developers';6='ms-settings:search';7='ms-settings:search';8='ms-settings:defaultapps';9='ms-settings:search';10='ms-settings:taskbar';11='ms-settings:taskbar';12='ms-settings:personalization-start';13='ms-settings:notifications';14='ms-settings:notifications';15='ms-settings:notifications';16='ms-settings:quietmomentshome';17='ms-settings:quiethours';18='ms-settings:multitasking';19='ms-settings:display';20='ms-settings:multitasking';21='ms-settings:clipboard';22='ms-settings:clipboard';23='ms-settings:clipboard';24='ms-settings:easeofaccess-display';25='ms-settings:easeofaccess-visualeffects';26='ms-settings:nightlight';27='ms-settings:mousetouchpad';28='ms-settings:sound';30='ms-settings:keyboard';31='ms-settings:privacy-general';32='ms-settings:privacy-feedback';33='ms-settings:privacy-appdiagnostics';34='ms-settings:privacy-webcam';35='ms-settings:privacy-location';37='ms-settings:backup';38='ms-settings:privacy';39='ms-settings:optionalfeatures';40='ms-settings:appsfeatures';41='ms-settings:storagesense';42='ms-settings:storagepolicies';43='ms-settings:storagesense';44='ms-settings:startupapps';45='ms-settings:windowsupdate-options';46='ms-settings:windowsupdate';47='ms-settings:recovery';48='ms-settings:appsfeatures';49='ms-settings:storagesense'
}
$pRoutes=@{} # Performance cards link to their exact evidence; no invented one-click remediation.
function Clean-Text([string]$text) { return ($text -replace '\[([^\]]+)\]\[[^\]]+\]','$1' -replace '\[([^\]]+)\]\([^)]+\)','$1' -replace '\*\*','' -replace '`','').Trim() }
foreach($set in @(@{File='WINDOWS-PERFORMANCE.md';Prefix='P';Category='Performance'},@{File='WINDOWS-QOL.md';Prefix='Q';Category='Windows preferences'})) {
    $body=Get-Content -LiteralPath (Join-Path $projectRoot "docs\research\$($set.File)") -Raw
    $references=@{}
    foreach($match in [regex]::Matches($body,'(?m)^\[([^\]]+)\]:\s+(https://\S+)')) { $references[$match.Groups[1].Value]=$match.Groups[2].Value }
    foreach($line in ($body -split '\r?\n')) {
        if($line -notmatch '^\| (P|Q)-(\d+)\s+([^|]+)\|') {continue}
        $prefix=$Matches[1]; $number=[int]$Matches[2]; $title=Clean-Text $Matches[3]
        $cells=$line -split '\|'; if($cells.Length -lt 6){throw "Malformed catalog row: $line"}
        $category=$set.Category
        if($prefix -eq 'Q' -and $number -in @(9,29)){$category='Tools'}
        if($prefix -eq 'Q' -and $number -in @(44,48)){$category='Apps & startup'}
        if($prefix -eq 'Q' -and $number -in @(37,41,42,43,45,46,47,49)){$category='Maintenance'}
        $source='https://support.microsoft.com/en-us/windows'
        $reference=[regex]::Match($line,'\[[^\]]+\]\[([^\]]+)\]')
        if($reference.Success -and $references.ContainsKey($reference.Groups[1].Value)){$source=$references[$reference.Groups[1].Value]}
        $uri=$null
        if($prefix -eq 'Q' -and $qRoutes.ContainsKey($number)){$uri=$qRoutes[$number]}
        $profiles=if($prefix -eq 'P'){@('Gaming','Laptop')}elseif($number -ge 31 -and $number -le 40){@('Privacy')}else{@('Everyday','Minimal Distraction')}
        $summary=Clean-Text $cells[3]
        $tradeoff=Clean-Text $cells[4]
        $actions.Add([ordered]@{id=('guide.'+$prefix.ToLowerInvariant()+$number.ToString('00'));title=$title;category=$category;summary=$summary;benefit=(Clean-Text $cells[2]);tradeoff=$tradeoff;mode='Guided';settingsUri=$uri;sourceUrl=$source;profiles=@($profiles)})
    }
}
$tools=@(
@('powertoys','Microsoft PowerToys','Window layouts, quick file previews and selected productivity utilities.','https://learn.microsoft.com/en-us/windows/powertoys/'),
@('autoruns','Autoruns','Inspect startup items with Microsoft Sysinternals; changes require your review.','https://learn.microsoft.com/en-us/sysinternals/downloads/autoruns'),
@('process-explorer','Process Explorer','Investigate process resource use and ownership.','https://learn.microsoft.com/en-us/sysinternals/downloads/process-explorer'),
@('rammap','RAMMap','Understand memory use; routine cache flushing is not an optimization.','https://learn.microsoft.com/en-us/sysinternals/downloads/rammap'),
@('everything','Everything','Fast filename search that complements Windows Search.','https://www.voidtools.com/'),
@('windirstat','WinDirStat','Find large folders before choosing what to remove.','https://windirstat.net/'),
@('presentmon','PresentMon','Measure frame presentation for a repeatable game workload.','https://github.com/GameTechDev/PresentMon'),
@('capframex','CapFrameX','Analyze controlled frame-time captures.','https://github.com/CXWorld/CapFrameX'),
@('latencymon','LatencyMon','Investigate a reproducible audio or DPC latency problem.','https://www.resplendence.com/latencymon'),
@('bcu','Bulk Crap Uninstaller','Optional advanced uninstall manager; review every deletion.','https://github.com/BCUninstaller/Bulk-Crap-Uninstaller'),
@('win11debloat','Win11Debloat','Reviewed candidate for a future restricted adapter. This build opens upstream documentation only.','https://github.com/Raphire/Win11Debloat'),
@('wpt','Windows Performance Toolkit','Advanced recordings and analysis for startup, CPU and storage bottlenecks.','https://learn.microsoft.com/en-us/windows-hardware/test/wpt/')
)
foreach($tool in $tools){$actions.Add([ordered]@{id=('tool.'+$tool[0]);title=$tool[1];category='Tools';summary=$tool[2];benefit='Optional specialist tool';tradeoff='Opens the official publisher page. Nothing is downloaded, installed or run automatically. Review publisher terms and background overhead.';mode='External';settingsUri=$null;sourceUrl=$tool[3];profiles=@('Everyday','Gaming')})}
foreach($action in $actions) { if($action.id -in @('guide.q01','guide.q02','guide.q03','guide.q04','guide.q05','guide.q30','guide.q37','guide.q38','guide.q43')) { $action.settingsUri=$null } }
($actions | Where-Object { $_.id -eq 'guide.q16' }).settingsUri='ms-settings:quiethours'
if(@($actions | Group-Object -Property { $_['id'] } | Where-Object Count -gt 1).Count -gt 0){throw 'Duplicate catalog IDs'}
[ordered]@{version='0.1.0';actions=@($actions.ToArray())} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $projectRoot 'catalog\actions.json') -Encoding utf8
Write-Output "Generated $($actions.Count) reviewed catalog entries."
