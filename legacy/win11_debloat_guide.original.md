# Windows 11 Expert Debloat & Speed Optimization Guide
> For tech-savvy users. Run PowerShell as Administrator unless otherwise noted.

---

## PHASE 0 — Pre-Flight

Before touching anything:

```powershell
# Create a System Restore Point
Checkpoint-Computer -Description "Pre-Debloat" -RestorePointType "MODIFY_SETTINGS"

# Verify it was created
Get-ComputerRestorePoint
```

- **Full image backup**: [Macrium Reflect Free](https://www.macrium.com/reflectfree.aspx) or `wbAdmin` for bare-metal backup
- Note current boot time baseline: `systeminfo | findstr "Boot Time"`
- Note RAM in use at idle before and after
- Run `winver` — confirm build number (22H2 / 23H2 / 24H2 — some tweaks differ by build)

---

## PHASE 1 — Built-in Windows Tweaks (No Tools Required)

### 1.1 Power Plan → Ultimate Performance

```powershell
# Unlock and set Ultimate Performance plan
powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61
# Then set it active via Settings > Power > Power Mode = Best Performance
# OR via command (get the GUID from above output):
powercfg /setactive <GUID>
```

For **laptops**: keep Balanced unless plugged in — Ultimate Performance drains battery.

### 1.2 Disable Fast Startup (causes sleep/hibernate issues & slows full boots paradoxically)

```powershell
powercfg /h off          # Also disables hibernation, frees hiberfil.sys (~RAM size)
# Or keep hibernation but kill fast startup:
# Control Panel > Power Options > Choose what the power buttons do > uncheck Fast Startup
```

### 1.3 Visual Effects — Pure Performance

```
Win+R → sysdm.cpl → Advanced → Performance Settings
→ "Adjust for best performance" → then re-enable:
  ✓ Smooth edges of screen fonts
  ✓ Show thumbnails instead of icons  (optional)
```

Or via PowerShell:
```powershell
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects" -Name "VisualFXSetting" -Value 2
```

### 1.4 Startup Programs

```powershell
# View all startup entries with their impact
Get-CimInstance Win32_StartupCommand | Select-Object Name, Command, Location | Format-Table -AutoSize
```

Disable in **Task Manager → Startup Apps** — or use Autoruns (Phase 4) for deeper control.

### 1.5 Disable Delivery Optimization (P2P Windows Update sharing)

```powershell
Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization" -Name "DODownloadMode" -Value 0
```

Or: Settings → Windows Update → Advanced Options → Delivery Optimization → Off

### 1.6 Disable Search Indexing (if you use Everything.exe instead)

```powershell
Stop-Service -Name "WSearch" -Force
Set-Service -Name "WSearch" -StartupType Disabled
```

### 1.7 Disable Xbox Game Bar / GameDVR (even if you game — OBS is better)

```powershell
# Game Bar
Set-ItemProperty -Path "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR" -Name "AppCaptureEnabled" -Value 0
Set-ItemProperty -Path "HKCU:\System\GameConfigStore" -Name "GameDVR_Enabled" -Value 0

# Disable via Group Policy equivalent
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\GameDVR" /v "AllowGameDVR" /t REG_DWORD /d 0 /f
```

### 1.8 Menu & Response Time Tweaks

```powershell
# Menu animation delay: 0ms (default 400ms)
Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "MenuShowDelay" -Value "0"

# Mouse hover delay
Set-ItemProperty -Path "HKCU:\Control Panel\Mouse" -Name "MouseHoverTime" -Value "0"

# Disable low disk space checks
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer" -Name "NoLowDiskSpaceChecks" -Value 1
```

### 1.9 Disable Connected Standby / Modern Standby (S0 sleep — causes background drain)

```powershell
# Check current sleep states
powercfg /a

# Disable S0 Modern Standby (forces back to S3 sleep — better for desktops)
reg add "HKLM\System\CurrentControlSet\Control\Power" /v "PlatformAoAcOverride" /t REG_DWORD /d 0 /f
# Reboot required
```

---

## PHASE 2 — Telemetry, Privacy & Bloat (Registry / Group Policy)

### 2.1 Disable Telemetry

```powershell
# Set telemetry to minimum (0 = Security, Enterprise only; 1 = Basic for Pro)
Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection" -Name "AllowTelemetry" -Value 0
Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection" -Name "AllowTelemetry" -Value 0

# Stop and disable the telemetry service
Stop-Service DiagTrack -Force
Set-Service DiagTrack -StartupType Disabled

# Disable WAP push (used for telemetry routing)
Stop-Service dmwappushservice -Force
Set-Service dmwappushservice -StartupType Disabled
```

### 2.2 Disable Cortana

```powershell
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search" /v "AllowCortana" /t REG_DWORD /d 0 /f
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search" /v "AllowCortanaAboveLock" /t REG_DWORD /d 0 /f
```

### 2.3 Disable Advertising ID & Personalized Ads

```powershell
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo" -Name "Enabled" -Value 0
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Privacy" -Name "TailoredExperiencesWithDiagnosticDataEnabled" -Value 0
```

### 2.4 Disable Windows Consumer Features (installs suggested apps silently)

```powershell
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent" /v "DisableWindowsConsumerFeatures" /t REG_DWORD /d 1 /f
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" /v "SystemPaneSuggestionsEnabled" /t REG_DWORD /d 0 /f
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" /v "SilentInstalledAppsEnabled" /t REG_DWORD /d 0 /f
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" /v "SoftLandingEnabled" /t REG_DWORD /d 0 /f
```

### 2.5 Disable Activity History / Timeline

```powershell
Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\System" -Name "EnableActivityFeed" -Value 0
Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\System" -Name "PublishUserActivities" -Value 0
```

### 2.6 Disable Notification / Feedback Requests

```powershell
reg add "HKCU\Software\Microsoft\Siuf\Rules" /v "NumberOfSIUFInPeriod" /t REG_DWORD /d 0 /f
reg add "HKCU\Software\Microsoft\Siuf\Rules" /v "PeriodInNanoSeconds" /t REG_DWORD /d 0 /f
```

---

## PHASE 3 — Services Optimization

Disable these safely on most systems. Use `sc config` or Services MMC (`services.msc`).

```powershell
$services = @(
    "DiagTrack",            # Connected User Experiences and Telemetry
    "dmwappushservice",     # WAP Push Message Routing
    "RetailDemo",           # Retail Demo Service
    "WerSvc",               # Windows Error Reporting
    "WMPNetworkSvc",        # Windows Media Player Network Sharing
    "lfsvc",                # Geolocation Service
    "MapsBroker",           # Downloaded Maps Manager
    "XblAuthManager",       # Xbox Live Auth Manager
    "XblGameSave",          # Xbox Live Game Save
    "XboxGipSvc",           # Xbox Accessory Management
    "XboxNetApiSvc",        # Xbox Live Networking
    "wisvc",                # Windows Insider Service
    "wlidsvc",              # Microsoft Account Sign-In Assistant (careful: breaks MS account login)
    "Fax",                  # Fax
    "PushToInstall",        # Windows PushToInstall
    "SEMgrSvc",             # Payments and NFC/SE Manager
    "SharedAccess"          # Internet Connection Sharing
)

foreach ($svc in $services) {
    Stop-Service -Name $svc -Force -ErrorAction SilentlyContinue
    Set-Service -Name $svc -StartupType Disabled -ErrorAction SilentlyContinue
    Write-Host "Disabled: $svc"
}
```

**Conditional disables (check before disabling):**

| Service | Disable If |
|---|---|
| `WSearch` | You use Everything.exe |
| `SysMain` (Superfetch) | You have an NVMe SSD |
| `Spooler` | No printer connected |
| `BDESVC` | No BitLocker drives |
| `RemoteRegistry` | Never remote into this machine |
| `BluetoothUserService` | No BT peripherals |
| `WbioSrvc` | No fingerprint/Hello biometrics |
| `Themes` | You use Classic theme |

---

## PHASE 4 — Debloat Tools (The Big Guns)

### 4.1 Chris Titus Tech's WinUtil ⭐ (Most Popular, Actively Maintained)

```powershell
# Run in Admin PowerShell:
irm christitus.com/win | iex
```

GUI launches. Recommended selections:
- **Tweaks tab**: Run `Recommended` preset, then customize
- **Programs tab**: Install only what you need (chocolatey-based)
- **Config tab**: Standard / Minimal for W11
- Use **Desktop** preset as baseline, uncheck anything you need

Source: https://github.com/ChrisTitusTech/winutil

---

### 4.2 Sophia Script ⭐ (Most Comprehensive PowerShell-Based)

```powershell
# Download and run (choose correct version for your W11 build)
Invoke-RestMethod -Uri https://raw.githubusercontent.com/farag2/Sophia-Script-for-Windows/master/Scripts/Sophia.ps1 | Invoke-Expression
# Or download release ZIP from GitHub and edit Sophia.ps1 to configure before running
```

Source: https://github.com/farag2/Sophia-Script-for-Windows

**Key advantage**: Granular control via a config file. Every tweak is opt-in with comments. 250+ tweaks. Safest script available — reverts gracefully.

---

### 4.3 O&O ShutUp10++ (GUI — Privacy/Telemetry Focused)

Download: https://www.oo-software.com/en/shutup10

- Free, no install needed (portable)
- **Actions → Apply only recommended settings** as a baseline
- Carefully review each section — some settings break OneDrive, Defender, etc.
- Exports `.cfg` files for reapplication after updates

---

### 4.4 Optimizer by hellzerg

```powershell
# Download from:
# https://github.com/hellzerg/optimizer/releases
```

Covers: startup, privacy, network tweaks, service management, hosts file editor, Windows Defender toggle. Lightweight, no install.

---

### 4.5 Bulk Crap Uninstaller (BCUninstaller) — Uninstall Manager

Download: https://www.bcuninstaller.com/

Far more powerful than Apps & Features:
- Batch uninstall with leftover detection
- Removes Store apps, forced-install apps, orphaned entries
- Quiet mode for automation
- Run: `File → Uninstall` with "Quiet uninstall" + "Remove leftovers" checked

Uninstall these default W11 apps if unused:
```
Clipchamp, Microsoft Teams (personal), News, Weather, Maps, 
Skype, Xbox (all), Solitaire, Sticky Notes, Tips, 
Your Phone (Phone Link), Office Hub, Paint 3D, Mixed Reality Portal,
Feedback Hub, Get Help, Power Automate, People
```

---

### 4.6 Autoruns (Sysinternals) — Startup Deep Dive

```powershell
# Or download from:
# https://docs.microsoft.com/en-us/sysinternals/downloads/autoruns
winget install Microsoft.Sysinternals.Autoruns
```

- Options → Scan Options → Check VirusTotal.com
- Options → Hide Microsoft Entries (focus on 3rd party)
- Disable (uncheck) anything suspicious or unneeded
- Tabs to hit: Logon, Scheduled Tasks, Services, Drivers, Browser Extensions

---

## PHASE 5 — AppX / UWP Bloat Removal

```powershell
# List all installed Store apps
Get-AppxPackage -AllUsers | Select-Object Name, PackageFullName | Sort-Object Name

# Remove specific app (example: 3D Viewer)
Get-AppxPackage *Microsoft.Microsoft3DViewer* | Remove-AppxPackage

# Bulk remove common bloat (customize this list)
$bloat = @(
    "Microsoft.3DBuilder",
    "Microsoft.BingFinance",
    "Microsoft.BingNews",
    "Microsoft.BingSports",
    "Microsoft.BingWeather",
    "Microsoft.GamingApp",
    "Microsoft.GetHelp",
    "Microsoft.Getstarted",
    "Microsoft.Messaging",
    "Microsoft.Microsoft3DViewer",
    "Microsoft.MicrosoftOfficeHub",
    "Microsoft.MicrosoftSolitaireCollection",
    "Microsoft.MicrosoftStickyNotes",
    "Microsoft.MixedReality.Portal",
    "Microsoft.NetworkSpeedTest",
    "Microsoft.News",
    "Microsoft.Office.OneNote",
    "Microsoft.People",
    "Microsoft.Print3D",
    "Microsoft.SkypeApp",
    "Microsoft.Teams",
    "Microsoft.Todos",
    "Microsoft.WindowsAlarms",
    "Microsoft.WindowsFeedbackHub",
    "Microsoft.WindowsMaps",
    "Microsoft.WindowsSoundRecorder",
    "Microsoft.Xbox.TCUI",
    "Microsoft.XboxApp",
    "Microsoft.XboxGameOverlay",
    "Microsoft.XboxGamingOverlay",
    "Microsoft.XboxIdentityProvider",
    "Microsoft.XboxSpeechToTextOverlay",
    "Microsoft.YourPhone",
    "Microsoft.ZuneMusic",
    "Microsoft.ZuneVideo",
    "Clipchamp.Clipchamp",
    "MicrosoftCorporationII.MicrosoftFamily",
    "MicrosoftTeams"
)

foreach ($app in $bloat) {
    Get-AppxPackage -Name $app -AllUsers | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
    Get-AppxProvisionedPackage -Online | Where-Object DisplayName -EQ $app | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue
    Write-Host "Removed: $app"
}
```

> ⚠️ **Do NOT remove**: `Microsoft.WindowsStore`, `Microsoft.DesktopAppInstaller`, `Microsoft.UI.Xaml.*`, `VCLibs` — these break other apps.

---

## PHASE 6 — Disk Cleanup & Storage Optimization

### 6.1 Advanced Disk Cleanup (Component Store)

```powershell
# Run extended Disk Cleanup (all categories including system files)
cleanmgr /sageset:65535
cleanmgr /sagerun:65535

# DISM: Clean WinSxS component store (can free 5–20GB)
DISM /Online /Cleanup-Image /StartComponentCleanup /ResetBase

# Verify Windows image health after
DISM /Online /Cleanup-Image /RestoreHealth
sfc /scannow
```

> ⚠️ `/ResetBase` removes the ability to uninstall Windows Updates. Do this only when stable.

### 6.2 Find What's Eating Your Disk

**WizTree** (fastest — reads MFT directly, ~2s for 1TB):
```
winget install AntibodySoftware.WizTree
```

**WinDirStat** (visual treemap):
```
winget install WinDirStat.WinDirStat
```

Focus cleanup on:
- `C:\Windows\Temp` → delete all
- `%TEMP%` → delete all
- `C:\Users\<user>\AppData\Local\Temp` → delete all
- `C:\$WINDOWS.~BT` → old upgrade files
- `C:\Windows.old` → old Windows install

```powershell
# Nuke temp files
Remove-Item -Path "C:\Windows\Temp\*" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$env:TEMP\*" -Recurse -Force -ErrorAction SilentlyContinue
```

### 6.3 SSD-Specific Optimization

```powershell
# Verify TRIM is enabled (should show 0 = enabled)
fsutil behavior query DisableDeleteNotify

# Enable TRIM if disabled
fsutil behavior set DisableDeleteNotify 0

# Disable scheduled defrag for SSDs (Windows should auto-detect, but verify)
# Task Scheduler → Microsoft → Windows → Defrag → ScheduledDefrag
# Ensure your SSD drive is NOT in the defrag list

# Align partition check (use diskpart or manufacturer tool)
# msinfo32 → Components → Storage → Disks → check "Partition Starting Offset" divisible by 4096
```

---

## PHASE 7 — Memory & Pagefile Optimization

### 7.1 Pagefile (Virtual Memory)

**Rule of thumb**:
- ≥32GB RAM: Set pagefile to 2GB system-managed minimum, or disable entirely (risky)
- 16GB RAM: 4–8GB fixed pagefile on fastest drive
- 8GB RAM: 8–16GB pagefile, let Windows manage

```powershell
# View current pagefile
Get-CimInstance Win32_PageFileUsage | Select-Object Name, AllocatedBaseSize, CurrentUsage

# To set manually: System Properties → Advanced → Performance → Virtual Memory
# Uncheck "Automatically manage" → Custom size → Set Initial = Maximum (no resizing overhead)
```

### 7.2 Disable Memory Compression (if you have ≥32GB RAM)

```powershell
# Check current state
Get-MMAgent

# Disable memory compression (reduces CPU overhead at cost of more page file use)
Disable-MMAgent -MemoryCompression

# Re-enable if needed
Enable-MMAgent -MemoryCompression
```

### 7.3 Flush Standby Memory (frees cached RAM)

```powershell
# Sysinternals RAMMap: File → Empty → Empty Standby List
winget install Microsoft.Sysinternals.RAMMap

# Or EmptyStandbyList.exe CLI (lightweight, can be scheduled)
# https://wj32.org/wp/software/empty-standby-list/
```

---

## PHASE 8 — Network Stack Optimization

### 8.1 Switch DNS

```powershell
# Set DNS on your primary adapter (replace "Ethernet" with your adapter name)
$adapter = "Ethernet"

# Cloudflare (fastest globally)
Set-DnsClientServerAddress -InterfaceAlias $adapter -ServerAddresses "1.1.1.1","1.0.0.1"

# Or Google
Set-DnsClientServerAddress -InterfaceAlias $adapter -ServerAddresses "8.8.8.8","8.8.4.4"

# Flush DNS cache
Clear-DnsClientCache
ipconfig /flushdns
```

For privacy + adblocking: use **NextDNS** (https://nextdns.io) or **AdGuard DNS**.

### 8.2 Disable Nagle's Algorithm (reduces latency, especially in games/RDP)

```powershell
# Get your adapter's interface GUID
Get-NetAdapter | Select-Object Name, InterfaceGuid

# Apply to target adapter GUID (replace with your GUID)
$guid = "{YOUR-ADAPTER-GUID}"
$path = "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\$guid"
Set-ItemProperty -Path $path -Name "TcpAckFrequency" -Value 1
Set-ItemProperty -Path $path -Name "TCPNoDelay" -Value 1
```

### 8.3 Network Adapter Advanced Settings

Device Manager → Network Adapter → Properties → Advanced:
- **Energy Efficient Ethernet**: Disabled
- **Green Ethernet**: Disabled  
- **Power Saving Mode**: Disabled
- **Interrupt Moderation**: Disabled (reduces latency, increases CPU load slightly)
- **Receive Buffers / Transmit Buffers**: Max values
- **Flow Control**: Disabled
- **Large Send Offload v2**: Enabled (for throughput)

### 8.4 QoS Reserved Bandwidth (Windows reserves 20% by default)

```
Win+R → gpedit.msc
Computer Configuration → Administrative Templates → Network → QoS Packet Scheduler
→ "Limit reservable bandwidth" → Enabled → Bandwidth limit %: 0
```

---

## PHASE 9 — Process & CPU Optimization

### 9.1 Process Lasso (Best CPU Management Tool)

Download: https://bitsum.com/

- **ProBalance**: Automatically lowers priority of runaway processes
- **CPU Affinities**: Pin background processes to specific cores
- **Power Plan Switching**: Auto-switch to performance plan when gaming
- **IdleSaver**: Drop to power-saver when idle
- Enable **SmartTrim** for RAM management

### 9.2 Set Priority for Your Main Apps (manual)

```powershell
# Example: set your game/app to High priority permanently via Lasso
# Or one-time via PowerShell:
$proc = Get-Process -Name "YourApp"
$proc.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::High
```

### 9.3 Disable CPU Core Parking (improves latency for real-time workloads)

```powershell
# For Ultimate Performance plan, parking is already disabled
# For other plans, use powercfg or ParkControl:
# https://bitsum.com/parkcontrol/

# Manual registry approach (change 0 = all cores unparked):
powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR CPMINCORES 100
powercfg /setactive SCHEME_CURRENT
```

---

## PHASE 10 — Windows Update & Driver Hygiene

```powershell
# View installed updates
Get-HotFix | Sort-Object InstalledOn -Descending

# View optional / driver updates available
Get-WindowsUpdate  # Requires PSWindowsUpdate module

# Install PSWindowsUpdate module
Install-Module PSWindowsUpdate -Force
Get-WindowsUpdate -AcceptAll -Install -AutoReboot

# Clean up old driver packages from DriverStore (can be huge)
# Use DriverStore Explorer (RAPR):
winget install "Snappy Driver Installer Origin"
```

**DriverStore Explorer (RAPR)**: https://github.com/lostindark/DriverStoreExplorer
- Removes duplicate / old driver versions safely
- Sort by size — GPU drivers accumulate GBs over time

---

## PHASE 11 — Hosts File & DNS-Level Blocking

```powershell
# Add to C:\Windows\System32\drivers\etc\hosts
# Blocks Microsoft telemetry endpoints at network level

$telemetryHosts = @"
0.0.0.0 telemetry.microsoft.com
0.0.0.0 vortex.data.microsoft.com
0.0.0.0 settings-win.data.microsoft.com
0.0.0.0 watson.telemetry.microsoft.com
0.0.0.0 oca.telemetry.microsoft.com
0.0.0.0 sqm.telemetry.microsoft.com
0.0.0.0 compatexchange.cloudapp.net
0.0.0.0 cs1.wpc.v0cdn.net
0.0.0.0 a-0001.a-msedge.net
0.0.0.0 fe2.update.microsoft.com.akadns.net
"@

Add-Content -Path "C:\Windows\System32\drivers\etc\hosts" -Value $telemetryHosts
```

Or use **StevenBlack Hosts** (comprehensive, maintained):  
https://github.com/StevenBlack/hosts — pick your flavor (ads + telemetry + tracking).

---

## PHASE 12 — Scheduled Tasks Cleanup

```powershell
# View all non-disabled scheduled tasks
Get-ScheduledTask | Where-Object {$_.State -ne "Disabled"} | Select-Object TaskName, TaskPath | Sort-Object TaskPath

# Disable specific telemetry/diagnostic tasks
$tasks = @(
    "\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
    "\Microsoft\Windows\Application Experience\ProgramDataUpdater",
    "\Microsoft\Windows\Autochk\Proxy",
    "\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
    "\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
    "\Microsoft\Windows\Customer Experience Improvement Program\Uploader",
    "\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector",
    "\Microsoft\Windows\Feedback\Siuf\DmClient",
    "\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload",
    "\Microsoft\Windows\Maps\MapsToastTask",
    "\Microsoft\Windows\Maps\MapsUpdateTask",
    "\Microsoft\Windows\Shell\FamilySafetyMonitor",
    "\Microsoft\Windows\Shell\FamilySafetyRefreshTask",
    "\Microsoft\Windows\Windows Error Reporting\QueueReporting"
)

foreach ($task in $tasks) {
    Disable-ScheduledTask -TaskPath (Split-Path $task) -TaskName (Split-Path $task -Leaf) -ErrorAction SilentlyContinue
    Write-Host "Disabled task: $task"
}
```

---

## PHASE 13 — BIOS / UEFI Level (Outside Windows)

These require a reboot into BIOS:

| Setting | Recommendation |
|---|---|
| XMP / DOCP Profile | Enable (runs RAM at rated speed) |
| C-States | C1E only or Disabled for minimum latency |
| Secure Boot | Keep enabled (required for Windows 11 TPM) |
| CSM | Disabled (UEFI-native mode) |
| Resizable BAR / Smart Access Memory | Enable (massive GPU perf gain) |
| Fast Boot | Enable (BIOS-level, different from Windows Fast Startup) |
| Spread Spectrum | Disable for stability |
| Virtualization (VT-x / AMD-V) | Enable only if needed (WSL2, VMs) |
| HPET (High Precision Event Timer) | Disable for gaming (lower latency) |

```powershell
# Disable HPET in Windows (pairs with BIOS setting)
bcdedit /deletevalue useplatformclock
bcdedit /set useplatformtick yes
bcdedit /set disabledynamictick yes
```

---

## PHASE 14 — Optional Nuclear Options

### 14.1 Tiny11 / Custom ISO (Clean Slate)

**Tiny11 Builder**: https://github.com/ntdevlabs/tiny11builder  
Strips Windows 11 ISO to bare minimum before install — removes all provisioned bloat before first boot.

```powershell
# Tiny11 Builder usage (runs against a mounted/offline WIM):
# 1. Mount W11 ISO
# 2. Run tiny11maker.ps1 as admin
# 3. Creates a new lean ISO
```

### 14.2 NTLite (Advanced ISO Customization)

https://www.ntlite.com/ — paid for full features, free for basic

- Remove components at image level (telemetry, Edge, IE, WMP, etc.)
- Integrate drivers and updates into ISO
- Most powerful pre-install customization tool

### 14.3 AtlasOS / ReviOS (Pre-debloated W11 Variants)

- **AtlasOS**: https://atlasos.net — gaming-focused, heavily stripped, open source AME Wizard playbook
- **ReviOS**: https://www.revi.cc — similar approach, maintained

> ⚠️ These are modified Windows builds. Not suitable for enterprise/work machines. No Windows Update by default. Use only if you know the tradeoffs.

---

## PHASE 15 — Verification & Benchmarking

```powershell
# Boot time (measure before/after)
systeminfo | findstr "Boot Time"
# Or: Get-CimInstance Win32_OperatingSystem | Select-Object LastBootUpTime

# Startup processes count
(Get-Process).Count

# RAM usage at idle
(Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1MB

# Disk read speed (rough)
winsat disk -seq -read -drive c

# Full WinSAT assessment
winsat formal
# Results: C:\Windows\Performance\WinSAT\DataStore\
```

**Third-party benchmarks:**
- **CrystalDiskMark**: NVMe/SSD read-write
- **Cinebench R24**: CPU performance
- **3DMark**: GPU (if gaming)
- **LatencyMon**: https://www.resplendence.com/latencymon — detects DPC latency (crucial for audio/gaming)

---

## Quick Reference: Tool Stack Summary

| Tool | Purpose | Link |
|---|---|---|
| WinUtil (CTT) | All-in-one debloat + tweaks | `irm christitus.com/win \| iex` |
| Sophia Script | Deep PS-based config | github.com/farag2/Sophia-Script-for-Windows |
| O&O ShutUp10++ | Privacy/telemetry GUI | oo-software.com/en/shutup10 |
| BCUninstaller | Bulk app removal | bcuninstaller.com |
| Autoruns | Startup deep dive | Sysinternals / winget |
| Process Lasso | CPU management | bitsum.com |
| WizTree | Disk usage (instant) | `winget install AntibodySoftware.WizTree` |
| DriverStore Explorer | Old driver cleanup | github.com/lostindark/DriverStoreExplorer |
| LatencyMon | DPC latency diagnostics | resplendence.com/latencymon |
| RAMMap | Standby memory flush | Sysinternals |
| NTLite | ISO customization | ntlite.com |
| Tiny11 Builder | Lean install ISO | github.com/ntdevlabs/tiny11builder |

---

## Recommended Order for a Fresh Machine

1. Install Windows → **don't sign in with MS account** (use local during OOBE, add later)
2. Run `WinUtil` → apply tweaks + uninstall bloat
3. Run `Sophia Script` → granular privacy/performance config
4. Apply Phase 1–3 tweaks manually (power, registry, services)
5. Run `O&O ShutUp10++` → privacy pass
6. `BCUninstaller` → remove remaining AppX
7. `Autoruns` → clean startup
8. BIOS tweaks (XMP, Resizable BAR, HPET)
9. `DriverStore Explorer` → clean old drivers
10. `DISM /Cleanup-Image /ResetBase` → shrink WinSxS
11. `LatencyMon` → verify no DPC issues
12. Benchmark and compare

---

*Last updated for Windows 11 23H2/24H2. Some GPO paths and service names change between builds — verify in your environment.*
