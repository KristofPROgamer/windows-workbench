# Windows Workbench

A native Windows 11 desktop app for understanding your PC, reviewing improvements, and applying a small set of reversible preferences. **Version 0.1.0 is a working preview**, with the audit and broader revamp roadmap preserved alongside it.

## Start the delivered app

Double-click **Start-WindowsWorkbench.cmd** in this folder, or open **artifacts\WindowsWorkbench\WindowsWorkbench.exe** directly. Run normally, without administrator elevation. The published folder includes its .NET runtime; no SDK, installer or account is needed to run it. Keep the entire folder together.

First launch performs read-only assessment. Choose preferences, open **Review changes**, inspect each before/after value, and explicitly apply only those you want. **History** shows recorded results and offers guarded recovery. Windows may require a new Explorer window or sign-out before a preference becomes visible; the app does not restart Explorer or reboot automatically.

## Available now

- Native desktop interface with search, priority filters, review, local history and redacted report export.
- Read-only Windows, CPU, memory/commit, storage, network counts, power, display, startup counts, app counts, service configuration and restart observations. Incomplete results remain unknown or unavailable.
- Six current-user preferences: show file extensions, left-align the taskbar, hide Task View, disable transparency, reduce Settings suggestions, and disable the advertising-ID preference.
- **85 manual guides** for performance, gaming, privacy, productivity, storage and maintenance, plus **12 optional tool references** including Win11Debloat, PowerToys, Sysinternals, PresentMon and CapFrameX. Guides open supporting instructions or Windows Settings. Public scripts are not downloaded or executed.
- Shared desktop/CLI planning with expiring reviews, device/account binding, current-value checks, a durable SQLite journal, readback verification and recovery that preserves conflicting changes.

Every preference starts unselected. Priority filters never select changes. Configuration verification is separate from visible Windows behavior and measured performance; this release promises no FPS or speed increase.

## Preview boundaries

Direct changes are limited to non-elevated, unmanaged consumer Home/Pro x64 systems in build families 26100, 26200 and 26300. Other systems retain assessment and guidance. The build list is a configuration gate, **not a completed compatibility certification**. Failed management queries block writes.

The preview is unsigned. Real-machine apply/undo across the supported editions, forced process termination, power loss, alternate credentials, high-DPI and assistive-technology testing remain release qualification work. Automated tests use simulated providers and isolated journals; testing this project did not apply optimization settings to your PC.

Registry comparisons and writes are not an atomic transaction: another program can change a setting between them. Recovery restores a recorded value/type/absence when eligible; it cannot restore deleted files, old advertising identifiers or every visible Windows effect. Keep your normal backups. The journal checksum detects accidental edits/corruption, not malicious software with the same user's access.

This preview does not implement the planned privileged broker, app-removal engine, automated cleanup, benchmark capture/analysis or qualified third-party adapters. Their specifications remain in [the revamp plan](docs/REVAMP-PLAN.md).

## Data and recovery

The application has no telemetry, background service or automatic updater. Assessment stays local. Opening an external source contacts that publisher through your browser.

Applying a plan creates `%LOCALAPPDATA%\WindowsWorkbench\state\journal.sqlite`. Keep this directory to retain recovery. It contains original settings and account/device binding identifiers; do not publish it. Reading empty history does not create a journal. Save CLI plans privately; they expire after 30 minutes.

Desktop report export omits raw inventory and identifiers. CLI assessment omits principal identifiers, app names, startup commands and network addresses, but includes hardware details and drive letters. Neither export is a complete backup.

## Build and verify

Requires Windows and the .NET SDK version in `global.json` (10.0.401, compatible patch roll-forward allowed). Scripts prefer the project-local `.tools\dotnet\dotnet.exe` if present, then the installed SDK. Dependencies come from NuGet and use checked-in lock files.

```powershell
.\tools\Build.ps1
.\tools\Test.ps1 -SkipBuild
.\tools\Build.ps1 -Publish
```

The last command produces the self-contained x64 folder in `artifacts\WindowsWorkbench`. `SHA256SUMS.json` records file integrity, not publisher authentication. Third-party license information is in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Read-only CLI examples from the delivered folder:

```powershell
.\WindowsWorkbench.Cli.exe assess
.\WindowsWorkbench.Cli.exe catalog --mode Preference
.\WindowsWorkbench.Cli.exe plan --actions explorer.extensions --out private-plan.json
.\WindowsWorkbench.Cli.exe history
.\WindowsWorkbench.Cli.exe help
```

Applying requires the saved plan and its exact approval digest. Recovering requires an explicitly selected run ID. The help command documents both; there is no Full preset or automatic yes-to-everything mode. Files created by the CLI are not silently overwritten.

## Project documents

- [Implementation status and validation](docs/IMPLEMENTATION-STATUS.md)
- [Extensive legacy audit](docs/AUDIT.md)
- [Complete revamp plan](docs/REVAMP-PLAN.md)
- [Performance research](docs/research/WINDOWS-PERFORMANCE.md)
- [Windows quality-of-life research](docs/research/WINDOWS-QOL.md)
- [Public tools and script evaluation](docs/research/PUBLIC-TOOLS.md)
- [Research reconciliation](docs/research/RESEARCH-RECONCILIATION.md)

The archived script and guide are preserved in `legacy/` for inspection. The root `Optimize-Win11.ps1` now only launches this app or performs read-only CLI assessment; legacy flags and the old aggressive workflow are retired.
