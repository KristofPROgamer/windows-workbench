# Audit and revamp package

**4 October 2026 — audit and planning only.** This package incorporates the four supplied research reports, checks material claims against the local implementation and primary sources, and sets out a staged replacement. The legacy optimizer and its original README/guide remain unchanged. No Windows optimization, repair, package removal, driver installation or external debloat script was run.

## Start here

| Document | What it provides |
|---|---|
| [Revamp plan](REVAMP-PLAN.md) | Recommended product, native application architecture, broad feature map, profiles, recovery contract, eight delivery milestones, implementation backlog and release gates |
| [Project audit](AUDIT.md) | 27 prioritized findings with exact legacy source references, historical-report evidence, strengths worth preserving and a disposition for every legacy area |
| [Performance research](research/WINDOWS-PERFORMANCE.md) | 36 capability candidates, gaming/startup/storage/network/power/sleep analysis, tweak classification and measurement protocol |
| [Quality-of-life research](research/WINDOWS-QOL.md) | 49 Windows usability, privacy, app selection and optional productivity capabilities with applicability, tradeoffs and recovery boundaries |
| [Public tools](research/PUBLIC-TOOLS.md) | Script and utility comparison, verified research references, licensing limits, optional adapters and supply-chain qualification |
| [Research reconciliation](research/RESEARCH-RECONCILIATION.md) | Adopted conclusions, factual corrections, weakened guarantees and unresolved claims across the four reports |
| [Verification record](audit/VERIFICATION.md) | Eight isolated defect reproductions on PowerShell 5.1 and 7, limitations, reproducibility instructions and unchanged input fingerprints |

## Main conclusion

The project needs a new execution and recovery model before it gains more mutating features. The current script can override the user's DNS choice, ignore a repair opt-out, report success after suppressed errors, treat failed discovery as missing hardware, remove applications across user scopes, and proceed after a requested restore point fails. It has no durable per-action original state for targeted undo. Existing reports do not establish a measured speed improvement.

The recommended replacement is a comprehensive **Windows assessment, settings, maintenance and performance toolkit**: a native C#/.NET 10 application with a WPF interface and CLI, an unelevated user workflow, a short-lived machine executor, a versioned action catalog and durable recovery records. It should help users diagnose and improve real bottlenecks while making everyday Windows controls easier to understand.

The feature plan covers startup/background activity, gaming presentation and frame times, CPU/power/thermals, memory pressure, storage, networking, battery/sleep, Explorer/search, desktop productivity, privacy/AI settings, selected app removal, maintenance, optional utilities and recovery. These capabilities include diagnostics and guided settings when safe direct automation is not available.

Use Win11Debloat as the first **optional, restricted adapter candidate** after the native foundation is qualified. Use Sophia and WinUtil selectively as references or future qualified adapters; do not stack their broad presets. Offer PowerToys, Sysinternals, Everything, storage analyzers and performance tools according to the user's actual need. No candidate has yet passed this project's execution/VM qualification.

## Delivery order

1. Preserve the legacy baseline, set up source control and settle the action/security contracts.
2. Deliver a useful nonadmin assessment and proposal.
3. Complete a small set of reversible current-user changes with verified undo and crash handling.
4. Qualify the machine executor and recovery boundary.
5. Expand Everyday and quality-of-life features with accessible review/results screens.
6. Add selected app removal, bounded maintenance, optional tools and the restricted adapter.
7. Add repeatable gaming/performance experiments and deeper diagnostics.
8. Expand tested compatibility and pass packaging, update, security and release gates.

Eight legacy defects were reproduced using simulated Windows operations on both PowerShell 5.1 and 7. That validates the audit's control-flow evidence; it does not certify live Windows behavior. No hardware benchmark, boot/recovery test or production compatibility test was performed in this stage.

The next work is implementation of the plan's first milestones. This package does not change the current optimizer's behavior or make its existing aggressive profiles safe to run.
