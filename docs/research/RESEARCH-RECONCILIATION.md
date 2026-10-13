# Research reconciliation

Date: **2026-10-04**. This document records the treatment of all four supplied research reports. The detailed integration investigation below covers `third.md` (559 lines, read in full); the linked catalogs and audit cover the other reports. It records what informs the revamp plan and what needs correction or qualification. It does not certify any tool or approve execution.

## Coverage index

| Supplied research | Where its conclusions are evaluated |
|---|---|
| `first.md` | [Performance catalog](WINDOWS-PERFORMANCE.md), including measured-benefit distinctions, benchmark design and legacy tweak corrections. |
| `second.md` | [Quality-of-life catalog](WINDOWS-QOL.md), including user preferences, privacy, permissions, app removal and supported-setting versus automation boundaries. |
| `third.md` | Detailed corrections below and the [public tools integration contract](PUBLIC-TOOLS.md). |
| `fourth.md` | [Code audit and architecture review](../AUDIT.md) and [selected revamp plan](../REVAMP-PLAN.md), including privilege, crash recovery, delivery scope and test gates. |

This index separates coverage rather than implying that a citation marker in one supplied document substantiates claims in another. The main audit owns the combined decisions.

## Cross-report decisions and corrections

The reports agree on the useful direction: diagnose bottlenecks, preserve explicit choices, make selected changes, verify outcomes and capture recovery information. Agreement among reports is not independent evidence when they may derive from the same source. The final plan is a curated engineering decision, not a concatenation of every suggested feature.

| Research area | Adopted or corrected treatment | Evidence / destination |
|---|---|---|
| Performance vs counters | Retain boot/launch/frame-time/power measurements. Do not infer a gain from fewer processes, more free RAM or disabled services. No performance magnitude was measured during this audit. | [Performance protocol](WINDOWS-PERFORMANCE.md) |
| Game Mode/Game Bar | Do not treat both as disposable background software. Apply hardware-specific dependencies, including AMD's documented Ryzen 9 7950X3D/7900X3D setup guidance, rather than generalizing it to every CPU. | [AMD setup guidance](https://community.amd.com/t5/gaming/how-to-set-up-your-system-with-a-new-amd-ryzen-9-7950x3d-or/ba-p/589464) and performance catalog |
| Supported UI versus automation | A documented setting can be offered as guided navigation immediately. An undocumented registry recipe still needs a qualified provider and effective-behavior checks. | [QoL catalog](WINDOWS-QOL.md), plan action contract |
| Camera permission and Windows Hello | Correct `second.md`'s implication that app-camera permission changes disable Hello. Windows documents that Hello can still use the camera for sign-in when app-camera access is off. Device/biometric service disabling is a different operation. | [Microsoft camera privacy](https://support.microsoft.com/en-us/windows/privacy/windows-camera-microphone-and-privacy), QoL catalog |
| Application data and reinstall | `Remove-AppxPackage -PreserveApplicationData` is not a general consumer-app backup mechanism; its documented applicability is development/loose-file registrations. Conditional reinstall cannot promise original app data. | [Microsoft Remove-AppxPackage](https://learn.microsoft.com/en-us/powershell/module/appx/remove-appxpackage?view=windowsserver2025-ps), QoL application policy |
| Storage Sense defaults | Read actual retention and cloud-file settings. Descriptions of default behavior are not a substitute for device-specific inventory; restoring retention preferences cannot recover deleted files. | [Storage Sense](https://support.microsoft.com/en-us/windows/experience/storage-filemanagement/manage-drive-space-with-storage-sense), QoL catalog |
| Windows release chronology | Use dated, action-specific qualification. 26H1 build 28000 and 26H2 build 26300 demonstrate why numeric minimum-build gating alone is inadequate. | [Microsoft release information](https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information) |
| 24H2 consumer servicing date | `fourth.md` says October 14 in one section. Microsoft's table says **October 13, 2026** for Home/Pro. | [Microsoft release information](https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information) |
| Severity inflation | `fourth.md` labels many design/control-flow failures Critical. The code audit uses 27 prioritized findings with concrete scenarios and no claimed proven critical exploit. | [Audit rubric and findings](../AUDIT.md) |
| Runtime choice | Adopt .NET 10 as a proposed LTS implementation baseline, not as a security guarantee. Current support through November 14, 2028 was independently checked. | [Microsoft .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) |
| Plan digest and IPC | A hash identifies bytes; it does not establish authorization. A user ACL/nonce alone does not authenticate the reviewed UI. Use a bounded broker with independent plan, identity and resource validation. | [Named-pipe security](https://learn.microsoft.com/en-us/windows/win32/ipc/named-pipe-security-and-access-rights), plan section 4.4 |
| Undo ownership | Matching the applied value is a divergence check, not atomic compare-and-swap or proof that no external actor changed state. Scope the guarantee and handle conflicts. | [Architecture review](../AUDIT.md), plan section 5.4 |
| Journal durability | WAL is not an OS-wide transaction; durability settings, publication order and failure testing matter. A Windows write can succeed before its journal outcome is saved. | [SQLite synchronous behavior](https://www.sqlite.org/pragma.html#pragma_synchronous), plan section 5.3 |
| Timeouts and process killing | Killing a process tree does not safely cancel every servicing/installer operation or work owned by Windows services. Use operation-specific cancellation and indeterminate outcomes. | [Architecture review](../AUDIT.md), plan provider contract |
| Broad platform promise | Consumer Home/Pro first, with published qualification. Managed/LTSC/Arm64 and newer builds require separate evidence rather than immediate blanket support. | [Plan compatibility matrix](../REVAMP-PLAN.md) |
| First milestone | Start with a usable read-only prototype and a narrow reversible vertical slice. Extensive feature coverage follows tested foundations. | [Plan roadmap](../REVAMP-PLAN.md) |

Opaque citations and unverified historical issue anecdotes were not promoted into confirmed facts. Where the supplied research's claim was not required for a decision and its source could not be reconstructed, it remains unqualified rather than silently adopted. The original reports are preserved unchanged; the input hashes are in the [verification record](../audit/VERIFICATION.md).

## Source hygiene

`third.md` contains internal citation markers such as `turn…view…` and `turn…search…`. They are not usable source URLs outside their original conversation and cannot be treated as verified citations in this repository. The original document remains untouched as user-provided research. [PUBLIC-TOOLS.md](PUBLIC-TOOLS.md) replaces adopted claims with actual primary links and distinguishes source review from completed artifact/VM qualification.

No supplied short hash or release name was accepted merely because it appeared in the research. GitHub release and commit metadata were queried independently. The following identities were confirmed:

| Supplied claim | Rechecked result |
|---|---|
| Win11Debloat `2026.08.24 / f431c56` | [Release](https://github.com/Raphire/Win11Debloat/releases/tag/2026.08.24), full commit `f431c56159bfd0b68822c583509126bab8cb94cc`; latest release observed on research date. |
| Sophia `7.3.0 / f370e91`, September 5 | [Release](https://github.com/farag2/Sophia-Script-for-Windows/releases/tag/7.3.0), full commit `f370e91800dd1242001c4d3556329360ad3338c8`; date confirmed. API exposes per-asset SHA-256 digests, but downloaded artifact bytes were not independently validated. |
| WinUtil `26.09.29 / 9419b28`, September 29 | [Release](https://github.com/ChrisTitusTech/winutil/releases/tag/26.09.29), full commit `9419b2803e505b67a71b632205ce59132b52b41b`; GitHub metadata confirms a verified commit signature. That signature does not certify the source's behavior. |

These are **verified research references**, not approved dependency locks. All future integration locks must include full artifact verification and tests. The newer Win11Debloat master examined during research (`bbdb9365f4d38566c93dc3e790036911fae0e877`) has changes beyond the stable release. Claims from that tree must not silently be attributed to the stable artifact.

## Corrections and important qualifications

| `third.md` location / claim | Resolution | Consequence for plan |
|---|---|---|
| Line 130 names `-EnableWSL` among Win11Debloat parameters | **Incorrect parameter name.** The [release parameter block](https://github.com/Raphire/Win11Debloat/blob/f431c56159bfd0b68822c583509126bab8cb94cc/Win11Debloat.ps1) uses `-EnableWindowsSubsystemForLinux`. | Generate adapter contracts from the pinned source; reject unknown parameters. WSL installation remains a separate optional capability. |
| Explicit app targeting is described, but the hazardous default is not highlighted | [Upstream helper](https://github.com/Raphire/Win11Debloat/blob/f431c56159bfd0b68822c583509126bab8cb94cc/Scripts/Helpers/Get-TargetUserForAppRemoval.ps1) defaults to **AllUsers plus image removal**. | Every operation must name the user and app scope; current-user selection must survive alternate-account elevation. |
| Windows PowerShell 5.1 requirement | **Confirmed** in the pinned entry point, which rejects PowerShell Core. | Do not invoke Raphire through whichever `pwsh` happens to be on PATH. |
| `SupportsShouldProcess` means dry-run support | Declaration confirmed, but it does not prove all helpers are free of side effects. Startup includes logging and supporting-file/state handling. | Native planning is read-only by design; qualify upstream `WhatIf` per operation. |
| Normal-looking successful completion despite failures | **Supported.** [Wait-ForKeyPress](https://github.com/Raphire/Win11Debloat/blob/f431c56159bfd0b68822c583509126bab8cb94cc/Scripts/CLI/Wait-ForKeyPress.ps1) defaults to exit zero; normal main completion does not synthesize every feature outcome. | Independent postcondition probes remain mandatory. |
| Sophia supports current Windows 11 editions and PS7/ARM | Broadly correct but underspecified. The [current support table](https://github.com/farag2/Sophia-Script-for-Windows#system-requirements) says **Windows 11 25H2+** for regular editions, with separate LTSC packages. | A generic `Windows 11 supported` claim is inadequate. Match build, edition, architecture, runtime, and selected release asset. |
| Sophia is MIT | Script source is MIT; the [documented wrapper](https://github.com/farag2/Sophia-Script-for-Windows#wrapper) is third-party closed source. | Do not include the wrapper in an MIT assumption or in the initial adapter. |
| WinUtil September release corrected misleading completion logs/service restoration | **Confirmed** in [release notes](https://github.com/ChrisTitusTech/winutil/releases/tag/26.09.29). | Use as regression scenarios, not proof that every current tweak is broken. |
| FanControl license remains a research gap | Gap partly closed: [publisher license](https://github.com/Rem0o/FanControl.Releases/blob/master/LICENSE) states personal/noncommercial use and disallows redistribution without consent. | Optional externally obtained tool only; no bundled binary or universal automatic fan configuration. |
| Sysinternals cannot be bundled | [Microsoft terms](https://learn.microsoft.com/en-us/sysinternals/license-terms) restrict publication/transfer while allowing use on the user's devices. | Link to official acquisition or detect installed tools; do not ship copies. Do not conflate private local acquisition with public redistribution. |
| DriverStore Explorer license unknown | Repository identifies [GPL-2.0 and a LICENSE file](https://github.com/lostindark/DriverStoreExplorer). The README explicitly warns of boot/device failure from misuse. | Top-level license gap narrowed; exact selected artifact/dependencies still need review. Keep driver deletion manual. |
| O&O is normally on-demand and exact current product details unavailable | [Current publisher page](https://www.oo-software.com/en/shutup10) describes free portable operation and Premium continuous reapplication of settings. | Persistence depends on edition/configuration. No automatic O&O pass and no Premium enforcement alongside native ownership. CLI/redistribution remains unqualified. |
| Process Lasso uses a background Governor | [Bitsum](https://bitsum.com/) confirms this. | Closing the GUI does not imply its policy engine is stopped. Detect existing ownership before power/affinity experiments. |
| CapFrameX 1.9.1/.NET 10, local MCP/API, monitoring conflicts | [Current README](https://github.com/CXWorld/CapFrameX) confirms these, including write-capable API operations and a separate API remaining when MCP is disabled. | External analysis/capture import first; no automatic local-server connection or configuration writes. Do not assume portable means no remaining capture process. |
| Wintoys automation unclear | [Publisher changelog](https://bogdan-patraucean.github.io/about/wintoys/changelog.html) documents a launch alias, but this is not evidence of a granular state-changing automation API. | Manual external tool remains the appropriate role. |
| BCU described as actively maintained | Current [project page](https://github.com/BCUninstaller/Bulk-Crap-Uninstaller) seeks maintainers. Activity alone does not establish sustained maintenance capacity. | Monitor maintenance status at future packaging review; keep optional/manual. |
| WinDirStat is GPL v3-or-later | **Confirmed** on the [official site](https://windirstat.net/). | Useful optional external disk analyzer; no need to embed it in core product. |

## Architectural recommendations adopted, with refinements

The supplied research's central direction is sound as an engineering recommendation: native ownership of state, narrow adapters, independent verification, and truthful recovery categories. Broad public-script coverage is research breadth, not a reason to install or execute every candidate.

The implementation plan should strengthen several details:

- Write and durably flush the journal **before** mutation. The conceptual diagram in `third.md` places the journal after verification, which must not become execution order.
- An elevated child process provides process isolation, not a security sandbox. Source pinning and an allowlisted parameter set cannot contain malicious administrator code.
- A verified tree still needs protection against replacement between hashing and execution. Use protected locations and explicit ownership/ACL rules, including per-run generated configuration.
- Upstream tools may write settings, backups, and logs beside their code. A blanket ban on extra files needs declared mutable locations; otherwise normal output breaks validation or a too-broad exception admits executable input.
- Exact rollback must detect subsequent changes. If Windows, an external tool, or the user changed a setting after apply, do not overwrite it silently when restoring a previous snapshot.
- Capture the original user SID before elevation. Current user inside an elevated process may be the administrator account, not the intended personal profile.
- Script-backed changes may leave policies, tasks, services, or package state behind after the script exits. `No background script` and `no persistent effect` are different claims.
- VM tests establish repeatability for supported scenarios. They do not replace real hardware tests for game anti-cheat, GPU behavior, sleep, storage, and controller support.

## Claims deliberately left open

Exact commercial/redistribution terms for selected Everything, WizTree, LatencyMon, O&O, and Wintoys artifacts have not been fully qualified here. No permission to bundle is inferred. PresentMon/CapFrameX packaged dependency notices and service footprints require artifact-specific review. No ARM64 Win11Debloat support certification, performance improvement percentage, or universal compatibility promise is made.

Historical issue anecdotes in supplied research are useful test ideas but should not be promoted to current confirmed defects without reproducing them against the selected version. Similarly, a tool author's performance/safety claim is a hypothesis for this project's measurements, not evidence that every affected Windows machine benefits.

The next valid step is to implement and qualify the native safety/measurement foundation and a small operation catalog, then evaluate the first optional adapter against those gates. Merely adopting the release numbers above does not close any execution-safety work.
