# Audit verification record

Date: 2026-10-04. These checks verify audit evidence, not production readiness.

## Method

The legacy optimizer was not executed or dot-sourced. `Invoke-AuditChecks.ps1` parses its syntax tree and loads only named function definitions. Every reachable Windows mutation in the eight probes is replaced with an in-memory mock. A fixed SHA256 guard rejects a different source revision before loading those definitions. The harness needs a fresh review if the source changes.

The initial Windows PowerShell attempt failed before executing probes because the default parameter expression could not resolve PSScriptRoot. Moving path resolution into the script body repaired the harness. The subsequent final invocations succeeded on both runtimes.

| Runtime | Legacy syntax errors | Defects reproduced | Probe execution exit code |
|---|---:|---:|---:|
| Windows PowerShell 5.1.26100.9444 | 0 | 8 / 8 | 0 |
| PowerShell 7.6.5 | 0 | 8 / 8 | 0 |

**A zero exit code here means every expected legacy defect was reproduced. It is not a passing safety score for the optimizer.**

| Probe | Observed mocked result |
|---|---|
| DNS-KEEP | Full schedules Cloudflare DNS despite Keep. |
| HEALTH-OPTOUT | Full schedules DISM and SFC despite false health-check selection. |
| FALSE-SUCCESS | Two denied service operations are suppressed and the action reports Applied. |
| RESTORE-CONTINUE | A failed restore point records Failed and returns normally, permitting the caller to continue. |
| UNKNOWN-HARDWARE | Failed Bluetooth and biometric discovery returns false. |
| PDF-PRINTER | A PDF-only printer inventory leads to selecting Spooler for disabling. |
| POWER-DUPLICATE | Two calls create two power scheme duplicates without checking for an existing equivalent first. |
| NOOP-APPLIED | An absent AppX package reports Applied although no removal was invoked. |

Commands to reproduce from the project root, after inspecting the harness:

```powershell
powershell.exe -NoProfile -File .\docs\audit\Invoke-AuditChecks.ps1
pwsh.exe -NoProfile -File .\docs\audit\Invoke-AuditChecks.ps1
```

The mocks establish control-flow behavior. They do not prove AppX module availability in PowerShell 7, actual driver behavior, Windows UI effects, rollback completeness, or performance improvements. No live registry/service/package/network changes, repairs, benchmarks, external script execution, or driver installs were performed.

## Preserved input fingerprints

| File | SHA256 |
|---|---|
| Optimize-Win11.ps1 | `80200A67C6253C2385F4C24E5CC5A27B54458D2445F4C58734F79CF1A7FE36D3` |
| README.md | `8EB9A9810262CE6F73FC02332B3FC0AFFAB0C3C333D1EE426AEEFC80A1602551` |
| win11_debloat_guide.md | `4AC03EB5958F8276CB7752901D5659897B1E4400D79FAB729D66FF7727D02949` |
| first.md | `F75210143FA244EE59B2E2261FAB71CEF96DC9B2DAD06E8E728A6A7B467D125C` |
| second.md | `26655E76BAE070C86BBCD3BCC9147319C45FC20BB0BF04BAEEEF39A713E5BF55` |
| third.md | `496D6CA1AED72DAE830083C7C0023C9B4DD50EEC9DD32F7374BAEC4BCCCDD1E2` |
| fourth.md | `2429797D7F21ED53EC1571EF42A7B4DE5B37AB995FAC55D84F49E7A372DDF755` |

These identify local inputs and are not signatures or proof of trusted authorship. The four research files contain citation tokens from other sessions; those tokens cannot be resolved here. Retained material claims were checked against source code or linked primary references, with unverified material qualified or deferred.

## Final document checks

[DOCUMENT-CHECKS.json](DOCUMENT-CHECKS.json) records checks across nine Markdown deliverables: no broken local file links, unresolved reference-link definitions, opaque citation markers or unbalanced code fences were found. It also confirms 27 audit findings, 36 performance capability entries, 49 quality-of-life entries, and matching fingerprints for all seven preserved inputs. These are local document-integrity checks; they do not certify every external URL or turn proposed features into implemented capabilities.

Independent final reviews checked audit/source alignment and the plan's integration/recovery boundaries. Their additions were incorporated: timed restart can imply force-closing applications, and a trusted PowerShell host must also resolve privileged helper executables/modules from trusted paths.
