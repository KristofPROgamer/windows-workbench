# Core verification

Run from the repository root:

```powershell
& .\.tools\dotnet\dotnet.exe run --project tests/Optimizer.Core.Tests -c Release
```

The console harness exits with a nonzero code if a check fails. It uses an in-memory action provider and isolated temporary SQLite journals; it does not instantiate the Windows provider or change Windows preferences.

The 75 checks cover catalog validation, assessment uncertainty, user and machine scope, approval digest and expiry, desired-state validation, exact original values and absence, concurrent writers, failure before and after mutation, verification failure, durable journal ordering, simulated interruption states, journal corruption, recovery conflicts, overlap ordering, repeated recovery, and failures while recording verification or restoration.

The journal uses schema version 2. A checksum covers immutable originals, action identity and order, device context, and run metadata. It detects corruption and ordinary edits; it is not authentication against another program with the same user's access. Mutable progress states are excluded from that checksum. Unsupported journal versions fail closed and have no automatic migration in this preview.

These checks do not establish physical power-loss durability, atomicity with Windows Registry writes, or compatibility across Windows builds. Interruption tests simulate persisted progress states and provider failures; they do not forcibly terminate a live Windows mutation process. A change by another process between the final comparison and the Registry write remains possible because the provider does not have an atomic compare-and-swap primitive. The Windows provider and app require separate qualification.
