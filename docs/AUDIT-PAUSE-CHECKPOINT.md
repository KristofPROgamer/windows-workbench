# Audit pause checkpoint

Historical checkpoint from the user's requested pause on 2026-10-04, Europe/Budapest. The user subsequently supplied all four research reports and explicitly requested resumption. The current deliverables are indexed in [README.md](README.md); the earlier state below is preserved as history, not a current instruction to pause.

## Authorized scope on resumption

- Extensive audit and detailed revamp plan FIRST; no optimizer implementation yet.
- Audience: personal Windows 11 PCs for gaming and everyday use.
- Broad research into performance, responsiveness, quality of life, public scripts and tools.
- The user is commissioning four independent deep-research reports and will paste them into this chat.
- Evaluate their claims against evidence, deduplicate, resolve contradictions, then incorporate them into the audit and plan.

## Work completed before pause

- Read the 1,713-line Optimize-Win11.ps1, README.md, guide sections, and four existing JSON reports.
- This folder was not a Git repository at audit time; no AGENTS.md was found in the inspected workspace or ancestor locations.
- No optimizer entry point, external optimization script, or Windows mutation was run. Original project files were not edited.
- Three parallel audit/research agents were interrupted on pause. Any documents they already saved are drafts, not final deliverables.
- Added docs/audit/Invoke-AuditChecks.ps1: syntax-tree extraction of selected legacy functions with in-memory Windows-operation mocks. It never dot-sources or executes the optimizer entry point.
- PowerShell 7.6.5 reproduced all eight probes with zero source syntax errors. Confirmed means a legacy defect was reproduced, not that the optimizer passed safety testing.
- Windows PowerShell 5.1 probe invocation failed in the harness before running probes: PSScriptRoot was empty in its parameter default expression. Move default-path construction into the script body and rerun after resumption; 5.1 behavior remains unverified.
- Audited Optimize-Win11.ps1 SHA256: 80200A67C6253C2385F4C24E5CC5A27B54458D2445F4C58734F79CF1A7FE36D3.

## Findings to carry forward

1. Full overrides DNS Keep at 1318-1320, including GUI selection; CLI also overrides at 1668. Mock reproduced.
2. Full ignores disabling health checks: 1442 only skips if preset is not Full. Mock reproduced.
3. Suppressed service/task/AppX errors lead to Applied at 279; service writes 952-954, task write1005, AppX1218/1227. Service mock reproduced.
4. Restore failure is caught without gating later mutations:1015-1017,285-287,1684-1685. Mock confirmed failure returns normally.
5. Bluetooth/biometric discovery errors become false, enabling disabling decisions:702-717,1175-1183. Mock reproduced.
6. Physical-printer inventory intentionally excludes Print to PDF/XPS:674-681; Full disables Spooler at1161-1162. Mock reproduced.
7. Ultimate Performance creates a duplicate scheme on every pass:1029. Mock reproduced.
8. Absent AppX packages are reported Applied:1214-1229. Mock reproduced.
9. No durable per-action previous-state capture or rollback. Aggregate before/after counts cannot restore original settings.
10. Default AppX deletion reaches all users and provisioned image, including useful productivity/gaming apps. No per-package review or recovery contract.
11. Baseline disables broad services and tasks, including Xbox/location/sharing and Family Safety functions. Everything presence causes Windows Search disable.
12. Modern Standby detection searches all powercfg /a text, including unavailable states, and depends on English; PlatformAoAcOverride is then written for desktops.
13. Blank or failed AppX inventory becomes zero. Historical ReportOnly logs show access denied/elevation errors while reports show zero failed actions.
14. Existing latest Full report lists 111 Applied, one Failed (WizTree installation), eight Skipped, four Detected. These statuses do not prove successful postconditions or speed gains.
15. Broad temp deletion has no age/in-use/reparse-point boundary policy. Default performance, power, memory, network and service changes require evidence-driven reclassification.
16. Native return-code handling is inconsistent; winget falls back from exact IDs to names; package versions/sources are not pinned. Reporting lacks interrupted-run durability and aggregate failure exit contract.

## Research leads already inspected (reverify against incoming reports)

- Microsoft release information: https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information . At audit date lists 26H2 build26300, GA2026-09-29;26H1 build28000 is for specific new devices, not a normal upgrade from24H2/25H2. Home/Pro24H2 support end2026-10-13. Do not use a simple monotonic build cutoff as feature compatibility.
- Performance: https://support.microsoft.com/en-us/windows/experience/performance-optimization/tips-to-improve-pc-performance-in-windows
- Windowed gaming: https://support.microsoft.com/en-au/windows/hardware/display-graphics/optimizations-for-windowed-games-in-windows-11
- PowerToys: https://learn.microsoft.com/en-us/windows/powertoys/ ; investigate selective QoL modules, not blanket startup overhead.
- Modern Standby: https://learn.microsoft.com/en-us/windows-hardware/design/device-experiences/modern-standby
- Raphire: https://github.com/Raphire/Win11Debloat . Agent reported MIT; release2026.08.24 commit f431c56159bfd0b68822c583509126bab8cb94cc, inspected master bbdb9365f4d38566c93dc3e790036911fae0e877 dated2026-10-03. These are research snapshot identifiers, not approved execution artifacts or verified release hashes.
- Raphire agent findings: requires Windows PowerShell5.1; explicit CurrentUser targeting needed because default app removal may affect all users/provisioned image; independent postcondition checks necessary even with process success. Reconfirm exact CLI at chosen pin.
- WinUtil/Sophia are candidates to compare, not stack. Agent reported MIT; verify notices and per-artifact licensing before copying. Sophia has runtime/build-specific payloads and a separate third-party wrapper.
- FanControl agent reported personal/noncommercial restrictions and redistribution restrictions; use upstream licensing, never assume public availability means open source or redistribution rights.

## Intended deliverables after research returns

- docs/AUDIT.md: prioritized code findings, exact evidence, limitations, historical-report assessment.
- docs/REVAMP-PLAN.md: architecture, phased backlog, acceptance gates, compatibility matrix, migration, recovery, evidence standards and release/testing plan.
- docs/research/WINDOWS-PERFORMANCE.md: measured performance opportunities and rejected/conditional tweaks.
- docs/research/WINDOWS-QOL.md: build-aware Windows preferences and optional productivity features.
- docs/research/PUBLIC-TOOLS.md: upstream comparisons, licensing, supply-chain requirements and bounded integration contract.
- Read-only regression evidence on5.1 and7, source hashes, documentation/link consistency checks.

No release-readiness claim, complete audit, full compatibility verification, performance measurement or implemented revamp has been delivered yet.
