# Third-party components and references

Windows Workbench's self-contained Windows x64 preview includes Microsoft .NET 10 / WPF, Microsoft.Data.Sqlite, SQLitePCLRaw and the SQLite native library through NuGet dependencies. Exact versions and package content hashes are recorded in each project's `packages.lock.json`. Microsoft .NET and Microsoft.Data.Sqlite are MIT licensed; SQLitePCLRaw uses Apache-2.0; SQLite is public domain. Runtime distributions retain Microsoft's `LICENSE.txt` and `ThirdPartyNotices.txt` in the published directory. See the packages' accompanying license metadata and notices before redistribution.

- [.NET runtime license](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT)
- [WPF license](https://github.com/dotnet/wpf/blob/main/LICENSE.TXT)
- [Microsoft.Data.Sqlite / EF Core license](https://github.com/dotnet/efcore/blob/main/LICENSE.txt)
- [SQLitePCLRaw license](https://github.com/ericsink/SQLitePCL.raw/blob/master/LICENSE.TXT)
- [SQLite copyright statement](https://sqlite.org/copyright.html)

The catalog links to Microsoft, hardware vendors and public utility projects. Win11Debloat, PowerToys, Sysinternals, Everything, WinDirStat, PresentMon, CapFrameX, LatencyMon and Bulk Crap Uninstaller are **not bundled or executed**. These are publisher-documentation links. Their separate terms apply if a user obtains them independently. No affiliation or endorsement is implied.

The research documents record the evidence and proposed integration boundaries. A source URL, pinned revision or file hash alone is not a security or compatibility certification. This preview includes no remote script execution or automated third-party acquisition.
