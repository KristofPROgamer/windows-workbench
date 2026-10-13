# Windows performance research and action catalog

Reviewed: **2026-10-04**. Scope: a personal Windows 11 PC used for gaming and everyday work. This document is a design input, not an executable optimization recipe. No Windows settings were changed and the optimizer was not executed for this review.

The review read the complete supplied `first.md` and `second.md`, inspected the legacy optimizer and guide, and checked the material claims below against primary documentation. The supplied reports' opaque citation tokens cannot be followed independently; retained claims have usable source links here. A documented mechanism is evidence that a feature exists, not proof of a performance gain on this PC.

## Product direction

Build a comprehensive diagnostics, configuration, recovery, and experiment suite. Its useful outcomes should be shorter startup and application delays, steadier game frame delivery, fewer interruptions, sufficient storage, reliable sleep, and understandable controls. A long catalog is worthwhile; applying the entire catalog in one pass is not.

The main workflow should be **inventory → diagnose → propose → capture prior state → apply selected changes → verify → compare → keep or undo**. First launch should inventory and propose. “Default” below means a default proposal or preservation rule after the user chooses an appropriate profile; it never means changing the PC merely by opening the app.

Classification:

| Class | Product behavior |
|---|---|
| Default diagnostic/preserve | Gather relevant evidence or retain working Windows behavior. |
| Recommended proposal | Offer a narrow change with a clear mechanism, supported applicability, and undo. |
| Opt-in | A workflow, quality, power, storage, or privacy tradeoff the user selects. |
| Experiment | A hypothesis requiring a repeatable comparison; never included by a broad preset. |
| Guided/manual | The application diagnoses and links to supported controls or vendor procedures. |
| Excluded | Do not implement as an optimization action. A specific recovery operation may still be designed separately. |

## Compatibility policy

Microsoft's release table currently lists 26H2 as generally available since September 29, 2026, with build family 26300; 25H2 uses 26200 and 26H1 uses 28000. 24H2 Home/Pro servicing ends October 13, 2026; other editions have different dates. **A larger build number is not a universal feature-superset test.** Treat 26H1 as a separate compatibility lane. Release availability also does not prove this application has passed validation on that release. [Windows release information][releases]

Each action needs edition, architecture, build-family/revision, feature-presence, user-scope, and dependency checks. Unknown detection must mean **unknown/skip**, not absent/disable. Record locale, domain/management state, battery/AC, pending reboot, security state, and relevant drivers. x64 and Arm64 require separate validation; third-party binaries must match the architecture. No rule should migrate blindly from the old guide's 23H2/24H2 assumptions.

## Legacy behavior that must change

Line references below point to the reviewed legacy `Optimize-Win11.ps1`; they describe static code findings, not observed damage to the user's machine.

| Priority | Source lines | Finding and concrete consequence | Revamp disposition |
|---|---|---|---|
| P1 | 753, 1091–1094 | `ModernStandbyS0` searches the complete English `powercfg /a` text. The unavailable-states section can contain the same S0 text; localized output can fail the opposite way. A desktop then receives `PlatformAoAcOverride=0`. Microsoft does not support switching sleep models this way. | **Remove** the override. Use capability-aware detection and SleepStudy. [Modern Standby][standby] |
| P1 | 674–681, 1161–1162 | Printer detection deliberately excludes PDF/XPS/OneNote and then disables Spooler when no physical printer remains. “No physical printer” does not mean printing is unused. | **Preserve** printing, including Print to PDF. Make complete printing removal outside ordinary profiles. [Printing troubleshooting][printing] |
| P1 | 1318–1328, 1668 | Full converts `DnsProvider=Keep` to Cloudflare, including an explicit keep selection, and writes every active physical adapter. Existing local/VPN/network-specific resolution can be replaced. | **Fix** Keep as immutable user intent; DNS stays opt-in, adapter-scoped, with exact static/DHCP restoration and connectivity verification. |
| P1 | 70–108, 1208–1227 | Balanced and Full remove named apps for all users and remove provisioning. The list includes Xbox/Game Bar, Sticky Notes, OneNote, To Do, media apps, and Family. Useful features and app-local data may be lost. | **Replace** blacklist execution with exact inventory and current-user selection. Gaming dependencies and unknown data-bearing apps remain protected. |
| P1 | 98–102, 1377–1380 | Game Bar removal plus permanent core-parking changes are unsuitable generic gaming defaults. AMD's setup guidance for Ryzen 9 7950X3D/7900X3D requires Game Mode and updated Game Bar. | **Preserve** platform scheduling and gaming integrations; detect CPU/chipset dependencies first. Do not extend that specific AMD guidance to every CPU without evidence. [AMD setup][amd-x3d] |
| P1 | 1145–1149 | Everything installation causes Windows Search to be disabled; otherwise Search is changed to Manual. Everything is not evidence that the user no longer needs Windows content/Outlook indexing. | **Keep** Search; offer indexing-scope diagnosis. Microsoft recommends its normal automatic delayed-start service configuration. [Search performance][search-perf] |
| P1 | 688–716, 1168–1183 | BitLocker fallback parses English strings, detection failures collapse to false, and missing biometric/Bluetooth evidence authorizes service disabling. Temporarily absent devices and unknown states are treated as unused features. | **Keep** these services. Model present/absent/unknown separately. Microsoft marks BDESVC “Don't disable,” even in specialized IoT guidance. [Service semantics][services] |
| P1 | 119–149, 1139–1140, 1196–1198 | Baseline disables Xbox, geolocation, sharing, error reporting and other feature services, plus Family Safety and diagnostics tasks, without a user dependency model. | **Remove blanket disabling**. Privacy preferences should use supported controls. Do not infer everyday-PC guidance from fixed-function IoT guidance. |
| P2 | 1023–1041, 1052–1055, 1377–1380 | Ultimate Performance, disabling Fast Startup/desktop hibernation, and unpark-all are baseline behavior; Full bypasses the laptop-on-battery guard. Every rerun duplicates a power scheme. | **Replace** with current-plan preservation and AC/battery-aware experiments. Preserve hibernation/sleep unless selected. Do not accumulate plans. |
| P2 | 1153–1154, 1271–1284 | SSD presence disables SysMain; RAM ≥32 GB disables compression; optional pagefile sizing caps high-RAM systems at 2 GB without commit/dump analysis. | **Remove** size/media heuristics. Keep memory management; diagnose pressure. Sizing depends on peak commit and crash-dump needs. [Pagefile sizing][pagefile] |
| P2 | 1341–1372 | Full changes delayed ACK, Nagle-related registry values, NIC moderation/power/flow-control and QoS as a single “latency” bundle. English display-name matching is vendor/locale dependent. | **Remove bundle**. NIC tuning is an experiment with supported properties; no universal TCP/QoS recipe. [TCP ACK][ack], [NIC tuning][nic] |
| P2 | 1383–1387 | Optional BCD “latency tweaks” use options Microsoft describes as debugging controls. Removing a clock override does not itself mean HPET hardware was disabled. | **Exclude** from optimization. Any recovery from a known prior override must restore captured state, not invent a universal default. [BCDEdit][bcd] |
| P2 | 1103–1105, 1118–1122 | Registry writes assume policies take effect without edition/build or effective-state verification. Diagnostic data off is not supported on all consumer editions. | **Gate and verify** supported privacy controls; never promise zero telemetry. [Diagnostic data policy][telemetry] |
| P2 | 1233–1242, 1247–1254 | Recursive temp cleanup has no age/ownership/active-work/junction policy. Component cleanup runs every time; ResetBase is exposed without a rollback model. | **Replace** with scan/preview and maintenance-specific actions; irreversible cleanup must be labeled. Preserve Downloads, projects and recovery by default. |
| P2 | 1477–1488, 1555–1564, 1682–1703 | WinSAT is after-only and runs before reboot; before/after counters mix installs, cleanup, workload noise, and pending changes. `LastBootUpTime` in guide lines 716–717 is a timestamp, not boot duration. | **Replace** with representative controlled measurements and post-reboot verification. Counters remain inventory, not success scores. |
| P2 | 351–365, 484–488 | GUI defaults to Full plus Cloudflare; unattended `AssumeYes` chooses Full. A confirmation preference silently selects the most invasive scope. | **Default to audit/preview**; consent behavior must never choose a stronger action set. |

Also retire the guide's instructions to remove SSDs from scheduled optimization (lines 431–433), routinely flush standby memory (471–478), and prescribe BIOS C-state/HPET changes (663–677). These should be replaced by the diagnosis and manual boundaries below.

## Performance capabilities to build

The implementation column describes the supported surface. Where only a Settings UI is documented, ship a guided control first. A support article describing a toggle is **not** a license to invent its registry API.

### Startup, application responsiveness, and storage

| ID / capability | Default and applicability | Mechanism / implementation | Verify and undo |
|---|---|---|---|
| P-01 Startup impact explorer | **Default diagnostic** for all PCs | Inventory user startup entries, publisher, path, enabled state and observed contribution. Link to Settings/Task Manager; optional Autoruns expert inspection. [Startup controls][startup] | Compare repeated sign-in-to-usable-desktop times. No mutation during scan. |
| P-02 Selective startup suppression | **Opt-in**, exact selected nonessential apps | Use app-native startup control or a validated per-user adapter. Preserve antivirus, backup, accessibility, password managers and device utilities unless specifically selected. [Startup controls][startup] | Relaunch app and test intended features; restore exact enabled state. Delay is separately labeled from disable. |
| P-03 Background workload diagnosis | **Default diagnostic**, bounded sample | Rank sustained CPU/I/O/network/GPU work, not number of processes. WPR/WPA escalation identifies the responsible process/driver. [WPR][wpr] | Compare the same foreground task and sample duration; no automatic process killing. |
| P-04 App background permissions | **Opt-in**, apps exposing a supported control | Limit selected unused app background activity in its Settings page. Notifications/sync may change. [Windows performance guidance][perf-tips] | Check notification/sync behavior and foreground launch time; restore previous policy. |
| P-05 Browser/extension triage | **Guided opt-in** | Identify browser resource use and guide to its own task manager, extensions and sleep/background settings; do not edit opaque profile databases. | Compare real tab set, cold/warm launch and memory pressure; undo within browser. Browser/version-specific research required before automation. |
| P-06 Search workload correction | **Opt-in**, measured indexing contention | Preserve Search service. Review Classic/Enhanced and indexed folders; exclude only user-selected high-churn paths. [Search performance][search-perf] | Search known documents/mail and monitor contention; restore scope. Reindexing takes time and is not routine cleanup. |
| P-07 Storage pressure and large-item explorer | **Default scan**, delete only selected categories | Show bytes by volume/category, app/game locations and reclaim estimates. Prefer Storage cleanup surfaces. [Storage settings][storage] | Report actual freed bytes separately from speed. Deletion has no optimizer undo unless files were explicitly quarantined. |
| P-08 Conservative Storage Sense | **Opt-in proposal** | Show full retention policy; default proposed Downloads cleanup and cloud dehydration off, and make Recycle Bin retention explicit. [Storage Sense][sense] | Check saved rules; changing rules is reversible, already-deleted content is not. |
| P-09 SSD/HDD maintenance readiness | **Default diagnostic/preserve** | Verify media mapping, TRIM and scheduled Optimize Drives; invoke supported maintenance only when indicated. Windows handles media-appropriate optimization. [Storage settings][storage] | Check supported status/results; no claim that repeated TRIM boosts FPS. Avoid invalid disk joins based solely on friendly name. |
| P-10 Game storage/DirectStorage readiness | **Default information; move opt-in** | Detect actual game drive and game support; use launcher's move feature to move selected games. DirectStorage must be implemented by the game. [DirectStorage][directstorage] | Compare repeatable loading/streaming; moving back is conditional on space and launcher support. Never offer an “enable DirectStorage” registry switch. |
| P-11 Component-store analysis | **Guided maintenance** | Analyze before proposing supported cleanup. Keep repair operations separate from performance profiles. | Verify servicing outcome/space reclaimed. No ordinary preset performs ResetBase or destroys previous-OS rollback. |
| P-12 Driver and health diagnosis | **Default inventory; repair/update opt-in** | Detect failed devices/basic display drivers, record versions, link to Windows Update/OEM/vendor. DISM/SFC belong to symptom-driven repair. | Retest original fault; establish the supported driver rollback route before replacement. No bulk driver-updater installation. |

### Gaming, display and input responsiveness

| ID / capability | Default and applicability | Mechanism / implementation | Verify and undo |
|---|---|---|---|
| P-13 Windowed-game optimizations | **Recommended proposal**, compatible DX10/11 windowed/borderless games | Supported Graphics setting changes presentation path; provide per-game exceptions. Auto HDR has a dependency on this setting. [Windowed games][windowed] | Compare frame delivery and compatibility; restore previous global/per-app state and restart game. |
| P-14 Correct GPU selection | **Opt-in**, multi-GPU systems | Guide to per-app graphics preference when a game uses the wrong GPU. [Windowed games][windowed] | Check actual GPU use plus performance/power; restore Windows-decides or exact prior preference. |
| P-15 Refresh-rate mismatch detector | **Default diagnostic; change opt-in** | Compare current mode to supported modes at the current resolution. Explain mode/bandwidth/battery tradeoffs. [Refresh rate][refresh] | User-visible confirmation and timed mode recovery; restore full prior mode, not refresh alone. |
| P-16 VRR/DRR/HDR/Auto HDR | **Opt-in presentation controls** | Expose supported capabilities; separate smoothness/appearance from FPS. DRR can cap some non-VRR games. [Refresh rate][refresh], [Auto HDR][hdr] | Check image, tearing, game refresh and battery; restore prior state. No blanket “HDR off for speed.” |
| P-17 Preserve Game Mode/Game Bar | **Default preserve** | Keep supported scheduling/game integrations. Detect AMD chipset/CPU dependencies and keep capture preference separate from package removal. [AMD setup][amd-x3d] | Test game launch, overlay, capture and supported scheduling behavior; no action if already correct. |
| P-18 HAGS comparison | **Experiment**, supporting GPU/driver | Use exposed Graphics setting; retain current state until measured. GPU scheduling documentation does not prove a universal FPS gain. [Graphics settings][graphics] | Reboot as needed; fixed scene/driver/settings A/B/A; restore exact state. Note metric limitations under HAGS. |
| P-19 Overlay/capture comparison | **Opt-in experiment**, selected apps | Temporarily disable selected recording/overlay features within their own supported controls. Presence alone is not a defect. | Compare frame-time distribution and recording/voice needs; restore per-app settings. Keep Game Bar installed if platform needs it. |
| P-20 In-game latency features | **Guided opt-in**, supported GPU and game | Offer NVIDIA Reflex or AMD Anti-Lag guidance for the identified title; no injected helpers or anti-cheat bypass. [NVIDIA Reflex][reflex], [AMD Anti-Lag][antilag] | Measure supported latency metrics and frame pacing; undo in game/driver. Never copy vendor marketing percentages as a prediction. |
| P-21 FPS limits and graphics quality | **Guided opt-in**, per game | Use game-native controls for the user's target responsiveness, image quality and noise. Upscaling and generated frames must be disclosed as quality/latency tradeoffs. | Fixed scene; separate application/render FPS from displayed/generated FPS. Restore exact game settings. |
| P-22 Frame-time analysis | **Default capability, user-started capture** | Integrate a pinned optional PresentMon capture tool with explicit process selection and bounded duration. [PresentMon][presentmon] | Report median/p95/p99 frame times, long frames and sample count. Record tool version/API/HAGS caveats; no benchmark injection. |
| P-23 Audio/DPC diagnosis | **Guided diagnostic** when stutter/dropouts exist | Optional LatencyMon or WPR capture, then device/driver-specific investigation. A high DPC value is a clue, not measured input-to-photon latency. | Reproduce audio/workload fault and verify it disappears; don't disable devices/services en masse. |
| P-24 Mouse/input preferences | **Opt-in QoL** | Native pointer speed/precision and vendor-supported polling settings; no FPS claim. Preserve accessibility and user sensitivity. [Mouse settings][mouse] | Validate real input in relevant games/apps; restore exact prior state. High polling-rate experiments measure CPU/frame-time effects. |

### Memory, power, sleep and networking

| ID / capability | Default and applicability | Mechanism / implementation | Verify and undo |
|---|---|---|---|
| P-25 Commit/pressure dashboard | **Default diagnostic** | Sample commit/limit, paging, hard faults, working sets and physical/cache composition. [Pagefile][pagefile], [RAMMap][rammap] | Judge foreground latency and failures; “free RAM” alone is not a win. No cache clearing. |
| P-26 Pagefile correctness | **Preserve**; recommend system management when an artificial cap is causing pressure | Consider measured peak commit plus dump requirements, existing volumes and free space. [Pagefile][pagefile] | Confirm after reboot, repeat pressure workload, restore prior complete multi-volume configuration if selected. Never size only by installed RAM. |
| P-27 Compression/SysMain preservation | **Default preserve** | Do not disable these because RAM exceeds a threshold or the OS disk is SSD. The legacy heuristics have no validated benefit here. | Only a separately scoped troubleshooting experiment could justify a change; check warm launches and paging, not cached-RAM screenshots. |
| P-28 Power-mode experiment | **Opt-in**, separate AC/battery targets | Compare supported Balanced/current and Best performance mode; preserve OEM control and existing plan. [Performance guidance][perf-tips] | Sustained workload, temperatures, power and noise; restore old plan/settings. No repeated duplication or unpark-all. |
| P-29 Thermal/power-limit diagnosis | **Default diagnostic where reliable sensors exist; manual remediation** | Show sustained clocks, temperatures and limit flags with provider confidence; unknown sensors stay unknown. Adaptive scheduling must remain intact. [Intel hybrid guidance][intel] | Repeat sustained workload after a specific correction. Cooling, fans, hardware service and firmware remain manual. |
| P-30 Sleep/hibernate/Fast Startup | **Preserve; guided opt-in changes** | Diagnose actual sleep/resume and boot behavior. Hibernation availability is a user workflow choice, not an automatic disk-cleanup target. | Test restart, shutdown/boot, sleep/resume and peripherals separately; restore captured power/hibernate state. |
| P-31 SleepStudy and battery reports | **Default available diagnostic**, applicable laptop | Bounded native reports identify standby activity and battery trends; use WPR escalation when needed. [SleepStudy][sleepstudy] | Repeated representative sleep intervals, resume functionality and drain; no permanent monitoring by default. |
| P-32 DNS/network-path diagnosis | **Default diagnostic; DNS change opt-in** | Separate DNS lookup, local link, router, Internet path and remote service. Preserve existing DNS/DHCP by default. | Repeated cached/uncached DNS tests, packet RTT/loss under load and connectivity. Restore prior per-adapter IPv4/IPv6/DHCP state. |
| P-33 NIC moderation/offload experiment | **Experiment**, exact supporting adapter/driver | Expose documented property values and tradeoffs, not English-name assumptions. Latency and CPU/throughput objectives can conflict. [NIC tuning][nic] | Compare host CPU, throughput, loss and loaded latency; restore exact previous values. No changes on remote-use link without recovery. |
| P-34 Router/Wi-Fi guidance | **Guided/manual** | Use measured signal/link loss/loaded latency to suggest Ethernet, placement/channel or router queue-management investigation. | Repeat same endpoints/load; record topology. Do not claim a Windows registry change fixes upstream congestion. |
| P-35 CPU affinity/priority experiments | **Expert experiment only**, demonstrated scheduler contention | Prefer application fixes; avoid hard affinities on hybrid CPUs. No automatic Process Lasso rules or priority boosts. [Intel guidance][intel], [Scheduling priorities][priorities] | Per-title A/B/A with throughput, frame pacing and background functionality; restore all process settings. Realtime priority excluded. |
| P-36 Hardware upgrade/readiness advisor | **Manual recommendation after diagnosis** | Identify an actual storage, RAM-capacity, cooling or display constraint before suggesting purchases or firmware changes. | State expected affected workload, uncertainty and physical/vendor checks. No automated BIOS flash, overclock, undervolt, XMP/EXPO or security disabling. |

## Explicit exclusions and corrections

| Claim/action | Why it does not belong in a preset |
|---|---|
| HPET/platform clock/tick recipes | Microsoft labels the relevant BCD options debugging-only. No general-purpose speed claim is justified. [BCDEdit][bcd] |
| Permanent timer-resolution daemon | `timeBeginPeriod` semantics differ by Windows version, including process/occlusion behavior; higher resolution can affect performance and power. This is an application timing API, not a universal gaming switch. [Timer API][timer] |
| Global realtime process priority | It can starve essential work. Normal/High/Realtime are not a monotonic “performance quality” scale. [Scheduling priorities][priorities] |
| Memory cleaner, standby-list purge, shader-cache purge on every run | These discard reusable state. No credible default benefit was established. RAMMap is an inspection tool; application results must decide. [RAMMap][rammap] |
| “Reclaim the reserved 20% Internet bandwidth” | Current policy documentation describes reservation limits, not a constant loss of Internet capacity; its stated default also differs from the guide. Do not ship this consumer speed claim. [QoS policy][qos] |
| Force TCP ACK/Nagle values for every game | Delayed ACK and Nagle are different mechanisms; not all game traffic uses TCP. Microsoft advises study before changing ACK defaults. [TCP ACK][ack] |
| Disable Defender, firewall, mitigations, VBS/HVCI, Secure Boot or encryption for a gaming preset | This changes protection, not merely performance configuration. The user's requested safe baseline must preserve these. Memory integrity protects against malicious low-level drivers. [Device security][security] |
| Disable modern sleep, indexing, printing, biometrics, Bluetooth, Xbox, family controls or updates because they are “background” | Feature availability and a current zero-use observation do not establish that the user never needs the feature. Preserve core usability and security. |
| Registry cleaners / bulk driver cleaners / generic driver updaters | No measurable general benefit established; broad deletions remove useful recovery/configuration state. Inventory and exact supported remediation are preferable. |
| Treat privacy toggles or hidden taskbar buttons as FPS gains | Benefits are privacy, distraction reduction and appearance unless a representative benchmark independently demonstrates performance improvement. |

## Measurement protocol and promotion gates

These are proposed engineering requirements, not vendor benchmark claims.

1. **Fingerprint each run.** Store OS edition/build/revision/architecture, firmware and device drivers, CPU topology, GPU/display mode, power/AC, game/app version, security state, pending restart, capture tool/version and selected action IDs. Minimize personally identifying data; traces may contain paths/process names and must remain local unless the user explicitly exports them.
2. **Establish repeatability.** Warm up shader/application caches naturally. Repeat a representative scene/task at least three to five times; use longer capture for tail percentiles. Use A/B/A where practical. Report all samples, median and spread. Five repetitions are a proposed minimum starting point for boot validation, not a guarantee of statistical confidence.
3. **Measure the user's endpoint.** For startup, separately measure restart, shutdown/boot with Fast Startup state, login-to-shell, and login-to-usable desktop. Use WPR/WPA where required. Do not substitute last-boot timestamp or process count. [WPR][wpr]
4. **Keep game metrics honest.** Record frame-time distribution, long-frame counts, capture duration and frame count. Define the 1% low calculation. Separate generated/displayed and application frames. PresentMon documents API/HAGS-related limitations; surface them with results. Software timing is not automatically an end-to-end input-to-photon measurement. [PresentMon][presentmon]
5. **Hold confounders constant.** Keep resolution, render scale, scene, cap, VRR/HDR, driver, game version, power source and security settings unchanged except for the tested variable. Log background maintenance, shader compilation or thermal drift; don't disable protection to manufacture a clean score.
6. **Evaluate side effects.** A lower median with worse p99, lost voice capture, higher sustained throttling, broken sleep, or materially worse battery use is not an unqualified success. DNS comparisons cannot prove lower game-packet RTT.
7. **Verify after restart.** Pending-reboot changes remain pending. A successful write is not verified behavior. Check effective state, original workflow and dependent functionality after the appropriate restart/sign-in.
8. **Retain only supported conclusions.** A result inside run-to-run noise is inconclusive. No single “optimization percentage.” Show measured milliseconds, frame distributions, bytes reclaimed, battery drain or specific removed interruptions, with uncertainty.
9. **Make undo conflict-aware.** Restore only when current state still matches this tool's applied state. If another app/user changed it, stop and show the conflict. Missing registry values differ from explicit zeros; DHCP differs from a copied DNS list.
10. **Keep experiments bounded.** Temporary overlays/power/NIC/affinity changes need a time limit, visible stop, durable journal and recovery on interrupted runs. The product must survive a crash without forgetting what it changed.

Release gates: parser/static checks, pure detection/action-planning tests, mock failure/cancellation/unknown-state tests, apply→read-back→undo tests in disposable Windows VMs, then real hardware validation for gaming, sleep, NICs and GPUs. VMs cannot prove physical hardware performance or battery behavior. Real hardware lanes should include Intel hybrid, AMD single/dual-CCD X3D, mainstream desktop, Modern Standby laptop, and Arm64 for features claimed there.

## Research limitations and reconciliation

- No fresh performance measurements were made on this PC. Benefit magnitudes are intentionally unspecified.
- The supplied report treats HAGS and Game Mode similarly. Keep both as measured settings, but apply the stronger CPU-specific AMD dependency evidence to Game Mode/Game Bar.
- Microsoft IoT service-disable guidance is for specialized devices. It documents service roles; it does not authorize applying an IoT disable list to a general-purpose gaming PC.
- Older Intel/AMD/DirectStorage articles establish mechanisms and specific platform requirements. They do not prove every 2026 driver behaves identically.
- No primary evidence located establishes a universal SysMain-off, compression-off, core-unpark, NIC-flow-control-off or timer-hack gain. “Unproven as a default” is different from claiming no workload can ever benefit.
- A diagnostics feature can be fully implemented even when its remediation is guided/manual. Supported Settings pages are an acceptable first release; undocumented write adapters are not required to make the suite powerful.

## Primary sources

[releases]: https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information
[standby]: https://learn.microsoft.com/en-us/windows-hardware/design/device-experiences/modern-standby
[printing]: https://support.microsoft.com/en-US/Windows/Hardware/printer/fix-printer-connection-and-printing-problems-in-windows
[services]: https://learn.microsoft.com/en-us/windows/iot/iot-enterprise/optimize/services
[amd-x3d]: https://community.amd.com/t5/gaming/how-to-set-up-your-system-with-a-new-amd-ryzen-9-7950x3d-or/ba-p/589464
[search-perf]: https://learn.microsoft.com/en-us/troubleshoot/windows-client/shell-experience/windows-search-performance-issues
[pagefile]: https://learn.microsoft.com/en-us/troubleshoot/windows-client/performance/how-to-determine-the-appropriate-page-file-size-for-64-bit-versions-of-windows
[ack]: https://learn.microsoft.com/en-us/troubleshoot/windows-server/networking/registry-entry-control-tcp-acknowledgment-behavior
[nic]: https://learn.microsoft.com/en-us/windows-server/networking/technologies/network-subsystem/net-sub-performance-tuning-nics
[bcd]: https://learn.microsoft.com/en-us/windows-hardware/drivers/devtest/bcdedit--set
[telemetry]: https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-system#allowtelemetry
[startup]: https://support.microsoft.com/en-gb/windows/experience/startup-boot/configure-startup-applications-in-windows
[wpr]: https://learn.microsoft.com/en-us/windows-hardware/test/wpt/windows-performance-recorder
[perf-tips]: https://support.microsoft.com/en-gb/windows/experience/performance-optimization/tips-to-improve-pc-performance-in-windows
[storage]: https://support.microsoft.com/en-us/windows/experience/storage-filemanagement/storage-settings-in-windows
[sense]: https://support.microsoft.com/en-us/windows/experience/storage-filemanagement/manage-drive-space-with-storage-sense
[directstorage]: https://devblogs.microsoft.com/directx/directstorage-1-1-coming-soon/
[windowed]: https://support.microsoft.com/en-us/windows/hardware/display-graphics/optimizations-for-windowed-games-in-windows-11
[refresh]: https://support.microsoft.com/en-au/windows/hardware/display-graphics/change-the-refresh-rate-on-your-monitor-in-windows
[hdr]: https://support.microsoft.com/en-us/windows/hardware/display-graphics/use-auto-hdr-for-better-gaming-in-windows
[graphics]: https://devblogs.microsoft.com/directx/navigating-the-redesigned-graphics-settings-page/
[reflex]: https://www.nvidia.com/en-us/geforce/technologies/reflex/
[antilag]: https://www.amd.com/en/products/software/adrenalin/radeon-software-anti-lag.html
[presentmon]: https://github.com/GameTechDev/PresentMon/blob/main/README.md
[mouse]: https://support.microsoft.com/en-us/windows/hardware/input-devices/change-mouse-settings
[rammap]: https://learn.microsoft.com/en-us/sysinternals/downloads/rammap
[intel]: https://www.intel.com/content/www/us/en/developer/articles/guide/12th-gen-intel-core-processor-gamedev-guide.html
[sleepstudy]: https://learn.microsoft.com/en-us/windows-hardware/design/device-experiences/modern-standby-sleepstudy
[priorities]: https://learn.microsoft.com/en-us/windows/win32/procthread/scheduling-priorities
[timer]: https://learn.microsoft.com/en-us/windows/win32/api/timeapi/nf-timeapi-timebeginperiod
[qos]: https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-admx-qos#qosnonbesteffortlimit
[security]: https://support.microsoft.com/en-us/windows/security/windows-security/device-security-in-the-windows-security-app
