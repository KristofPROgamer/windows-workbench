# Windows Workbench

A native Windows 11 desktop application for inspecting a PC, reviewing suggested improvements and applying a limited set of reversible user preferences.

**Status:** Version 0.1.0 working preview according to the original documentation; real-machine changes were not exercised for publication.

## What is here

- `src/Optimizer.App`: desktop application.
- `src/Optimizer.Core`, `src/Optimizer.Windows`: planning and Windows providers.
- `src/Optimizer.Cli`: command-line interface.
- `catalog/`, `docs/`: guides, audit notes and design.
- `legacy/`: earlier PowerShell approach.
- `tests/`: simulated-provider and isolated-journal checks.

## Getting started

Use Windows and the .NET SDK selected by `global.json`.

```powershell
dotnet build WindowsWorkbench.sln -c Release
dotnet run --project src/Optimizer.App
```

This is a source distribution: the original prebuilt executable and bundled runtime are omitted. [The original preview notes](README.original.md) describe supported operations and limits; their prebuilt-delivery paths refer to the original local delivery.

## Current boundaries

Review changes inside the app before applying them. Compatibility and recovery under crashes/power loss remain qualification work in the original documentation. Hardware-specific audit outputs, local journals and SDK bundles are excluded. Third-party notices and the original legacy code are retained.

## Preservation and checks

This repository preserves a development snapshot, including incomplete work. See [PUBLICATION.md](PUBLICATION.md) for what was retained or excluded and [VALIDATION.md](VALIDATION.md) for the checks actually run. Original copyright and license notices take precedence over the repository’s display name.
