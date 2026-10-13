# Windows 11 quality-of-life, privacy and selective decluttering catalog

**Research reconciled:** 2026-10-04, Europe/Budapest. **Scope:** proposed features for personal Windows 11 gaming, everyday and laptop PCs. This is an audit/design artifact; no settings, applications or Windows features were changed to produce it.

The supplied `second.md` is a useful discovery input. Its opaque citation markers are not independently usable references, its default recommendations are not implementation approval, and several claims need qualification. The catalog below replaces those markers with primary sources and separates a documented Windows setting from a supported way for this optimizer to change it.

## Product recommendation and evidence rules

Build a substantial Windows preferences and maintenance companion alongside the performance engine. Its strongest everyday value is clearer file handling, fewer interruptions, better window/display workflows, deliberate privacy choices, selective application management and understandable recovery. These are valuable even when they do not improve frame rates.

Use distinct benefit labels: **usability**, **appearance**, **privacy**, **storage**, **reliability**, and **measured performance**. Removing an icon is appearance; disabling optional data sharing is privacy; reclaiming disk is storage. A performance badge requires a workload measurement from the protocol in [WINDOWS-PERFORMANCE.md](WINDOWS-PERFORMANCE.md).

Recommendation labels in this document refer to a **reviewable proposal**, never changes made on launch:

- **Recommend:** offer prominently when relevant; respect an explicit Keep choice.
- **Opt-in:** preserve the current value until the user selects a change.
- **Preserve:** no mutation from an ordinary profile.
- **Exclude:** do not implement as an optimizer action in this product scope.

Automation labels have a separate meaning:

- **Guide:** open a documented Windows/app page and explain the choice. A successful page launch is not a successful setting change.
- **Candidate:** a small native provider may be worth building, but must first qualify its actual API/write mechanism, effective-state readback, original-user scope and recovery on each claimed Windows build. A Support article describing a checkbox does not establish that a copied registry key is a supported API.
- **API:** an official operation exists; still test exact scope, partial failures, dependencies and recovery. This label is not a safety guarantee.

All guided items can ship before their automated providers. Microsoft's documented Settings URI scheme supports opening specific pages, with version/SKU availability constraints. Maintain an allowlist of documented destinations and a normal Settings fallback. Do not guess URIs or use them as evidence that a mutation occurred. [Launch Windows Settings][settings-uri]

Compatibility is a tuple of edition, build/update revision, feature presence, architecture, account/management state and relevant hardware. The main plan governs supported releases. Do not treat a generic Windows 11 or 26H2 label as validation of every shell/policy write. Some current policy documentation includes preview settings and edition restrictions. A policy that locks a preference for a machine is also a different product behavior from changing one user's preference. [Experience policy CSP][experience]

## Explorer, search and file workflows

| ID / proposed feature | Effect and recommendation | Setting / automation / compatibility | Tradeoff, verification and undo |
|---|---|---|---|
| Q-01 Show file extensions | Usability; **Recommend** | Explorer → View → Show → File name extensions. **Candidate** for a qualified user-scope provider; guided fallback. [Extensions][extensions] | Longer visible names; helps recognize file types but is not malware protection. Verify representative files in Explorer. Restore the captured preference. |
| Q-02 Explorer landing page | Usability; **Opt-in** | Explorer Options → Open File Explorer to → Home or This PC where available. **Guide/Candidate**. [Explorer][explorer] | Changes navigation habit and recent/cloud discovery. Reopen Explorer to verify; restore exact previous target. |
| Q-03 Frequent folders and tabs | Usability; **Opt-in** folders, educational tips available by default | Use native pinning and Explorer tabs on compatible builds. **Guide**. [Explorer][explorer], [Shortcuts][shortcuts] | Never infer private folders or replace existing pins. Verify chosen destination; removing a new pin is different from reconstructing an entire previous layout. |
| Q-04 Hidden files and preview pane | Usability; **Opt-in** | Native Explorer controls; preserve protected-system-file hiding. **Guide**. [Explorer][explorer] | Preview capability depends on installed handlers and trust restrictions. Do not bypass preview protections; restore preferences without deleting handlers. |
| Q-05 Extended context-menu commands | Usability; **Preserve** current menu | Native Show more options and keyboard reference. **Guide**. [Explorer][explorer] | No legacy-menu registry patch or Explorer injection in defaults. Verify required app command remains accessible. |
| Q-06 Index scope review | Usability/resource hygiene; **Recommend assessment**, exclusions **Opt-in** | Windows Search settings / Advanced indexing options. **Guide** initially. [Indexing][indexing] | Exclusions can make searches incomplete; show exact paths and file types. Verify representative filename/content searches after index settles; restore scope. |
| Q-07 Enhanced search choice | Usability; **Opt-in** | Classic/Enhanced or current equivalent when exposed. **Guide**. [Indexing][indexing] | Wider indexing consumes resources; account for laptop use and initial indexing. Restore prior mode, allowing index rebuild time. |
| Q-08 Default-app repair | Usability; **Opt-in**, user selects app/type | Windows Default apps page; documented per-app link where supported. **Guide**, not association-database rewriting. [Settings URI][settings-uri] | Changes affect opening files/links. Test a sample file; guide back to the recorded prior choice. Do not promise an automated undo without a qualified provider. |
| Q-09 Optional fast filename search | Usability; **Opt-in** | Optional Everything integration, qualified separately in [PUBLIC-TOOLS.md](PUBLIC-TOOLS.md). | Extra software/index/service choices need disclosure. It complements content/app search; do not disable Windows Search because it was installed. |

Search scope changes should follow evidence: demonstrate a large irrelevant indexed path or a broken/incomplete search workflow. Do not rebuild the index on every run. Preserve Windows Search and investigate specific symptoms before service changes; the index also serves dependent applications. [Indexing][indexing]

## Start, taskbar and interruptions

| ID / proposed feature | Effect and recommendation | Setting / automation / compatibility | Tradeoff, verification and undo |
|---|---|---|---|
| Q-10 Taskbar alignment/search presentation | Appearance/usability; **Opt-in** | Personalization → Taskbar. **Guide/Candidate** only for qualified individual preferences. [Taskbar][taskbar] | Preserve the user's navigation habits. Check taskbar and keyboard search; restore old values. No FPS claim. |
| Q-11 Widgets and Task View entry points | Appearance; **Opt-in**, suggested in Minimal Distraction | Documented taskbar switches. **Guide/Candidate**. [Taskbar][taskbar] | Hiding an entry point is not package removal or proof that background work stopped. Verify visibility; restore exact state. |
| Q-12 Start pins, folders and recommendations | Appearance/privacy; **Opt-in** | Personalization → Start and native pin controls. **Guide**, version-specific controls only. [Start][start] | Reduces convenient recent-item discovery. Do not rewrite layout databases or impose an invented default layout. Verify visible results; guided restoration unless exact layout capture is supported. |
| Q-13 Windows tips/welcome interruption | Distraction reduction; **Recommend proposal** | System → Notifications → Additional settings. **Guide/Candidate**. [Notifications][notifications] | Less onboarding help. Restore previous toggles; distinguish value readback from proving a future welcome screen will not appear. |
| Q-14 Notification sender review | Usability; **Recommend audit**, changes **Opt-in** | Per-app banners, sounds and notification-center preferences. **Guide** initially. [Notifications][notifications] | Missed messages/alarms are possible. Preview each sender; test important workflows and restore that sender's configuration. |
| Q-15 Lock-screen content privacy | Privacy; **Recommend in Privacy** | Hide sensitive notification content using exposed controls. **Guide/Candidate**. [Notifications][notifications] | Less glanceable information. Test with a harmless notification while locked; restore the prior choice. |
| Q-16 Do Not Disturb rules | Usability; **Gaming/Minimal proposal**, otherwise **Opt-in** | Automatic game/full-screen/schedule rules and priority exceptions. **Guide**. [Notifications][notifications] | Important banners may be silenced. Test exceptions, notification-center delivery and automatic exit; restore all changed rules. |
| Q-17 Focus sessions | Usability; **Opt-in**, user-started | Native Focus/Clock workflow. **Guide**. [Focus][focus] | User chooses session duration and breaks. No service disabling or permanent notification kill switch. End the session and restore changed preferences. |

Enterprise cloud-content/Spotlight controls require policy-by-policy qualification for the actual edition and management state. On unsupported consumer editions, present the normal UI instead of writing a policy value that may do nothing. Existing organization policy produces Managed/Unavailable, not a bypass button. [Experience policy CSP][experience]

## Desktop productivity, displays, accessibility and input

| ID / proposed feature | Effect and recommendation | Setting / automation / compatibility | Tradeoff, verification and undo |
|---|---|---|---|
| Q-18 Snap configuration | Usability; **Preserve**, offer tailored adjustments | System → Multitasking; Win+Z tips. **Guide/Candidate**. [Snap][snap] | Some users dislike individual snap gestures. Check actual window placement; restore only selected behavior. |
| Q-19 Monitor reconnect behavior | Usability; **Recommend** for docks/multiple monitors | Display → Multiple displays → remember positions/minimize on disconnect. **Guide**. [Monitors][monitors] | Depends on display topology and apps. Test undock/reconnect; preserve scaling, orientation and primary display. Restore original preferences. |
| Q-20 Virtual desktops | Usability; **Opt-in** | Task View and Multitasking preferences. **Guide** with shortcuts. [Shortcuts][shortcuts] | No automatic desktop creation or relocation of open apps. Verify current/all-desktop behavior; manual restoration when layout reconstruction is unsupported. |
| Q-21 Local clipboard history | Usability with retention tradeoff; **Opt-in** | System → Clipboard / Win+V. **Guide**. [Clipboard][clipboard] | Copied private material can remain accessible. Show local history separately from cloud sync; restore preference, not deleted history. |
| Q-22 Cross-device clipboard | Convenience/cloud privacy tradeoff; **Opt-in**, Privacy proposes off | Clipboard sync and automatic/manual sync choice. Account-dependent. **Guide**. [Clipboard][clipboard] | Explain cloud transfer/account scope. Verify configured mode; turning sync off cannot undo data already transferred. |
| Q-23 Clear clipboard history | Privacy; **explicit one-time action** | Native clear controls; **Guide/API candidate** only after scope validation. [Clipboard][clipboard] | Destructive; pinned items require separate review. Do not capture clipboard contents in optimizer logs or backups. No content undo. |
| Q-24 Text size, scale and pointer visibility | Accessibility/usability; **Preserve**, **Opt-in** changes | Native Accessibility and Display pages. **Guide**. [Visibility][visibility], [Resolution][resolution], [Mouse][mouse] | Preview readability and layout; never reset accessibility with a gaming profile. Restore full previous values; keep UI operable at high DPI. |
| Q-25 Animation/transparency preferences | Appearance/perceived responsiveness; **Opt-in** | Accessibility visual effects and documented personalization controls. **Guide/Candidate**. [Focus][focus] | Reduced motion may be preferable; transparency is a visual preference. No automatic benchmark gain claimed. Verify visible effect and restore prior state. |
| Q-26 Night light/brightness/color | Comfort/display quality; **Opt-in** | Display settings, supported hardware only. **Guide**. [Color][color] | Color-sensitive work may require different choices. Test image and schedule; restore exact prior values. HDR/refresh performance belongs in the performance catalog. |
| Q-27 Mouse behavior | Usability; **Opt-in** | Pointer speed/precision, button assignment, scrolling and visibility in native settings. **Guide/Candidate**. [Mouse][mouse] | Preserve sensitivity, handedness and accessibility. Check intended app behavior; restore all selected values. No universal gaming acceleration prescription. |
| Q-28 Sound output and volume workflow | Usability; **Opt-in**, shortcut education by default | Native Sound/volume mixer; Win+Ctrl+V where supported. **Guide**. [Shortcuts][shortcuts] | Device changes can affect calls, capture and games differently. Verify playback/mic selection in those apps; restore previous endpoints, without deleting audio devices. |
| Q-29 Selective PowerToys modules | Usability; **Opt-in install and module choices** | Official PowerToys settings; version qualification in [PUBLIC-TOOLS.md](PUBLIC-TOOLS.md). [PowerToys][powertoys] | Extra resident processes, shortcuts and updates. Measure footprint if relevant; preserve existing configuration, disable selected modules or uninstall via normal route. |
| Q-30 Shortcut reference | Usability; **Recommend education**, no mutation | Searchable small set for Explorer, Snap, screenshots, audio and desktops. [Shortcuts][shortcuts] | Show availability and existing remappings. Do not overwrite user shortcuts to match a preset. |

For PowerToys, prioritize FancyZones for complex layouts, Peek for file preview, File Locksmith for diagnosing locked files, PowerRename/Image Resizer for batch workflows, Keyboard Manager for explicit remapping, Always On Top and pointer utilities. The user chooses modules; Awake stays off unless deliberately needed. File Locksmith must never imply permission to terminate a process with unsaved work. Optional AI/cloud features require their own disclosure and must not be enabled as a side effect of installing a productivity utility. [PowerToys][powertoys], [FancyZones][fancyzones], [Peek][peek]

## Privacy, permissions, accounts and AI

| ID / proposed feature | Effect and recommendation | Setting / automation / compatibility | Tradeoff, verification and undo |
|---|---|---|---|
| Q-31 Advertising/personalization review | Privacy; **Privacy proposal** | Privacy & security → General or Recommendations & offers, according to actual build. **Guide**. [General privacy][general] | Turning off advertising ID does not remove all ads or browser tracking. Read effective setting; restore preference without claiming recovery of the previous identifier. |
| Q-32 Optional diagnostics/feedback review | Privacy; **Privacy proposal** | Diagnostics & feedback. **Guide**, edition/policy-aware. [Diagnostics][diagnostics] | Reduces optional reporting, not all Windows data transmission. Required data and security remain. Verify setting; past transfers cannot be undone. |
| Q-33 App diagnostics permission | Privacy; **Opt-in** | App diagnostics page, per account/app where exposed. **Guide**. [App diagnostics][app-diagnostics] | Some diagnostic app features may stop working; desktop apps are not governed by this control. Test chosen workflow and restore permission. |
| Q-34 Camera/microphone access audit | Privacy/usability; **Recommend audit**, changes **Opt-in** | Separate device access, app permissions, desktop-app access and browser site permissions. **Guide**. [Camera/microphone][camera] | Calls/recording/dictation may break. Do not describe the list as exhaustive. Test the actual selected application; restore the exact permission level. |
| Q-35 Location and Find My Device | Privacy/convenience; **Opt-in**, preserve by default | Native Location settings plus Find My Device review. **Guide**. [Location][location] | Disabling location can affect device finding, automatic time zone and some Wi-Fi information access. Show conflicts first; restore permission and test intended features. |
| Q-36 Browser permission handoff | Privacy; **Opt-in** | Browser-native site-permission controls. **Guide**, browser/version-specific. [Camera/microphone][camera] | Do not edit browser profile databases, extension stores or certificates. Verify chosen site's call/permission flow; restore its previous permission if desired. |
| Q-37 OneDrive/Windows Backup awareness | Reliability/cloud convenience; **Preserve**, audit **Recommend** | Display sync/backup configuration and open official apps/settings. **Guide**. [Backup][backup], [OneDrive][onedrive] | Cloud storage/account availability matters; sync can propagate deletions. Do not treat the presence of a sync client as proof that files are backed up. No silent unlink/removal. |
| Q-38 Recall state and controls | Privacy/usability; **Preserve current state** (off stays off), changes **Opt-in** | Native Recall & snapshots; keep Windows Hello authorization in Windows. **Guide**. [Recall privacy][recall] | Show unsupported/absent/unknown separately. Separate saving, filters, retention and deletion. Changing a toggle is not a guarantee that all sensitive material is excluded. |
| Q-39 Recall optional-feature removal | Storage/privacy/feature loss; **explicit advanced opt-in** | Supported Windows optional-feature UI; automate only after exact servicing contract qualifies. **Guide/API candidate**. [Recall removal][recall-remove] | Removal deletes saved snapshots; re-enabling the feature does not restore them. Machine-wide feature operation must show all affected users. Never classify as exact undo. |
| Q-40 Copilot/AI app entry points | Appearance/feature choice; **Opt-in** | Detect actual installed app and supported pin/uninstall controls. **Guide**, no old universal registry key. [Start][start], [App removal][remove-appx] | Unpin, uninstall app and disable other AI functionality are distinct actions. Preserve unrelated apps, runtimes and NPU drivers. Verify exact selected scope; reinstall is conditional. |

**Correction to supplied research:** Microsoft's camera privacy documentation explicitly says Windows Hello can use the camera for sign-in even when app-camera access is off. App permission changes must not be described as disabling Hello. Device/driver disabling and biometric-service changes are different operations and are excluded from privacy presets. Classic desktop applications also have limitations/exceptions in permission reporting and enforcement; do not promise complete camera/mic isolation. [Camera/microphone][camera]

Recall is per-user opt-in for snapshot saving on compatible Copilot+ PCs. Its local storage/security model is distinct from Copilot cloud interactions. Keep user authentication intact; do not harvest snapshots into optimizer inventory. If the user values Recall, offer filters/retention instead of assuming removal. Keep sensitive-information filtering enabled, explain it as a reduction mechanism rather than a guarantee, and use documented Windows controls. Turning saving off, deleting stored snapshots and removing the Windows feature are separate proposals. [Recall privacy][recall]

A privacy profile never disables SmartScreen/reputation checking, Defender, firewall, servicing, encryption, Windows Hello or certificate trust. It never bulk-blocks Microsoft endpoints or claims “zero telemetry.” Reports must themselves avoid exposing microphone usage, app inventory, account names or private paths by default; reveal sensitive details locally on demand and redact exports.

## Storage, updates, backup and application review

| ID / proposed feature | Effect and recommendation | Setting / automation / compatibility | Tradeoff, verification and undo |
|---|---|---|---|
| Q-41 Storage overview and cleanup preview | Storage; **Recommend assessment**, deletion **Opt-in** | Native Storage categories/cleanup recommendations; bounded optimizer-owned scan later. **Guide/API candidate**. [Storage][storage] | Estimated bytes differ from actual reclaimable space. Never select Downloads, Recycle Bin or previous Windows install implicitly; verify actual free-space change. Deleted content has no general undo. |
| Q-42 Storage Sense retention | Storage/usability; **Recommend review**, activation **Opt-in** | System → Storage → Storage Sense. Capture actual retention/cloud settings. **Guide** initially. [Storage Sense][sense] | Recurring deletion or online-only conversion is consequential. Show each category; restore configuration, not files already deleted. |
| Q-43 OneDrive offline availability | Storage/usability; **Opt-in per selection** | Free up space versus Always keep on this device. **Guide**. [OneDrive][onedrive] | Online-only files need connectivity; content search differs. Check sync/availability and known-folder protection before changes. Rehydration needs account access, network and space. |
| Q-44 Startup-app review | Startup/background performance potential; **Recommend audit**, changes **Opt-in** | Apps → Startup / Task Manager and application controls. **Guide/Candidate**. See [performance catalog](WINDOWS-PERFORMANCE.md). | Preserve security, accessibility, sync and peripheral needs. Verify login/workflows, not merely entry disappearance; restore prior launch configuration. |
| Q-45 Active hours and restart scheduling | Reliability/usability; **Recommend review** | Windows Update → Advanced options → Active hours; exposed restart scheduler. **Guide**. [Active hours][active], [Update FAQ][update] | Do not overwrite a chosen manual schedule with Automatic. Verify configured schedule; cancellation/restoration cannot reverse a restart already performed. |
| Q-46 Temporary update pause | Troubleshooting convenience; **Opt-in**, bounded | Native Windows Update pause when available. **Guide**. [Update FAQ][update] | Delays protection; show end/resume date and management constraints. Preserve update services; resume using native control. |
| Q-47 Recovery readiness | Reliability; **Recommend assessment/guidance** | Recovery/Windows Backup pages; distinguish System Restore, WinRE, file backup and newer available recovery options. **Guide/API assessment**. [Recovery][recovery], [Backup][backup] | Backup registration is not evidence of a tested restore. Do not collect recovery keys into logs. Recovery configuration changes require their own preview; no guaranteed system rollback badge. |
| Q-48 Installed-app review | Storage/usability; **Recommend inventory**, removal **Opt-in** | Current-user Installed apps / official package APIs after qualification. **API/Guide**. [App removal][remove-appx] | Show exact identity, data risk, scope and reinstall route; never auto-remove merely because bundled. Reinstall rarely restores all app state. |
| Q-49 Large files and redundant tools | Storage/usability; **Opt-in scan and selection** | Bounded file-size view/optional disk visualizer; tool choices in [PUBLIC-TOOLS.md](PUBLIC-TOOLS.md). | Size/age alone is not evidence a file is unwanted. Exclude reparse traversal by default, preserve cloud placeholders, avoid destructive duplicate-file heuristics. |

Storage Sense documentation describes defaults differently in its summary and cloud-content section. The product should read and show the actual configuration instead of asserting that OneDrive or Downloads will never be touched. Documented scope is the system drive and each user's settings; do not advertise universal cleanup of every volume/account. Retention changes are reversible settings; their later deletion effects are not. [Storage Sense][sense]

Windows Backup supports selected files/settings and app restoration flows; it is not a universal image of every application and all local state. A consumer Microsoft account and sufficient cloud storage are relevant prerequisites. A recovery-readiness card should report evidence such as configured scope, recent status and user-verified recovery access, with unknowns visible. [Backup][backup]

## Selective application policy

The following are recognition candidates from the supplied research, not an executable removal list. Resolve runtime identities against a reviewed catalog, including publisher, package family/full name, version, architecture, framework/resource status, dependencies, installed users, provisioning and uninstall support. Recheck immediately before execution.

| Recognized app/package family examples | Proposed policy and user value | Data/dependency and recovery treatment |
|---|---|---|
| Xbox app (`Microsoft.GamingApp`), Game Bar (`Microsoft.XboxGamingOverlay`) | Preserve in Everyday/Gaming; removal needs a specific user request and dependency review | Xbox/Game Pass, overlays/capture and platform-specific scheduling may matter. No “Gaming means remove Xbox” rule. Conditional official reinstall. |
| Gaming Services / Xbox identity components | Exclude from ordinary removals | Authentication/game install/launch dependencies take priority over small storage savings; no inference of non-use from an empty game list. |
| Phone Link (`Microsoft.YourPhone`) | Optional review when unused | Calls/messages/photos integration; explain lost workflow. Do not alter phone data or unlink accounts automatically. |
| Solitaire / Clipchamp | Reasonable user-selected candidates | Preserve game progress/projects/exported work; unknown app-data layout means unknown data risk. Reinstall does not prove project restoration. |
| Feedback Hub / Get Help | Preserve support capability; optional exact app review | Troubleshooting/Insider users may need them. Removal isn't a substitute for diagnostic-data settings. |
| Calculator, Notepad, Paint, Photos, Snipping Tool, Camera | Preserve ordinary utility workflows | Small potential space benefit; image/screenshot/default-app workflows matter. Camera app removal is not camera permission control. |
| Outlook / Teams variants | Explicit user choice | Accounts, cached/offline data, calls and work/school usage; identify actual product/installer rather than matching a broad name fragment. |
| Store, App Installer, framework/runtime/resource packages | Hard exclusion from normal app-removal catalog | Preserve acquisition, updates and dependent applications. No wildcard Microsoft/package-family deletion. |
| OEM utilities | Assess actual role | Power/hotkeys/fans/drivers may be tied to apparent “bloat.” Unknown dependency blocks automatic removal. |

Current-user uninstall, selected other-user uninstall and future-user provisioning removal are separate operations. Microsoft documents `Remove-AppxPackage` user/all-user scope and administrator requirements. `-PreserveApplicationData` is restricted to development/loose-file registrations; it does not provide a general consumer-app backup guarantee. [Remove-AppxPackage][remove-appx]

Provisioning controls which packages are installed for new users; ordinary removal of a provisioned package is not the same as uninstalling each existing user's instance. The DISM command also exposes an `AllUsers` parameter whose behavior must be qualified separately; do not combine it casually with the new-user-only explanation. The first release should defer these broader scopes. [Remove-AppxProvisionedPackage][remove-provisioned]

Each removal proposal must show the exact selected identity and account, observed dependencies, unknowns, possible app-data loss, package availability/version constraints, and whether recovery is manual or conditional. A Microsoft Store listing or reinstall command is not a backup. No uninstall may expand from one selected package to every wildcard match during execution.

## Profiles, conflicts and preservation

| Profile | QoL proposal focus | Explicit decisions that remain with the user |
|---|---|---|
| Everyday | Extensions, startup/storage review, optional tips reduction, usable search, recovery visibility | Explorer target, clipboard history, cloud sync, taskbar appearance, app removals |
| Gaming | Audio switching, DND review, capture/overlay preferences; performance module handles actual presentation changes | Voice-chat notifications, recording, HDR, Game Pass/dependency needs; no global service shutdown |
| Laptop | Dock reconnect behavior, display readability, sleep/battery diagnosis, offline-file planning | Cloud/offline tradeoff, location/Find My Device, OEM tools; preserve power-aware behavior |
| Privacy | Diagnostics/personalization review, lock-screen exposure, app permissions, Recall/cloud choices | Local clipboard retention, app camera access, location conveniences, existing Recall use |
| Minimal Distraction | Start/taskbar entry points, tips, sender-level alerts, focus sessions | Security/important alerts, motion/accessibility preferences, individual app choices |

Profiles may be combined only after resolving conflicting values for the same resource. Explicit action choice overrides a suggestion; Keep is a real choice. Show unresolved conflicts rather than silently changing precedence. Example: Everyday's clipboard suggestion cannot override a Privacy choice to preserve history off. A user's Manual active-hours schedule cannot be replaced simply because a new profile prefers Automatic.

Preserve by default: security and reputation protection; Windows Update/servicing; encryption/Windows Hello; recovery infrastructure; Search; printing including Print to PDF; audio, Bluetooth/Wi-Fi/VPN and controllers; accessibility; Store/App Installer/frameworks; configured sync/backup; game dependencies; OEM functions with unknown roles. “No device currently detected” and “inventory failed” never mean permission to remove its infrastructure.

## Implementation-ready acceptance contract

This document proposes breadth; the action journal and execution model are defined in [REVAMP-PLAN.md](../REVAMP-PLAN.md). Each automated QoL action must meet these gates before promotion from Guide:

1. A documented or explicitly qualified mechanism with a precise touched-resource list, supported compatibility records and correct initiating-user scope. UI availability is insufficient evidence for a registry adapter.
2. Reliable before/after detection that distinguishes absent, inherited, policy-enforced, unavailable and unknown. When readback is unavailable, report Guided/Unverified rather than Applied.
3. Capture of original typed values and existence before mutation. Exact undo may be claimed only for qualified captured state; destructive content operations have no such badge.
4. Independent effective-state verification where feasible and a small functional check: search finds a known file; an important notification still arrives; the desired audio endpoint works; a package change affects only selected users.
5. Recovery only if current state still matches the optimizer's applied state. Otherwise show an external-change conflict. Restore the original setting, not an assumed Windows default.
6. Tests covering other-admin elevation, multiple users, unavailable feature, managed policy, localization, interrupted write, duplicate execution and partial failure. Application and storage actions additionally exercise missing reinstall media, cloud placeholders and data-loss classification.
7. Accessible previews with current/proposed state, benefit class, scope, practical tradeoff, restart requirement, confidence and undo grade. No arbitrary optimization percentage or green “safe” label for an unknown state.

Suggested delivery order: **first**, read-only inventory, source-backed guide cards and preference previews; **second**, a few qualified user-setting providers with complete recovery; **third**, broader desktop/privacy controls and optional utilities; **fourth**, selective app/cleanup operations with their more limited recovery model. An unsupported automated action should remain a useful guided feature rather than holding back the entire catalog.

## Explicit exclusions and unresolved work

- Exclude legacy-menu forcing, shell patching, wildcard AppX removal, deletion of core dependencies, bulk service/task disabling and permanent Windows Update suppression from profiles.
- Exclude camera-device/biometric disabling as privacy shortcuts, hosts/firewall telemetry blocklists, deletion of recovery state for a speed score, and hardcoded old Copilot/Recall keys.
- Exclude global cache purges, recurring clipboard clearing and automatic deletion of Downloads/Recycle Bin/previous Windows installation. These discard user choices or data; a selected maintenance operation must be described honestly.
- Do not silently install a utility bundle or enable all PowerToys modules. Extra software needs a concrete user benefit and a qualified acquisition/update route.
- Open implementation questions remain for the exact native read/write mechanisms of many shell and notification preferences, per-build effective-policy resolution, preservation of complex layouts, and reliable package reinstall/data recovery. Those are acceptance-gated engineering tasks, not research-proven capabilities.

No application benchmarks or hardware changes were performed in this research. Sources establish available functionality and documented constraints; product recommendations, preservation rules and test gates are engineering judgments for this project's stated safe personal-PC scope.

## Primary sources

[settings-uri]: https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings
[experience]: https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-experience
[extensions]: https://support.microsoft.com/en-us/windows/experience/storage-filemanagement/common-file-name-extensions-in-windows
[explorer]: https://support.microsoft.com/en-gb/windows/experience/fileexplorer/file-explorer-in-windows?nochrome=true
[indexing]: https://support.microsoft.com/en-us/windows/experience/performance-optimization/search-indexing-in-windows
[taskbar]: https://support.microsoft.com/en-us/windows/experience/personalization/customize-the-taskbar-in-windows
[start]: https://support.microsoft.com/en-us/windows/experience/personalization/customize-the-windows-start-menu
[notifications]: https://support.microsoft.com/en-us/windows/experience/notifications-and-do-not-disturb-in-windows
[focus]: https://support.microsoft.com/en-us/accessibility/windows/make-it-easier-to-focus-on-tasks
[snap]: https://support.microsoft.com/en-us/windows/experience/snap-your-windows
[monitors]: https://support.microsoft.com/en-us/windows/hardware/display-graphics/how-to-use-multiple-monitors-in-windows
[shortcuts]: https://support.microsoft.com/en-us/accessibility/windows/keyboard-shortcuts-in-windows
[clipboard]: https://support.microsoft.com/en-US/Windows/Apps/using-the-clipboard
[visibility]: https://support.microsoft.com/en-us/accessibility/windows/make-windows-easier-to-see
[resolution]: https://support.microsoft.com/en-us/windows/hardware/display-graphics/change-your-screen-resolution-and-layout-in-windows
[mouse]: https://support.microsoft.com/en-us/windows/hardware/input-devices/change-mouse-settings
[color]: https://support.microsoft.com/en-us/windows/hardware/display-graphics/change-display-brightness-and-color-in-windows
[powertoys]: https://learn.microsoft.com/en-us/windows/powertoys/
[fancyzones]: https://learn.microsoft.com/en-us/windows/powertoys/fancyzones
[peek]: https://learn.microsoft.com/windows/powertoys/peek
[general]: https://support.microsoft.com/en-us/windows/privacy/general-privacy-settings-in-windows
[diagnostics]: https://support.microsoft.com/en-us/windows/privacy/diagnostics-feedback-and-privacy-in-windows
[app-diagnostics]: https://support.microsoft.com/en-us/windows/privacy/windows-app-diagnostics-and-privacy
[camera]: https://support.microsoft.com/en-us/windows/privacy/windows-camera-microphone-and-privacy
[location]: https://support.microsoft.com/en-us/windows/privacy/windows-location-service-and-privacy
[backup]: https://support.microsoft.com/en-us/windows/experience/backup-recovery/back-up-and-restore-with-windows-backup
[onedrive]: https://support.microsoft.com/en-us/onedrive/save-disk-space-with-onedrive-files-on-demand-for-windows
[recall]: https://support.microsoft.com/en-us/windows/privacy/privacy-and-control-over-your-recall-experience
[recall-remove]: https://support.microsoft.com/en-us/accessibility/windows/copilot/use-a-screen-reader-to-explore-and-navigate-recall
[storage]: https://support.microsoft.com/en-us/windows/experience/storage-filemanagement/storage-settings-in-windows
[sense]: https://support.microsoft.com/en-us/windows/experience/storage-filemanagement/manage-drive-space-with-storage-sense
[active]: https://support.microsoft.com/en-us/windows/deployment/updates-lifecycle/keep-your-pc-up-to-date-with-active-hours
[update]: https://support.microsoft.com/en-us/windows/deployment/updates-lifecycle/windows-update-faq
[recovery]: https://support.microsoft.com/en-us/windows/experience/backup-recovery/recovery-options-in-windows
[remove-appx]: https://learn.microsoft.com/en-us/powershell/module/appx/remove-appxpackage?view=windowsserver2025-ps
[remove-provisioned]: https://learn.microsoft.com/en-us/powershell/module/dism/remove-appxprovisionedpackage?view=windowsserver2025-ps
