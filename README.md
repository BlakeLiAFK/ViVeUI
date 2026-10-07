# ViVeUI

A considered way to explore Windows feature experiments. Native C# / WPF,
offline catalog, deliberate changes, and a clear record of what happened.

![Explore ViVeUI in simulated mode](docs/previews/explore.png)

## Start here

1. Download `ViVeUI-win-x64.zip` or `ViVeUI-win-arm64.zip` from the
   [Windows workflow artifacts](https://github.com/BlakeLiAFK/ViVeUI/actions/workflows/windows.yml).
2. Extract the whole ZIP into its own directory. Run `ViVeUI.exe`.
   The packages contain the .NET runtime; no runtime installation is needed.
3. Browse and search without administrator access. Use `ViVeUI.exe --demo`
   to practice safely with an in-memory fake backend.
4. Select **Windows default**, **Enable**, or **Disable**, then **Add to review**.
   Review exact IDs and before/after states, acknowledge the experimental nature,
   and apply. Only this step launches a short-lived elevated worker with UAC.
5. Restart Windows yourself when ready. ViVeUI never restarts or silently installs.

**Experiments can destabilize Windows.** This is not a recommendation to enable
unknown flags. Prepare a recovery path and prefer one experiment at a time.

## What is included

- All **17,000 unique IDs** from the pinned upstream dictionary, embedded offline.
- Virtualized search, custom ID inspection, historical example categories,
  original labeled vector schematics, and explicit unknown/unverified states.
- English, Simplified Chinese, and Spanish interface text, keyboard navigation,
  Ctrl+F search, text scaling, native accessibility controls, high-contrast support.
- Staged review; exact-snapshot history; conflict-checked, scoped restore.
- Stable GitHub release checks, optional automatic download, bounded streaming,
  trusted-host redirect checks, SHA-256 validation, and manual installation.
- x64 and ARM64 self-contained packages, corresponding GPL source in each package.

The dictionary is **not every feature in every Windows build**. It is a March 2025
upstream snapshot at [`3f8c6a3`](https://github.com/thebookisclosed/ViVe/commit/3f8c6a3425983412da1e8b26cd757c3aa17b3f25).
A name is not a verified description; a missing runtime observation is not proof
of disabled or unsupported status. There are no fabricated per-build defaults.
Unknown flags intentionally have no claimed visual preview.

## Scope and compatibility

Windows 10 build 18963 or later, x64 / ARM64. OS builds change undocumented
behavior; this compatibility floor comes from upstream and is not a guarantee
that a particular feature works. Native binary execution is validated by Windows CI;
actual system mutations are deliberately excluded from automated tests.

ViVeUI edits only **priority 8 (User), boot-persistent overrides** using the ViVe
library. It does not edit policy/security priorities, runtime experiments,
subscriptions, variants, or Last Known Good state. Advanced/unrecognized override
keys are read-only. Other priorities may supersede a user override.

**Windows default** removes the selected simple user override. **Restore** uses
the recorded previous snapshot. They are different actions. Batch operations are
not atomic: the app stops at the first failure and reports partial results.
See [safety and recovery](docs/SAFETY.md).

## Build and test

Use the .NET 8 SDK on Windows (WPF targeting required). All dependencies are
Microsoft SDK/framework components; there are no third-party NuGet packages.

```powershell
dotnet run --project src/ViVeUI.Tests -c Release
dotnet build src/ViVeUI.Windows -c Release
src/ViVeUI.Windows/bin/Release/net8.0-windows/ViVeUI.exe --smoke
dotnet publish src/ViVeUI.Windows -c Release -r win-x64 --self-contained true
```

`--smoke` runs only a fake store, exercises the actual WPF window, and exports
rendered PNG previews. Core tests also run on macOS/Linux with .NET 8 (or a newer
runtime via configured roll-forward). WPF cross-compilation does not establish
native usability; see [validation](docs/VALIDATION.md).

## Updates

Automatic checks and downloads are initially **off**. Enable them in Updates.
Downloads come from published stable releases in this repository and require the
GitHub release asset's SHA-256 digest. A missing release, missing digest, offline
network, or failed verification is an explicit failure, never “up to date”.
Artifacts from Actions are build downloads, not automatically discovered releases.
See [release process and trust model](docs/RELEASING.md).

## Sources and license

GPL-3.0-or-later. Full upstream source is vendored unmodified at `vendor/ViVe`;
its library is compiled under the SDK project. Original upstream .NET Framework
project files are retained for provenance, not used by this build.
[License](LICENSE) · [notices](THIRD-PARTY-NOTICES.md) ·
[catalog provenance](docs/CATALOG.md) · [design research](docs/DESIGN.md).
