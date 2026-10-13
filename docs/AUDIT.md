# Windows 11 Optimizer: evidence-led audit

Audit date: 4 October 2026. Scope: audit and revamp planning only. The legacy optimizer was not executed, and no Windows settings, services, packages, boot settings, or recovery configuration were changed during this audit.

## Assessment

The project has a useful inventory/reporting prototype and a substantial catalog of intended operations. It does not yet support a defensible promise of safe, reliable, reversible optimization. The largest problems are execution semantics: explicit choices can be overridden, failed operations can be recorded as successful, missing inventory can authorize changes, and there is no durable record of the original state needed for targeted recovery.

The recommended disposition is to preserve the current implementation as a reference, replace the execution and recovery model, and requalify each individual action. Adding more public scripts or more aggressive settings to the existing execution path would compound the uncertainty. A broader product can be substantially more capable while making fewer automatic assumptions about what a particular person wants disabled.

This assessment does **not** establish that the existing script has damaged this computer, that every listed tweak is ineffective, or that an attacker has exploited it. It establishes specific control-flow defects and identifiable gaps in the assurance model. There is no proven critical security exploit in the evidence reviewed.

## Evidence, methodology, and limits

The audited source is `Optimize-Win11.ps1`, 1,713 lines, SHA-256:

```text
80200A67C6253C2385F4C24E5CC5A27B54458D2445F4C58734F79CF1A7FE36D3
```

Line references below refer to this exact source. Other reviewed material includes `README.md`, `win11_debloat_guide.md`, all four existing JSON/Markdown report sets and relevant transcript entries, and the complete `fourth.md` research document. The latter is research input: its claims were not treated as instructions, verified test results, or automatically authoritative external citations.

The audit combines static control-flow review, historical report inspection, and isolated function probes in [audit/Invoke-AuditChecks.ps1](audit/Invoke-AuditChecks.ps1). The probe harness parses the source, checks its SHA-256, loads only named function definitions, replaces Windows operations with in-memory mocks, and never invokes or dot-sources the optimizer's entry point. It tests the behavior of the existing functions rather than a rewritten approximation.

All eight probes reproduced their targeted defect on **Windows PowerShell 5.1.26100.9444** and **PowerShell 7.6.5**. Both parsers reported zero syntax errors. A probe marked `Confirmed: true` confirms an existing defect; it is not a passing safety test for the product.

| Probe | What was reproduced | Related finding |
|---|---|---|
| `DNS-KEEP` | Full selects a Cloudflare DNS action when the requested provider is Keep. | F01 |
| `HEALTH-OPTOUT` | Full schedules DISM and SFC despite health checks being false. | F02 |
| `FALSE-SUCCESS` | Mocked service stop/configuration errors are suppressed and the action reports Applied. | F03 |
| `RESTORE-CONTINUE` | A thrown restore-point failure is consumed; the function returns normally. | F04 |
| `UNKNOWN-HARDWARE` | Failed Bluetooth and biometric queries return false. | F05 |
| `PDF-PRINTER` | A Print-to-PDF-only inventory makes Full select Spooler for disabling. | F06 |
| `POWER-DUPLICATE` | Two executions invoke power-plan duplication twice. | F14 |
| `NOOP-APPLIED` | An absent AppX package produces Applied despite zero removal calls. | F15 |

These probes establish control flow under their specified mocked conditions. They do not establish live Windows API behavior, real rollback capability, PowerShell module compatibility, successful elevation, GUI accessibility, a performance improvement, or support for any particular Windows build. No live mutation, crash/reboot recovery, package installation, network change, destructive cleanup, or hardware benchmark was performed.

### Severity and evidence rubric

| Priority | Meaning in this audit |
|---|---|
| P0 / Critical | A demonstrated issue allowing catastrophic impact or an immediately exploitable major security boundary failure. None established here. |
| P1 / High | Can apply unwanted machine changes, misrepresent safety/results, lose important recovery evidence, or cause substantial functionality/data risk in a concrete scenario. Address before recommending broad mutating use. |
| P2 / Medium | Meaningful reliability, scope, reproducibility, observability, privacy, or usability weakness; fix during the revamp. |
| P3 / Low | Smaller presentation/maintenance issue with limited direct impact. |

**Proven** means directly supported by code or an isolated probe. **Historical** means observed in a supplied report/transcript, with no independent reconstruction of the earlier run. **Design risk** means a missing safeguard or a plausible consequence requiring further live/lab validation. Severity is not a claim of observed harm.

## Findings

### F01 — P1: Full overrides the explicit Keep DNS choice

**Evidence: proven; isolated probe.** `Apply-NetworkTweaks` initializes the effective provider from the selected value, then replaces Keep with Cloudflare under Full (`Optimize-Win11.ps1:1318–1320`). It subsequently selects each active physical adapter and calls `Set-DnsClientServerAddress` (`1323–1329`). The orchestration separately replaces Keep before execution when the UI was not shown (`1667–1668`), and the AssumeYes branch does so too (`484–486`).

**Trigger:** `-Preset Full -DnsProvider Keep`, or selecting Full and Keep in the GUI. The GUI path avoids the orchestration override but still hits the network function's override. A device relying on local/corporate/VPN-related DNS can therefore receive an unwanted public resolver configuration. The exact operational consequence depends on that device's network.

**Disposition:** resolve defaults once, preserve whether a value was explicitly chosen, and make Keep an unconditional no-write instruction. Preview and execution must consume the same resolved plan. Test CLI, GUI, saved profiles, and elevated handoff separately.

### F02 — P2: Full ignores the health-check opt-out

**Evidence: proven; isolated probe.** The GUI exposes the checkbox and stores its value (`395–398`, `465`), but `Invoke-HealthChecks` returns only when the switch is false **and** the preset is not Full (`1441–1445`). Full always selects both `DISM /RestoreHealth` and `sfc /scannow` (`1447–1453`). An explicit `-RunHealthChecks:$false` also cannot disable them under Full.

**Impact:** a user-facing opt-out is ineffective, and potentially lengthy repair operations run unexpectedly. These are repair operations, not merely read-only health inventory.

**Disposition:** split read-only assessment from repairs; use one resolved boolean/action selection with explicit false taking precedence. Name each operation accurately and display expected duration/side effects before execution.

### F03 — P1: Applied is not evidence of success

**Evidence: proven; isolated service probe.** `Invoke-Action` marks Applied whenever its script block returns (`263–287`, especially `279`). Several mutation helpers explicitly suppress errors: service stop/startup writes (`952–954`, `972`), task disable (`1005`), AppX removal (`1218`, `1221`, `1227`), and temp deletion (`1241–1242`). A nonterminating access-denied error can therefore be ignored and followed by Applied. The AppX fallback's catch does not catch a nonterminating error deliberately suppressed by `SilentlyContinue`.

Native command handling is inconsistent. A checked helper exists (`535–547`), but power-plan activation (`1041`), hibernation (`1055`), CPU power commands (`1379–1380`), and BCDEdit (`1385–1387`) bypass it. Under the targeted Windows PowerShell 5.1 semantics, a native process returning nonzero is not automatically a caught terminating error. `Get-CommandText` returns error text as ordinary data and does not preserve an exit-code result (`520–528`).

**Impact:** a report can claim success after failure or partial execution; later actions proceed on the assumption that the earlier step completed. A multi-write action can change one resource and fail on another while preserving neither a partial-result model nor targeted recovery.

**Disposition:** require structured apply results, action-specific native exit-code interpretation, terminating error handling where appropriate, and independent postcondition verification. Distinguish `Changed`, `AlreadyDesired`, `Failed`, `PartiallyChanged`, `PendingReboot`, and `Indeterminate`. Do not replace all success-code handling with a universal “nonzero is failure” rule: some tools use documented reboot/special-success codes that require explicit interpretation.

### F04 — P1: Requested restore-point failure does not stop dependent mutations

**Evidence: proven; isolated probe.** `Checkpoint-Computer` runs inside the general action wrapper (`1009–1018`); its error is recorded and consumed (`285–287`). Orchestration calls the restore function at `1684` and immediately begins mutations at `1685`. There is no result check, restore-point identifier verification, or separate policy for a requested but unavailable recovery prerequisite.

**Impact:** a user who left “Create a system restore point first” enabled can receive a failed restore attempt followed by the full mutating run. The audit did not reproduce a live Checkpoint-Computer failure. The ordinary exception path is nevertheless sufficient to demonstrate the control-flow defect.

**Disposition:** state the recovery contract before execution. If a requested prerequisite fails, return to review or block its dependent actions; do not silently continue. A restore point is supplementary system recovery, not a substitute for the exact original values/files required by per-action undo. Do not advertise restoration of deleted user/app data through this mechanism.

### F05 — P1: Failed inventory is converted into absence

**Evidence: proven; isolated hardware probe; corroborating historical inventory failures.** Bluetooth and biometric queries catch exceptions and return false (`702–717`). Full interprets false as permission to disable services (`1175–1186`). `Test-BitLockerInUse` can return false from an empty/suppressed query result; its fallback parses English text and also returns false if no matching text exists (`688–700`). That value drives BDESVC disabling (`1168–1172`).

The printer detector deserves a precise distinction: a terminating exception returns null (`683–684`), and null is not the intentionally supported zero-printer result. However, a nonterminating suppressed `Get-Printer` failure yields an empty array and count zero (`678–681`), enabling the disabling branch. Likewise, missing network inventory returns an empty array (`720–726`), but this primarily omits network actions; it does **not** by itself trigger a destructive “disable absent adapter” action.

Other false certainty occurs in battery queries (`619–630`), service summary counts (`777–785`), AppX summary defaults (`789–807`), and scheduled-task counts (`824–833`). Three historical ReportOnly transcripts contain AppX access/elevation errors while their reports display zero packages and no Failed actions.

**Disposition:** use a result envelope separating known-present/known-absent from unknown/unavailable, with evidence, scope, error, and observation time. Unknown must block only the dependent proposal, not be coerced to false, zero, empty, or a reassuring metric. Recheck critical prerequisites immediately before applying a reviewed action.

### F06 — P1: “No physical printer” is used to disable all printing

**Evidence: proven; isolated probe.** The printer filter explicitly excludes PDF, XPS, OneNote, Fax and several virtual ports (`674–681`). Full disables Spooler when the resulting count is zero (`1160–1166`). A machine with Microsoft Print to PDF as its only queue therefore selects Spooler for disabling. The broad name regex also excludes any real queue whose name happens to contain `PDF` or `XPS`.

**Impact:** the detector proves neither that the print subsystem is unused nor that future printing is unwanted. The probe verifies the disable decision; actual printing failure was not tested on this machine.

**Disposition:** preserve Spooler by default. Show print capabilities/queues as inventory; do not infer consent from disconnected/missing physical devices or from virtual-only printing. Treat any deliberate disabling as a separately selected capability change.

### F07 — P1: App removal silently spans existing and future users

**Evidence: proven scope mismatch.** The UI says “Remove common bundled AppX/UWP bloat” (`396`). Removal enumerates all users (`1215`), attempts all-user removal (`1218`), falls back to current execution identity on caught failure (`1221`), then removes provisioning (`1225–1227`). These are distinct scopes hidden inside one action.

The target list includes personal-use applications such as Sticky Notes, OneNote, To Do, Sound Recorder, and Phone Link, plus Xbox identity/gaming packages (`67–105`). A name appearing in this list is not evidence that the person does not use it. Store/dependency protections (`108–115`, `1209–1212`) do not make removal of the remaining packages harmless.

**Impact:** changes can affect other existing users and future profiles, while recovery may depend on package availability, registration state, licenses, and app data. This audit has not determined whether user data was deleted by any historical removal.

**Disposition:** default to a reviewable package list for the initiating user. Separate selected existing-user removal, all-user removal, and future-user provisioning into distinct explicit scopes. Preserve family/full names, version, architecture, ownership and recovery limitations. Never label reinstall as guaranteed data recovery.

### F08 — P1: Temp deletion trusts an unvalidated environment path

**Evidence: proven missing containment checks; data-loss consequence is conditional.** `Clear-TempFiles` targets a hard-coded `C:\Windows\Temp` and `$env:TEMP`; any existing value is accepted (`1233–1237`). It enumerates every immediate child and recursively removes it with force, suppressing errors (`1240–1242`). There is no canonical allowed-root validation, age policy, exclusion set, previewed file inventory, retention/quarantine, or before-state.

**Concrete scenario:** the process TEMP variable is redirected to a directory containing valuable files rather than a disposable temp directory. This code accepts that directory and requests deletion of its contents with the current elevated identity. That control flow is visible without executing it. Reparse/junction traversal behavior depends on the runtime/provider and path situation; this audit does not claim that every junction will be followed.

**Disposition:** replace this action. Prefer supported cleanup facilities where suitable; otherwise use explicitly approved roots, final-path/handle containment, reparse-point policy, age/usage exclusions, bounded enumeration, and honest deletion results. Validate the path again at use time to reduce replacement races. A restore point does not substitute for a retained copy of deleted files.

### F09 — P1: There is no targeted rollback or durable before-state

**Evidence: proven architectural gap.** Registry setters overwrite values without capturing existence, type, or data (`901–925`). Service/task setters do not save exact prior configuration (`928–1006`). The Before snapshot stores service/task counts, not per-resource recoverable state (`777–785`, `824–833`, `875–897`). Network snapshots contain displayed IPv4 servers but no DHCP/static origin or stable full recovery contract (`858–872`). Pagefile snapshot captures runtime usage rather than all original settings (`880`).

Actions exist in an in-memory list (`58`, `227–241`). Final JSON/Markdown are written only after all phases and the after snapshot (`1703–1705`). There is no recovery command. Even a successful final report cannot reconstruct most settings precisely; a crash can leave neither a complete report nor an unambiguous list of completed/partial actions. Transcript output is useful diagnostic evidence, not a transaction journal.

**Disposition:** before each reversible mutation, durably record exact original state and the intended resource, versioned action, desired state, scope, and recovery class. Journal action transitions and independently observed outcomes. Recovery must detect divergence and refuse to overwrite a later user/admin change automatically. Do not claim atomic rollback across unrelated Windows APIs.

### F10 — P1: Elevation can change which user's settings are modified

**Evidence: proven identity assumption; scenario not exercised live.** Self-elevation occurs at the top level (`202–212`), before the UI/profile and user-targeting decisions. Subsequent operations use elevated HKCU (`1071–1078`, `1086–1087`, `1114–1132`) and elevated TEMP (`1236`). No initiating SID is captured or passed to distinguish a standard user from a different administrator whose credentials were supplied at UAC.

**Impact:** with alternate-credential elevation, user-specific settings can apply to the administrator's profile instead of the initiating user's. Same-account elevation does not produce that particular mismatch. Separately, all-user AppX remains all-user regardless of which HKCU was intended.

**Disposition:** keep user-scope work in the initiating user's unelevated process; use a short-lived privileged executor for machine scope. Authenticate and bind identity across that boundary. Merely passing a caller-supplied SID to an administrator process is not a sufficient authorization design.

### F11 — P1: No meaningful compatibility, management, or maintenance preflight

**Evidence: proven missing gates; operational consequences are design risks.** OS version/build and hardware are recorded (`729–761`), but no tested build/edition/architecture allowlist or individual action compatibility check is consulted before execution (`1681–1701`). The script requires PowerShell 5.1 (`1`), which does not assert Windows 11 support. There is no domain/MDM policy ownership check, existing pending-reboot check, servicing/install conflict check, or free-space prerequisite for the journal/repair/pagefile work.

**Impact:** the same preset can attempt changes on unsupported or managed configurations and can compete with servicing/policy processes. Detection alone does not make an action safe; it must be linked to the decision and its uncertainty.

**Disposition:** gate individual action providers on documented capabilities, tested Windows/architecture ranges, policy ownership, identity, and maintenance prerequisites. Preserve useful read-only assessment on unknown builds. Do not advertise support for an OS version merely because a name appears in a research document.

### F12 — P1: Defaults disable capabilities without verifying user intent

**Evidence: proven policy breadth; benefit/harm is workload dependent.** The core list contains Xbox services, geolocation, error reporting, Insider support, Internet Connection Sharing and others (`117–134`); every mutating profile processes the list (`1139–1143`). It also targets Family Safety tasks (`148–149`, `1196–1199`). Everything installation triggers disabling Windows Search (`1145–1149`), and SSD identification triggers SysMain disabling (`1153–1155`). Tool installation occurs before the Everything re-detection and service decisions (`1687–1689`), so accepting a search utility can indirectly disable a Windows feature.

**Impact:** fewer services/processes is not equivalent to improved usability or performance. A gaming user can depend on Xbox-related functionality; a user can need content search, printing, hotspot, location or family controls despite hardware heuristics. No workload/preference checks justify these defaults.

**Disposition:** replace monolithic service lists with feature-aware recommendations. Preserve core search, printing, accessibility, security, family controls, gaming dependencies, recovery and update capabilities by default. Each optional feature reduction needs its own explanation and scope. The performance evidence review belongs alongside, not inside, the service setter implementation.

### F13 — P2: Elevation handoff loses some explicit switch values and completion semantics

**Evidence: proven.** `Convert-BoundParametersToArgs` serializes true switch parameters but drops switches explicitly bound false (`185–199`, especially `190–191`). Such a value can carry meaning: `RunHealthChecks:$false` is used to distinguish user input from defaults in `1670`. Dropping it loses that distinction. `Start-Process` does not wait for or capture the elevated child's exit code, and the parent exits immediately (`211–212`).

**Impact:** automation cannot reliably observe completion or the result of the privileged run through the original process. Explicit false/confirmation-related parameter semantics are not faithfully preserved. The separate health logic defect remains even if the switch survives handoff.

**Disposition:** use structured, validated plan handoff rather than reconstructing a command line from arbitrary bound parameters. Preserve all explicit choices and scopes. Return the actual execution outcome to the CLI caller. If compatibility requires self-elevation, define a strict serialization contract and test true, false, quoting, cancellation and failure.

### F14 — P2: Repeated runs create duplicate power schemes

**Evidence: proven; isolated probe.** The normal path calls `powercfg -duplicatescheme` first (`1028–1030`). Existing schemes are searched only when the duplication output lacks a GUID (`1032–1037`). Therefore successful repeat executions create new schemes instead of reusing a known one. There is no recorded ownership or cleanup of created GUIDs.

**Disposition:** detect desired state first. Preserve the original active plan, identify any optimizer-owned plan by a stable recorded identifier, create only when necessary, and verify activation. Recovery must not remove a plan created or subsequently modified by someone else.

### F15 — P2: No-op operations inflate Applied counts

**Evidence: proven; isolated probe; historical totals.** `Remove-BloatAppx` invokes an action for every name (`1208–1229`) even if both installed and provisioned queries are empty. The wrapper still emits Applied. Similar no-op cases exist when a hosts block already exists (`1402–1416`) or a value/service is already in the desired configuration.

**Impact:** an action count is neither the number of changed resources nor evidence of effective optimization. The historical Full report contains 111 Applied records, including app action entries far exceeding the before counts of five installed and four provisioned matching packages.

**Disposition:** plan from observed state, record `AlreadyDesired`/`NotInstalled` separately, and count verified transitions rather than attempted catalog entries. Repeated identical runs should converge without unnecessary writes.

### F16 — P2: The UI can overwrite previously supplied options

**Evidence: proven control flow.** The UI is suppressed by an explicit Preset or AssumeYes (`300–305`), not by other explicit options. It constructs hard-coded Full/Cloudflare defaults and checkboxes (`351–366`, `395–421`), then writes every control back to script parameters (`459–474`). For example, supplying a DNS or skip option without a preset does not initialize the corresponding control from that option.

**Impact:** command-line intent can be replaced by UI defaults unless the user notices and corrects the controls. Selecting Balanced in the GUI also leaves tool-install and health-check defaults selected; it can behave differently from a simple `-Preset Balanced` call. That behavior is visible, but the same profile name no longer predicts the same action set.

**Disposition:** keep one options model with provenance and use it for GUI, CLI, profiles, preview and execution. The GUI must reflect supplied options and show which profile defaults have been overridden. Avoid scattered profile logic.

### F17 — P2: ReportOnly and WhatIf have incomplete assessment contracts

**Evidence: proven scope/UX limitations; no demonstrated machine-mutation bypass.** The elevation block is unconditional on preset (`202–212`), so ReportOnly can request administrator rights before inventory. Using `-NoSelfElevate` avoids that path but may make privileged detectors fail, currently misleadingly, as the historical reports show. ReportOnly calls the same pipeline but suppresses action blocks (`271–273`), so it cannot inspect package-specific contents hidden inside those blocks and does not produce a full resolved mutation plan.

Initialization/report writing uses normal file commands outside the action wrapper (`215–220`, `1595`, `1621`). ReportOnly intentionally writes reports, so “no changes” should mean no configuration mutations, not zero filesystem activity. PowerShell WhatIf can also suppress supporting file cmdlets, complicating report creation. A clean WhatIf path does not set NeedsReboot inside skipped action blocks; this audit does **not** claim that ordinary WhatIf currently reboots the machine.

**Disposition:** make assessment a first-class read-only path with documented artifact writes, optional scoped elevated inventory, truthful unavailable results, and a concrete plan. Test WhatIf/preview at the whole-program boundary rather than assuming a wrapper covers every side effect.

### F18 — P2: Reports can disappear on failure and have no stable machine contract

**Evidence: proven.** `Initialize-Run` happens before the try/finally (`1673–1674`). The finalizer only stops transcription (`1708–1709`). An error before final serialization, including during the after snapshot, skips both structured reports (`1703–1705`). Markdown and JSON are separate non-atomic writes (`1595`, `1621`). Timestamps have second precision and are used as filenames (`60–65`); two starts within the same second can select the same paths. There is no report schema version or overall structured exit-status contract (`1608–1619`, `1713`).

**Impact:** interrupted/failed runs can be harder to diagnose than successful runs, despite needing better evidence. The final summary omits counts of failed/indeterminate actions (`1624–1640`). Handled failures do not establish a nonzero process exit code.

**Disposition:** persist the run header and per-action outcomes as they occur, write a final summary in all terminal paths, use collision-resistant run IDs and atomic export, version schemas, and define caller-visible exit codes. Retain partial results even if final inventory is unavailable.

### F19 — P2: External installation is broader and less deterministic than the proposal

**Evidence: proven selection/trust design; no observed malicious package.** WinGet's primary path uses exact IDs (`560–575`), but no explicit source/version policy. On any caught error, the fallback uses names without `--exact` (`577–596`). A failed installation can therefore change selection strategy instead of preserving package identity and reporting the actual failure. Full proposes twelve tools (`153–165`, `1420–1438`), including overlapping diagnostics and persistent utilities, without per-package review in the GUI.

The optional update path installs a package provider and the currently resolved PSWindowsUpdate module globally, permits clobbering, and imports it elevated (`1462–1472`). It does not pin the module version/repository in this code. That adds a third-party code trust boundary to a Windows maintenance operation.

**Disposition:** make tools a separate reviewed catalog with fixed IDs/sources and approved version/publisher policy; do not fall back to a looser match. Treat downloaded public scripts as audited, pinned, separately scoped integrations. No remote text evaluation, floating branch execution, or generic privileged command injection. A pinned hash protects integrity against the expected bytes; it does not prove those bytes are safe or trustworthy.

### F20 — P2: Reports expose identifying local inventory by default

**Evidence: proven fields and local artifacts.** Detection includes computer/user name (`737–738`) and adapter MAC addresses (`754`). Startup inventory includes full commands and locations (`811–817`). JSON serializes those objects (`1608–1621`); transcripts can include installation output and local paths. The existing report files contain such machine-specific data.

**Impact:** sharing this repository or attaching reports can reveal identities, personal paths, installed software and startup details. There is no evidence of automatic upload or exfiltration in the audited script. This is a local retention/export concern, not a claim of network disclosure.

**Disposition:** keep private recovery state separate from redacted support exports, add a previewable export policy, avoid secrets/full command lines when unnecessary, and exclude generated local reports from source distribution. Test redaction on realistic adversarial strings; avoid relying solely on simple username replacement.

### F21 — P2: Pagefile changes can be partial, and the “kept system-managed” note is unverified

**Evidence: proven control flow; impact depends on the host.** The pagefile action disables automatic management first (`1285–1287`), then creates/updates only the first matching setting (`1289–1295`). A later failure can leave automatic management disabled while the intended sizes were not applied. There is no rollback or disk/dump/peak-commit prerequisite. The size rule uses RAM capacity alone (`1283–1284`).

When the switch is absent, the note says “Pagefile was left system-managed” (`1300`), but no query established that the original configuration was system-managed. The script actually leaves it unchanged.

**Disposition:** preserve existing/system-managed configuration in normal profiles. A deliberate manual change needs complete original settings across volumes, capacity/workload/crash-dump evidence, a verified apply/recovery sequence and accurate unchanged messaging. Never equate the generic RAM heuristic with a safe universal size.

### F22 — P2: Native operations have no timeout, cancellation, or safe interruption policy

**Evidence: proven missing controls.** The native wrapper invokes a command synchronously and waits without timeout (`535–547`). There is no cancellation token/state, child-process ownership, operation duration bound, or resumed-reconciliation mechanism. DISM, SFC, Windows Update, WinGet and benchmarks can be long-running (`1247–1255`, `1441–1488`).

**Disposition:** define an operation-specific interruption policy. Stop scheduling new work on cancel; safely wait or cooperatively cancel active work. A process timeout is not proof that a Windows installer/servicing operation stopped. Do not indiscriminately kill servicing processes and label the action rolled back. Persist `Indeterminate`/pending state and reconcile with the authoritative subsystem before further writes.

### F23 — P2: Concurrent runs can race on machine state and reports

**Evidence: proven absence of run coordination; race impact not reproduced.** There is no global mutation lock, run ownership/lease, or collision detection around the sequence at `1659–1713`. Multiple runs can take different before snapshots and interleave service, package, DNS, cleanup, installer and report operations. Same-second report names compound this (`60–65`).

**Disposition:** serialize mutation/recovery runs, while allowing nonintrusive assessment where safe. Persist enough state to distinguish a completed prior run from an interrupted one. A lock that prevents two optimizer instances does not prevent Windows, Group Policy or another administrator from changing the same resource; action verification/divergence checks remain necessary.

### F24 — P2: Localization and storage assumptions undermine detection and targeting

**Evidence: proven assumptions; not all consequences reproduced.** BitLocker fallback parses English text (`698–699`); Modern Standby detection matches an English phrase (`753`); power-plan fallback searches the English display name (`1034`); NIC settings use English display names and values (`1353–1365`). Disk matching also falls back to a nonunique friendly name (`643–645`).

Temp cleanup hard-codes `C:\Windows\Temp` (`1235`), WinSAT hard-codes drive `c` (`1484`), while disk inventory correctly uses SystemDrive (`836–842`). Reports nevertheless label that metric C: (`1559`, `1635`). A Windows installation on another volume can therefore target/describe the wrong disk in those areas.

**Disposition:** prefer stable APIs/identifiers and explicit detected system paths. Record ambiguity instead of guessing. Test non-English installations, multiple identical disks, alternative system volumes, and per-driver adapter capabilities. A recognized property name does not guarantee the selected display value is valid for that driver.

### F25 — P2: Hosts matching can mistake comments or different mappings for an applied block

**Evidence: proven matching logic; no live hosts edit performed.** A host is considered already handled if its text appears as a whitespace-delimited token anywhere in existing lines (`1404`, `1408–1410`). The code does not ignore comments or check that the address is the intended block address. For example, a commented-out line containing the hostname can prevent insertion. The action still reports Applied when no lines are added (`1414–1416`, `279`).

**Disposition:** if retained, use an explicit owned block with parsed active mappings, preserved encoding/newlines, exact backup and conflict reporting. Hosts blocking changes name resolution, not a verified comprehensive telemetry boundary; explain the limited scope and possible service effects.

### F26 — P1: Timed restart can force-close apps, and reboot state is not verified

**Evidence: proven.** A few actions set one NeedsReboot flag (`1060`, `1094`, `1274`, `1296`, `1388`, `1473`), while others may have deferred effects. It is not initialized from the machine's existing pending state and there is no post-reboot verification. AutoReboot schedules `shutdown.exe` directly (`1645–1648`) without checking native result; reports are written beforehand (`1704–1706`). It also runs after handled action failures if the flag is set.

The command at `1647` uses `/r /t 30`. Microsoft documents that a timeout greater than zero implies `/f`, which forces running applications to close and can discard unsaved data. Thus selecting AutoReboot can cause loss of unsaved work even though the script never writes an explicit `/f`. This is a conditional consequence of that selected option, not a claim that an ordinary dry run reboots the PC. [Microsoft shutdown command reference](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/shutdown).

**Disposition:** distinguish required/requested/scheduled reboot and before/after-boot verification. Save the reboot decision and outcome durably, provide a clear opportunity to preserve work and cancel, and resume only the already-reviewed verification plan. Normal restart behavior must not imply force-closing applications through timeout semantics. Do not claim the report proves boot-time settings became effective. Ordinary WhatIf reboot safety is discussed separately in F17.

### F27 — P2: Existing measurements cannot substantiate performance claims

**Evidence: proven measurement design; historical sample.** Snapshots count processes and free memory/disk (`875–897`). Optional WinSAT runs only after the changes, and its structured results are not captured for a baseline comparison (`1477–1489`, `1701`). There are no controlled workloads, repeated trials, warmup, noise estimates, or power/temperature controls. Tool installs, cleanup and repairs are interleaved with settings, making causal attribution impossible.

In the supplied Full report, process count increases 206→208, startup entries 10→11, free RAM falls 5.99→4.23 GB, and disk free space increases 22.03→29.07 GB (`reports/win11-optimizer-20260624-205414.md:29–38`). These are useful observations, **not proof of either performance improvement or regression**. The elapsed work, installer activity, background processes and cache changes are uncontrolled.

**Disposition:** label these as inventory/resource deltas. Require repeatable workload-specific evidence for a speed claim, including relevant frame times/latency/throughput, power and functionality. Keeping useful cache or a required service can improve experience despite making a counter look worse.

## Historical reports: what they establish

Four pre-existing report sets were examined. They are historical artifacts, not fresh audit runs, and the source content used to create each historical set cannot be independently proven from those reports alone.

| Run suffix | Profile | Recorded actions | Applied | Failed | ReportOnly |
|---|---:|---:|---:|---:|---:|
| `20260624-202407` | ReportOnly | 97 | 0 | 0 | 84 |
| `20260624-204256` | ReportOnly | 99 | 0 | 0 | 84 |
| `20260624-204622` | ReportOnly | 99 | 0 | 0 | 84 |
| `20260624-205414` | Full | 124 | 111 | 1 | 0 |

The remaining entries have Detected/Skipped statuses. No personal identity, MAC address, startup command, or user-specific path is reproduced in the table.

The three ReportOnly logs contain access-denied/elevation errors during all-user AppX inventory. For example, `reports/win11-optimizer-20260624-204622.log:26–30` and `128–132` record failures, while its Markdown at `36–37` reports zero installed/provisioned matching packages. This is direct historical evidence that zero currently means either genuine absence **or** failed inventory.

The Full report records a WizTree installation failure (`reports/win11-optimizer-20260624-205414.md:64`) and continues to later actions. The recorded decrease in app/service/task counts suggests substantial machine changes were attempted and some observed state changed. It does not validate each Applied record, original user scope, functional compatibility, rollback, or a speed gain. No complete recovery journal, post-reboot verification report, or controlled benchmark is present.

## Strengths worth preserving

- A single action wrapper already centralizes much of the reporting, ReportOnly behavior and ShouldProcess support (`263–287`). It is a useful seam for migration, although its success semantics must change.
- Explicit switches exist for several especially consequential operations: ResetBase, pagefile sizing, BCDEdit, hosts edits and updates (`37–40`, `407–413`). Those actions are not silently part of the unmodified Full default.
- The code includes a protected AppX dependency/store list (`108–115`, `1209–1212`) and deliberately avoids automatic remote PowerShell launchers (`1438`). Both are sensible principles to retain and strengthen.
- Some hardware awareness exists: battery behavior, system drive type, BitLocker, adapters, printing and biometrics (`619–761`). A typed uncertainty model can build on this inventory intent without preserving unsafe inferences.
- A checked native runner exists (`535–547`), and primary WinGet selection uses exact package IDs (`564–565`). Both demonstrate a direction worth making consistent.
- JSON, Markdown and transcripts serve different diagnostic audiences (`1517–1621`). The revamp should retain these outputs with versioning, redaction, failure durability and truthful results.
- The README acknowledges important tradeoffs for ResetBase, hosts, pagefile and BCDEdit (`README.md:113–119`). The new documentation should extend that candor to default actions and distinguish preference from measured optimization.

## Disposition of every legacy area

These dispositions are recommendations for the revamp, not changes already made. “Experimental” means excluded from ordinary recommended profiles until action-specific compatibility and benefit evidence exists.

| Legacy area and source | Recommended disposition | Required safeguard/evidence |
|---|---|---|
| CLI, preset and GUI options (`20–45`, `300–505`) | Replace scattered defaults with one intent model. | Explicit choices, including false/Keep, win; CLI and GUI produce identical plans. |
| Elevation/argument handoff (`179–213`) | Replace with scoped privileged execution. | Original-user targeting, authenticated handoff, reviewed-plan binding, wait/exit semantics. |
| Inventory (`601–898`) | Retain concept; rewrite contracts. | Known/absent/unknown/unavailable, stable identifiers, privilege/locale coverage. |
| Action wrapper and reporting (`227–288`, `1517–1657`) | Replace result semantics and persistence. | Per-resource snapshots, verification, partial failure, durable journal and redacted exports. |
| Restore point (`1009–1018`) | Retain as optional supplementary recovery. | Verified result and explicit failed-prerequisite policy; no data-backup promise. |
| Ultimate Performance (`1020–1047`) | Optional workload/power preference. | Owned idempotent plan, original plan restoration, battery/thermals/standby qualification. |
| Fast Startup/hibernation (`1049–1061`) | Split separate preferences. | Preserve hibernation unless specifically selected; show reboot and disk/battery implications. |
| Visual effects/menu timing (`1070–1079`) | Optional current-user quality-of-life settings. | Exact original values, accessibility review; do not bundle low-disk warning suppression. |
| Delivery Optimization (`1081–1083`) | Optional bandwidth/privacy policy. | Managed-device policy ownership, documented edition/build behavior. |
| GameDVR/capture (`1085–1089`) | Explicit user feature choice. | Explain loss of recording/capture; verify chosen scope and supported policy behavior. |
| Modern Standby override (`1091–1099`) | Quarantine from normal product. | Supported platform-specific rationale and recovery lab evidence before any offer. |
| Privacy registry settings (`1102–1134`) | Review and port individually. | Current documented policy applicability, correct SID/scope, distinguish request from actual effective state. |
| Core services/tasks (`117–150`, `1136–1199`) | Replace blanket lists with capability choices. | Preserve gaming/search/printing/family/security dependencies; verify actual need. |
| Search/SysMain (`1145–1158`) | Preserve by default. | Utility/SSD detection is insufficient evidence of equivalent functionality or benefit. |
| Spooler/BitLocker/Bluetooth/biometric services (`1160–1187`) | Preserve by default. | Absence is neither consent nor permanent disuse; unknown detection blocks changes. |
| AppX removal (`1202–1230`) | Port selected current-user scope first. | Exact packages, review, app-data warning/recovery grade, separate other/future-user operations. |
| Temp deletion (`1233–1244`) | Replace fully. | Validated roots, race/reparse handling, exclusions, age rules and truthful deleted-byte results. |
| DISM component cleanup (`1247–1260`) | Separate maintenance action. | Servicing prerequisites, operation-specific cancellation and result semantics. |
| DISM ResetBase (`1252–1255`) | Irreversible expert maintenance only. | Explicit impact and stable-update prerequisite; no rollback guarantee. |
| TRIM (`1262–1265`) | Assess configuration first. | Supported filesystem/device semantics, no-op detection; do not imply immediate performance improvement. |
| Memory compression (`1271–1279`) | Preserve default; experimental only. | Workload evidence, commit/fault measurements, verification/recovery. |
| Manual pagefile (`1281–1301`) | Exclude automatic size heuristics. | Peak commit/dump/storage evidence and complete per-volume recovery. |
| DNS (`1304–1339`) | Explicit optional network preference. | Keep means Keep, chosen adapter, DHCP/static/IPv4/IPv6 recovery and managed-network protection. |
| Nagle/NIC/QoS (`1341–1374`) | Exclude from normal profiles; review individually. | Documented applicability and workload evidence, stable driver identifiers, functional regression tests. |
| CPU parking (`1377–1381`) | Experimental or explicit power preference. | AC/DC/device qualification, benchmark plus power/thermal impact, plan recovery. |
| BCD timer options (`1383–1393`) | Lab-only; no consumer default. | Boot recovery, complete prior BCD state, checked commands, objectively measured improvement. |
| Hosts telemetry block (`1396–1417`) | Optional advanced policy, if retained. | Parsed owned block, backup, conflicts, limited efficacy claim and service-compatibility tests. |
| External utility stack (`153–165`, `549–599`, `1420–1438`) | Separate tools catalog. | Deterministic identity/source/version policy; per-tool review and supported architecture. |
| DISM/SFC repair (`1441–1453`) | Separate repair workflow. | Honor opt-out, explain repair vs scan, servicing health and result reconciliation. |
| PSWindowsUpdate (`1456–1474`) | Exclude third-party module from core path. | Prefer supported Windows update interface; if retained, separately audit/pin integration. |
| WinSAT (`1477–1489`) | Diagnostic only until meaningful baseline comparison. | Correct target drive, parsed retained results and honest benchmark limits. |
| Reboot (`1642–1655`) | Explicit durable lifecycle phase. | Requested/scheduled/required distinctions, failures/cancel, post-reboot verification. |
| README/legacy guide | Preserve as historical input; replace product claims. | Action-specific documentation, evidence dates, support policy, recovery limits. |

## Review of the supplied architecture research

`fourth.md` correctly prioritizes explicit intent, typed uncertainty, durable before-state, verification, recovery classes, original-user targeting and a small qualified initial catalog. Those principles are supported by the code findings above. Several formulations need tighter engineering limits before becoming implementation requirements:

1. **A hash is not authorization.** The plan hash/pipe outline (`fourth.md:133–149`, `298–320`) is an incomplete trust protocol. A named-pipe user ACL and nonce do not on their own authenticate the trusted UI, validate arbitrary target resources, or bind alternate-credential UAC to the original human review. The executor must validate fixed action/resource allowlists, parameter bounds, identity/session, freshness, replay protection, catalog compatibility and the exact reviewed plan. Avoid a generic admin “registry/file command” provider exposed to an untrusted caller.
2. **A compiled application is not inherently safer.** Compilation can improve deployment and typed contracts, but does not supply correct privilege boundaries, state transitions or safe providers. Demonstrate one complete low-risk action with recovery before scaling into the extensive multi-project layout (`165–238`). Public-script compatibility can remain optional; it must not become a hidden privileged command escape hatch.
3. **The proposed fingerprint guard is not atomic ownership proof.** `current == expected-after` (`546–575`) cannot detect an external writer that returned the value to the same content, and a check followed by a write can race. Call it a divergence guard and define its limits. Use stronger subsystem concurrency/identity mechanisms where available; otherwise minimize the gap, recheck and refuse ambiguous recovery. Do not promise exact compare-and-swap across arbitrary Windows resources.
4. **SQLite commits are not Windows-wide transactions.** WAL mode (`469–531`) is not a complete crash/power-loss design. Specify durability settings, filesystem flush behavior, blob publication order, ACLs, disk-full/corruption handling and recovery after the Windows write succeeds but the journal update fails. Service runtime activity, installs and servicing have side effects beyond a scalar configuration value.
5. **Process termination is operation specific.** A Job Object (`583–599`) is useful containment but does not mean terminating an installer/DISM client cancels service-owned work safely. Reconcile active operations and permit an indeterminate result. The note that a specific operation may accept documented nonzero success codes also conflicts with the absolute wording at `671`; keep the operation-specific rule.
6. **User-scope recovery needs its own ownership design.** An unelevated user writer and an admin-owned ProgramData journal (`103–159`, `471–481`) need an explicit way to capture/read the right user's before-state without exposing arbitrary broker writes or letting untrusted journal entries drive privileged recovery. Define private user state and protected machine state deliberately.
7. **“Exact recovery” needs a precise boundary.** Service startup mode restoration is not restoration of every runtime effect of stopping/starting a service. DNS restoration must include dynamic/static origin, adapter identity, both families and relevant policy. App reinstall does not recover all user data. Do not generalize the registry scalar example into a promise of complete machine rollback.
8. **Third-party code requires a distinct assurance class.** An audited/pinned public script still performs operations outside a narrow provider contract unless its individual actions have been adapted and verified. Do not assign an opaque elevated subprocess the same granular recovery or verification guarantee as a native action provider.
9. **Support claims must be reverified.** `fourth.md` contains opaque citation tokens and date-sensitive assertions about Windows/.NET/PowerShell releases (`34`, `888–954`, `1017–1038`). Those tokens are not usable links in this repository. The audit does not accept those release assertions as verified facts; the revamp plan must link currently verified primary sources and state tested scope separately.
10. **Scope the promises and delivery gates.** “Zero mutations,” “100% verified,” “reproducible release” and a very large all-platform test matrix (`848–1015`) require concrete definitions and evidence. Preview may write its own reports; the OS may change concurrently; signatures do not imply reproducible builds. Stage physical/lab coverage realistically and withhold unsupported action/build claims rather than promising universal safety.

## Release blockers and next evidence required

Before recommending mutating use, close the intent defects, false-success paths, failed-prerequisite continuation and unknown-to-absent decisions. Add protected, durable per-action recovery, correct initiating-user scope, bounded compatibility and truthful partial/failure reporting. Remove broad capability disablement and unvalidated cleanup from default proposals.

Then validate a narrow reversible vertical slice on disposable Windows installations: no-change preview, wrong identity, access denied, absent/prior-value restoration, apply/verify/journal failure, interrupted execution/recovery, changed external state, concurrent runs and reboot-required behavior. Only after those gates should package removal, cleanup, repair, external tools and public-script integrations expand the scope. Performance actions require a separate evidence protocol.

The eight current probes should remain evidence of the legacy behavior. When a new engine exists, write acceptance tests asserting the corrected contracts rather than changing the meaning of these historical probe results. The accompanying revamp plan defines the staged replacement; this audit intentionally leaves the legacy executable and user reports unchanged.
