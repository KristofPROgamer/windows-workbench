# Windows 11 Optimizer Script

This repo turns `win11_debloat_guide.md` into a mostly automatic PowerShell workflow.

The main script is:

```powershell
.\Optimize-Win11.ps1
```

It self-elevates to Administrator, shows a small startup UI for the few choices it cannot safely infer, detects everything else from Windows, applies matching tweaks, then writes before/after reports to `reports\`.

## Quick Start

Open PowerShell in this folder and run:

```powershell
Set-ExecutionPolicy Bypass -Scope Process -Force
.\Optimize-Win11.ps1
```

The default UI starts on `Full`, with external tool installs, AppX cleanup, DISM/SFC health checks, and Cloudflare DNS selected. Riskier items stay unchecked until you explicitly confirm them.

For a no-change dry run:

```powershell
.\Optimize-Win11.ps1 -Preset ReportOnly
```

For a fully unattended full automatic run without the UI:

```powershell
.\Optimize-Win11.ps1 -AssumeYes -NoGui
```

For a parameter-driven full run:

```powershell
.\Optimize-Win11.ps1 -Preset Full -NoGui
```

## Profiles

| Profile | What it does |
|---|---|
| `ReportOnly` | Inventories the machine and writes reports without applying changes. |
| `Balanced` | Applies broadly safe Windows 11 debloat, privacy, UI, scheduled task, service, AppX, temp cleanup, TRIM, and CPU power tweaks. |
| `Full` | Adds external tool installs, Cloudflare DNS, Nagle/network adapter tweaks, health checks, and more conditional service cleanup. This is the default in the UI. |

## Startup UI

The UI asks only for choices that should not be guessed:

- Profile: `Full`, `Balanced`, or `ReportOnly`
- DNS provider
- Whether to create a restore point
- Whether to remove AppX bloat
- Whether to install the external tool stack
- Whether to run DISM/SFC health checks
- Whether to auto-reboot at the end

The "Must confirm up front" section contains options that are more disruptive, harder to undo, or long-running:

- Hosts-file telemetry blocking
- DISM `ResetBase`
- Manual pagefile sizing
- BCDEdit latency tweaks
- Windows Update installation through `PSWindowsUpdate`
- WinSAT benchmarks

Once you click Run, the script does not ask more questions.

## Automatic Detection

The script checks Windows build, laptop/desktop state, AC power, RAM, system disk type, Everything installation, printers, BitLocker, Bluetooth, biometrics, active network adapters, winget availability, startup entries, AppX bloat, scheduled tasks, disk space, RAM usage, power plan, and pagefile state.

It uses those checks instead of asking questions. For example:

- Laptops on battery do not get forced into Ultimate Performance in `Balanced`.
- Windows Search is not fully disabled unless Everything is installed.
- SysMain is disabled only when the OS disk is detected as SSD/NVMe.
- Printer, BitLocker, Bluetooth, and biometric services are only considered in `Full`, and only when matching hardware/features are not detected.

## Reports

Each run writes:

- `reports\win11-optimizer-<timestamp>.md`
- `reports\win11-optimizer-<timestamp>.json`
- `reports\win11-optimizer-<timestamp>.log`

The Markdown report includes automatic detections, before/after metrics, every applied/skipped/failed action, and follow-up notes.

## Extra Switches

Some guide items are powerful but easier to regret, so they are explicit switches:

```powershell
.\Optimize-Win11.ps1 -Preset Full -BlockTelemetryHosts
.\Optimize-Win11.ps1 -Preset Full -RunComponentResetBase
.\Optimize-Win11.ps1 -Preset Full -SetManualPageFile
.\Optimize-Win11.ps1 -Preset Full -EnableBcdLatencyTweaks
.\Optimize-Win11.ps1 -Preset Full -InstallWindowsUpdates
.\Optimize-Win11.ps1 -Preset Full -RunBenchmarks
```

Maximum-style run:

```powershell
.\Optimize-Win11.ps1 -Preset Full -NoGui -BlockTelemetryHosts -RunComponentResetBase -SetManualPageFile -EnableBcdLatencyTweaks -InstallWindowsUpdates -RunBenchmarks
```

Use that only when you are comfortable with the tradeoffs:

- `-RunComponentResetBase` prevents uninstalling currently installed Windows updates.
- `-BlockTelemetryHosts` edits the hosts file and can affect Microsoft services.
- `-SetManualPageFile` changes virtual memory sizing and requires a reboot.
- `-EnableBcdLatencyTweaks` changes boot configuration and should be tested, not blindly assumed better.
- `-InstallWindowsUpdates` installs available Windows updates and can take a long time.

## External Tools

`Full` attempts to install these with `winget` when available:

- Everything
- Autoruns
- RAMMap
- WizTree
- WinDirStat
- Bulk Crap Uninstaller
- Process Lasso
- LatencyMon
- CrystalDiskMark
- DriverStore Explorer
- O&O ShutUp10++
- Snappy Driver Installer Origin

The script does not automatically pipe remote PowerShell launchers like Chris Titus WinUtil or Sophia Script into execution. Those are excellent tools, but they should be launched deliberately from their upstream sources.

## Useful Automation Flags

```powershell
.\Optimize-Win11.ps1 -Preset ReportOnly -NoGui
.\Optimize-Win11.ps1 -Preset Balanced -NoGui -NoRebootPrompt
.\Optimize-Win11.ps1 -Preset Full -NoGui -AutoReboot
.\Optimize-Win11.ps1 -Preset Full -NoGui -WhatIf
```
