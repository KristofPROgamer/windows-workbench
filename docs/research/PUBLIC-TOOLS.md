# Public tools and integration contract

Research date: **2026-10-04**. Target: personal Windows 11 gaming and everyday PCs. This is an audit and implementation plan; no integration, external script execution, installation, or Windows modification was performed for this document. `third.md` was reviewed as supplied research and reconciled against primary sources; its findings are not an executable specification.

## Recommended architecture

Build a native planner, state journal, executor, and verifier. The proposed product uses C#/.NET 10, a WPF interface and CLI over the same core, and a short-lived privileged broker for approved machine changes. Public projects contribute reviewed implementations and specialized capabilities. Each registry value, service, scheduled task, application scope, and power setting must have exactly one owner in a proposed run. Broad capability should mean better inspection, choices, recovery, and measurements, with a small default change set.

The first integration candidate is a **restricted Win11Debloat adapter**, only where an upstream feature offers enough value to justify another execution surface. Simple supported operations belong in native providers. **Sophia** is a later selective-function candidate. **WinUtil** is primarily a source reference. Running all three presets sequentially is rejected. These are architecture recommendations, not statements that the candidates have passed release qualification.

Use a separate optional tool catalog for diagnostics and quality-of-life applications. An entry should say what problem the tool solves, whether it remains running, where it comes from, what it changes, and how to remove it. Nothing in the catalog is installed merely because a comprehensive profile was selected.

## Verified source identities, not approved execution artifacts

| Project | Release identity verified on research date | Research status |
|---|---|---|
| Win11Debloat | [2026.08.24](https://github.com/Raphire/Win11Debloat/releases/tag/2026.08.24), full commit `f431c56159bfd0b68822c583509126bab8cb94cc`; release published 2026-08-24 | Entry-point contract and critical helpers reviewed. Newer master `bbdb9365f4d38566c93dc3e790036911fae0e877` was separately inspected; do not conflate it with the release. |
| Sophia Script | [7.3.0](https://github.com/farag2/Sophia-Script-for-Windows/releases/tag/7.3.0), full commit `f370e91800dd1242001c4d3556329360ad3338c8`; published 2026-09-05 | Release metadata, asset choices, documentation, license, downloader reviewed; individual functions are not qualified. |
| WinUtil | [26.09.29](https://github.com/ChrisTitusTech/winutil/releases/tag/26.09.29), full commit `9419b2803e505b67a71b632205ce59132b52b41b`; published 2026-09-29 | Release metadata and source configuration inspected for reference; no runtime adapter proposed. |

Full identities were resolved through each repository's GitHub REST release/commit metadata. Sophia release metadata exposes SHA-256 asset digests; these are useful expected identities, but no archive was downloaded and independently hashed in this review. There is **no release-approved payload manifest** yet. Short hashes, a version string in a script, a GitHub signature badge, and an upstream asset digest are not substitutes for complete artifact qualification.

## Win11Debloat: first optional adapter candidate

### Verified behavior

The [release entry point](https://github.com/Raphire/Win11Debloat/blob/f431c56159bfd0b68822c583509126bab8cb94cc/Win11Debloat.ps1) declares `SupportsShouldProcess`, uses administrator privileges, rejects PowerShell Core, and requires Windows PowerShell 5.1. Relevant controls include `-Silent`, `-LogPath`, `-Config`, `-Apps`, `-RemoveApps`, `-User`, `-AppRemovalTarget`, `-SkipExplorerRestart`, and `-CreateRestorePoint`. Its WSL switch is `-EnableWindowsSubsystemForLinux`. It checks supporting files and imports helpers; the entry script alone is insufficient.

The [target helper](https://github.com/Raphire/Win11Debloat/blob/f431c56159bfd0b68822c583509126bab8cb94cc/Scripts/Helpers/Get-TargetUserForAppRemoval.ps1) defaults app removal to `AllUsers`, including removal from the image. The adapter must always select a scope explicitly. `CurrentUser` refers to the executing identity, so it is insufficient when elevation uses a different account: resolve the original user's SID before elevation, validate the upstream username mapping, and reject ambiguity. Never infer that privacy/UI targeting and app-removal targeting are interchangeable.

The [download helper inspected on master](https://github.com/Raphire/Win11Debloat/blob/bbdb9365f4d38566c93dc3e790036911fae0e877/Scripts/Get.ps1) resolves the latest release, or master in development mode, stages files in a temporary location, and launches the downloaded content. This convenience bootstrap is excluded from the adapter. Its tree also contains user configuration, logs, and backups, which require deliberate separation from immutable source files.

The [execution orchestrator](https://github.com/Raphire/Win11Debloat/blob/bbdb9365f4d38566c93dc3e790036911fae0e877/Scripts/Features/Invoke-Changes.ps1) tracks per-feature and app-removal failures and reports warnings. The [release exit helper](https://github.com/Raphire/Win11Debloat/blob/f431c56159bfd0b68822c583509126bab8cb94cc/Scripts/CLI/Wait-ForKeyPress.ps1) defaults to exit code zero. Therefore a completed process is only evidence of process completion; the host must verify each requested outcome independently.

Upstream [recovery documentation](https://github.com/Raphire/Win11Debloat/wiki/Reverting-Changes) describes registry backups, Start-layout restoration, and reinstalling removed apps. Registry restoration does not reinstall applications. App reinstallation cannot promise the old version, app data, sign-in state, or provisioned-image state. Widgets and search changes can include package removal, so a friendly feature label must not be mistaken for a registry-only operation.

The [MIT license](https://github.com/Raphire/Win11Debloat/blob/f431c56159bfd0b68822c583509126bab8cb94cc/LICENSE) permits reuse with the required notice. Retain original attribution and identify any local patches separately.

### Proposed first contract

| Boundary | Required implementation behavior |
|---|---|
| Runtime | Explicit full path to the supported Windows PowerShell 5.1 executable; verified architecture; no profile loading; host-resolved elevation; no second automatic relaunch. Use a trusted working directory and constrained environment, and resolve helper executables/modules from reviewed locations instead of user-writable search paths. |
| Targets | Plan includes original user SID and app-removal scope. Default to that user's selected apps. All-user removal and image deprovisioning are separate, visible operations. |
| Inputs | Generate strict versioned JSON or typed arguments from known operations. Never forward arbitrary switches, script blocks, downloaded profiles, or free-form commands. |
| Scope | Explicit app IDs and a few individually qualified UI/preferences. No `RunDefaults`, `RunDefaultsLite`, `RunSavedSettings`, `Apps Default`, broad OEM/gaming presets, forced Edge removal, or backup suppression. |
| Protected capabilities | Preserve Store, App Installer, framework packages, security, servicing, recovery, and selected gaming functionality. Verify package dependency metadata; a small name denylist is insufficient. |
| Logs | Create a private run directory before launch. Preserve stdout/stderr, the upstream transcript, selected feature IDs, effective scope, process result, and host verification results. |
| Explorer/restart | Request `SkipExplorerRestart`; consolidate restart requirements into the host's end-of-run action. Do not restart the shell during planning. |
| Network | No bootstrap or updater at execution time. Qualify every selected path for nested downloads/package-manager behavior; skip unsupported networked operations rather than silently broadening scope. |
| Isolation | Run in a dedicated child process with a timeout and cancellation model. This prevents PowerShell state collision; it does **not** sandbox an elevated script. |
| Dry run | Host renders plans from its own inventory and operation catalog. Upstream `WhatIf` is a qualification aid; do not use it as the sole proof that planning cannot mutate Windows. |
| Outcome | Verify package presence, exact registry type/data, service configuration, and task state as appropriate. Report partial/failed/verification-unavailable explicitly. |
| Recovery | Host records previous state before invocation. Preserve upstream backup output as supplementary recovery evidence. App deletion is labeled best-effort reinstall, never exact undo. |

For a personal gaming PC, preserve Xbox authentication, Gaming Services, controller support, Game Pass dependencies, overlays, and captures unless the user explicitly selects their removal after seeing the consequence. A rarely used app is not automatically unwanted.

## Sophia Script: selective reference first, adapter later

The [release documentation](https://github.com/farag2/Sophia-Script-for-Windows/blob/f370e91800dd1242001c4d3556329360ad3338c8/README.md#how-to-run-the-specific-functions) offers individual function invocation, separate PowerShell 5.1/7 and ARM packages, and default-restoration functions. Its regular Windows 11 support table says **25H2+**, with separate LTSC entries. The third-party GUI wrapper is closed source. Do not infer that the script license covers that wrapper or that every Windows 11 build accepts the same payload.

The [MIT source license](https://github.com/farag2/Sophia-Script-for-Windows/blob/f370e91800dd1242001c4d3556329360ad3338c8/LICENSE) supports adopting selected implementations with attribution. Each selected function still needs a build/edition/runtime test, a side-effect inventory, a previous-state snapshot, and independent verification.

The [reviewed downloader](https://github.com/farag2/Sophia-Script-for-Windows/blob/f370e91800dd1242001c4d3556329360ad3338c8/Download_Sophia.ps1) resolves mutable online release information. Bypass it in the product design: select a specific asset during release engineering and verify its contents independently. No default `Sophia.ps1` execution, downloaded preset execution, third-party wrapper dependency, automatic runtime installation, recurring cleanup tasks, or ancillary content downloads enter the initial adapter.

Proposed adoption sequence: identify a capability missing from native providers; review exactly that function and its imports; decide whether a native implementation is simpler; only then justify an adapter. A second broad executor has ongoing compatibility cost even when it is open source. Preserve the distinction between applying an opposite toggle, resetting a documented default, and restoring this machine's captured state.

## WinUtil: implementation reference, no preset adapter

WinUtil's [MIT license](https://github.com/ChrisTitusTech/winutil/blob/9419b2803e505b67a71b632205ce59132b52b41b/LICENSE) allows attributed source reuse. The [preset configuration](https://github.com/ChrisTitusTech/winutil/blob/main/config/preset.json) includes service and telemetry changes even in Minimal. Its [tweak configuration](https://github.com/ChrisTitusTech/winutil/blob/main/config/tweaks.json) includes fixed `OriginalValue`/`OriginalType` entries and a service-host grouping adjustment. Fixed restore values should not be interpreted as captures of a user's prior state.

The [September release notes](https://github.com/ChrisTitusTech/winutil/releases/tag/26.09.29) include corrections to service restoration and completion reporting after failed tweak steps. These are useful regression scenarios for this project's executor. They do not establish that every present operation is defective.

Adopt useful, independently checked concepts into native operations: structured catalog entries, startup visibility, package-selection UX, and diagnostic reporting. Exclude mutable web launchers, whole presets, image-building/MicroWin workflows, and automatic update-policy passes. Before copying any implementation, resolve the source to a full commit and test it on supported Windows versions. The reviewed moving-branch configuration links above are research references, not runtime pins.

## Broader capability catalog

The role column is this project's recommendation. Licensing descriptions summarize the reviewed publisher material; exact selected artifacts and bundled dependencies still need packaging review.

| Tool / method | Useful capability | Proposed role and boundary | License / persistence evidence |
|---|---|---|---|
| [PowerToys](https://learn.microsoft.com/en-us/windows/powertoys/) | FancyZones, PowerRename, Keyboard Manager, Command Palette, and other everyday conveniences | Optional user-selected installation; enable only desired modules. No FPS claims. | [MIT source](https://github.com/microsoft/PowerToys/blob/main/LICENSE); resident when running. Prefer normal official installation. |
| [Autoruns](https://learn.microsoft.com/en-us/sysinternals/downloads/autoruns), [Process Explorer](https://learn.microsoft.com/en-us/sysinternals/downloads/process-explorer), [RAMMap](https://learn.microsoft.com/en-us/sysinternals/downloads/rammap) | Startup, process, and memory diagnosis | Detect/launch an official user-installed copy. Use findings to propose changes; no blanket startup disable or automatic memory flushing. | [Sysinternals terms](https://learn.microsoft.com/en-us/sysinternals/license-terms) allow use on the user's devices but restrict publication/transfer. Do not bundle. Diagnostic exports may contain sensitive paths/data. |
| [PresentMon](https://github.com/GameTechDev/PresentMon) | Frame-duration and presentation metrics | Preferred future benchmark backend; bounded capture, retain raw data, use the console component where sufficient. | MIT project plus component notices. Console capture is on-demand; the separate telemetry service has a different footprint. |
| [CapFrameX](https://github.com/CXWorld/CapFrameX) | Manual frametime comparison and sensor analysis | Optional external analysis, initially import captures only. Avoid simultaneous overlapping FPS capture. | Current README requires .NET 10 and describes bundled components and a local API/MCP service; artifact review required. Portable is not proof of zero residual processes. |
| [Windows Performance Toolkit](https://learn.microsoft.com/en-us/windows-hardware/test/wpt/) | WPR/WPA traces for CPU, storage, startup, and responsiveness | Advanced diagnostics when simpler measurements cannot explain an issue; bounded trace sessions | Microsoft-distributed; acquire through official channels and review installation footprint. |
| [LatencyMon](https://www.resplendence.com/latencymon) | DPC/ISR and real-time audio troubleshooting | Manual diagnostic for a reproducible symptom; readings do not certify gaming gains | Proprietary publisher software; exact redistribution terms not qualified. |
| [BCUninstaller](https://github.com/BCUninstaller/Bulk-Crap-Uninstaller) | Human-driven third-party uninstall workflow | Optional advanced app tool. No automatic leftover deletion. | Apache-2.0; project currently seeks maintainers. Runtime/portable dependencies vary by package. |
| [Everything](https://www.voidtools.com/support/everything/) | Fast filename/path discovery | Optional everyday tool; keep Windows Search functionality unless independently justified | Official binary license must be reviewed before bundling; indexing/service choices add persistence. |
| [WinDirStat](https://windirstat.net/) / [WizTree](https://diskanalyzer.com/) | Visual disk usage and locating large files | Offer one chosen analyzer; files are not deletion candidates merely because large | WinDirStat site specifies GPL v3-or-later. WizTree publisher terms require separate review; do not assume freeware permits bundling. |
| [Wintoys](https://bogdan-patraucean.github.io/about/wintoys/) | Alternative maintenance/settings interface | Optional external application. No second automatic optimization pass. | Publisher app; source/automation/redistribution contract not qualified. Its launcher alias is not a granular API. |
| [O&O ShutUp10](https://www.oo-software.com/en/shutup10) | Privacy settings reference and manual GUI | Manual/reference only, no preset chaining | Proprietary freeware and Premium offering. Current Premium can continuously reapply settings; that conflicts with exclusive setting ownership. |
| [Process Lasso](https://bitsum.com/) / [ParkControl](https://bitsum.com/parkcontrol/) | Specific process/power workflows | Expert optional tools; leave affinity, core parking, and scheduler policies at defaults absent evidence | Proprietary Bitsum tools. Process Lasso's Governor is a persistent background service. |
| [FanControl](https://github.com/Rem0o/FanControl.Releases) | Manual thermal/noise tuning on compatible hardware | External opt-in only; preserve OEM/firmware control by default, no supplied universal fan curves | [License](https://github.com/Rem0o/FanControl.Releases/blob/master/LICENSE) restricts use to personal/noncommercial and redistribution without consent. Closed source, signed executables, optional startup/service, hardware driver dependencies. |
| [DriverStore Explorer](https://github.com/lostindark/DriverStoreExplorer) | Driver inventory and deliberate repair | Advanced manual only; native read-only driver inventory is preferable in core product | GPL-2.0 repository; upstream explicitly warns deletion can cause boot/device failure. Source/dependency obligations remain. |
| OEM/GPU vendor update tools | Supported hardware drivers, firmware, and device configuration | Link to exact hardware vendor; inventory version first; record driver changes as separate maintenance | Each publisher controls packages/terms. No bulk third-party driver updater in a performance profile. |
| Custom Windows images / broad playbooks | A different OS deployment model | Excluded from runtime optimizer; source ideas may be evaluated individually | Separate servicing, licensing, deployment, and recovery project; no inferred redistribution rights to Windows. |

## Artifact and execution contract

1. **Choose at review time.** Resolve the tag to a full commit. Record exact source and asset URLs, publication time, selected architecture/runtime, file sizes, upstream digests, local SHA-256 results, licenses, notices, nested dependencies, and the test report. A registry of permitted versions is distinct from a list of interesting projects.
2. **Verify the full behavior tree.** Cover scripts, imported modules, JSON, registry files, XAML, binaries, and dependency data. Reject missing/extra executable or configuration files, path traversal, unexpected links/reparse points, and alternate entry points. A top-level script hash does not cover imported behavior.
3. **Separate immutable code and mutable run data.** A protected cache holds reviewed source. If upstream requires writes alongside code, create a private per-run working copy with narrowly declared mutable paths for logs, backups, and generated configuration. Reverify all executable/configuration inputs immediately before launch; do not let a standard-user-writable staging tree become elevated code.
4. **Treat manifests as a trust root.** Bind the manifest to the product's signed release or other protected release channel. A digest obtained from the same compromised source as a payload does not authenticate that publisher. Signature validity, publisher identity, content review, and observed behavior are separate checks.
5. **Constrain nested acquisition.** Scan source and review behavior for every downloader, package manager, URL, install task, and updater. An allowlisted domain alone is insufficient: use selected versions and independently expected bytes where possible. Network access requirements are visible in the operation plan. No command may silently fetch and execute an unreviewed replacement.
6. **Journal before mutation.** Flush previous-state evidence and operation intent before applying. Record completed/failed/unverified operations incrementally so crashes do not lose the recovery record. Registry existence, type, data, user hive, and 32/64-bit view all matter. Backups must be protected against tampering and checked before restoration.
7. **Verify after mutation.** Probe Windows independently of upstream console text. Verification failures produce partial/failed states. Read-back success can still require reboot/session refresh before functional smoke tests. Exit code zero is never enough.
8. **Recover honestly.** Exact restoration requires a captured prior state and supported reverse operation. Before rollback, detect if the current value has drifted since apply; do not overwrite a newer user/Windows change silently. App/file removal and update cleanup require honest limitations and a recovery recipe.
9. **Update deliberately.** Review source, parameters, registry/service/task/package scopes, URLs, dependencies, and licenses between old and candidate versions. No runtime self-update of adapters. If verification fails or the candidate is unavailable, stop that operation; do not fall back to latest.
10. **Pin dependency resolution.** A fixed PowerShell executable and `NoProfile` do not constrain its PATH, PSModulePath, current-directory lookups or every invoked helper. Give the child a reviewed working directory/environment, resolve required executables and modules to trusted paths, and test that a same-name helper or module in a user-writable search path cannot be loaded under elevation.

## Qualification gates before implementation can claim safety

The first executable release should support a named Windows 11 x64 build/edition matrix, initially Home and Pro. Unsupported or unknown builds may run inventory only. ARM64, LTSC, managed devices, and other variants need explicit independent lanes before adoption.

Required tests include: no-change planning; wrong runtime; missing/changed/extra payload files; standard-user elevation to a different account; multiple profiles; exact user vs all-user app scope; missing WinGet; network unavailable; app framework dependencies; cancellation; partial native-command failure; restore failure; repeated apply; rollback after user drift; and crash recovery. Test apply, reboot, verify, rollback, reboot, and compare against the captured baseline in disposable VMs. Gaming, sleep, controller, Bluetooth, Store, update, and hardware-sensitive behavior also require real hardware validation.

Benchmark integrations should keep raw capture data and the workload definition, warm-up, power state, temperatures, driver versions, and repeated samples. Label variation and uncertainty. A reduction in services, RAM use, or process count is not proof of improved frame pacing or application responsiveness. No universal performance percentage or universal safety claim is supportable from this research alone.
