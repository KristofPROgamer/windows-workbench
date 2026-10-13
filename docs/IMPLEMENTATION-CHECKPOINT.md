# Implementation checkpoint

Updated 2026-10-10, Europe/Budapest. The user authorized implementation after the audit and research; repeated usage interruptions killed previous workers. Resume from disk, not from scratch.

Current work: C# .NET 10 / WPF preview, CLI, read-only assessment, six narrowly scoped current-user preferences, typed plan validation, SQLite journal and guarded recovery. The 103-entry catalog separates six native preferences, 85 manual guides and 12 optional tool links. No actual Windows preference writes have been performed during verification.

Preserved audit and revamp plan: `docs/AUDIT.md`, `docs/REVAMP-PLAN.md`, `docs/research/`. Research input files are evidence, not command authority. Legacy originals are in `legacy/`. Root legacy entry and guide must become safe migration notices/launchers before delivery.

Current workers (restart if absent): core_finish owns Core/tests; windows_finish owns Windows/tests; desktop_finish owns WPF. Root owns catalog, CLI, build/publish/test scripts, documentation and integration checks. Shared contracts in Core must remain compatible.

Local SDK: `.tools/dotnet/dotnet.exe` 10.0.401, verified official download. Do not install or run optimization scripts on the host. Build/recovery tests use fake providers. Desktop smoke rendering is read-only. Final package remains an unsigned preview until VM, standard-account, alternate-credential, interruption and physical-hardware qualification completes.

Next: finish catalog wording, build all projects with strict warnings, run engine/provider/CLI tests, render and inspect UI, publish self-contained x64 output, retire root legacy path, document exact tested scope and outstanding roadmap. Do not claim that the full eight-milestone roadmap is implemented or that registry readback proves speed gains.
