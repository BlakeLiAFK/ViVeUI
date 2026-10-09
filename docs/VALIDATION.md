# ViVeUI 0.7.0 validation

[Windows run 37892796234](https://github.com/BlakeLiAFK/ViVeUI/actions/runs/37892796234) validated source `bc329e4da9e7f447206008460f760d39bdcf8297`. The final release adds documentation and captured evidence; exact release commit and packaging run are recorded in `BUILD.json`. [Native images and reports](previews/v0.7.0/README.md) retain their source commit and per-file hashes. [Previous v0.6.0 evidence](VALIDATION-v060.md) remains available.

## Executed checks

- **163 Core tests:** feature scopes, conflicts/readback, cancellation, historical guards, exact grouped recipes, applicability, missing configuration evidence, partial failure, update metadata/hash/PE checks, file recovery and exclusive installation locks.
- **218 native WPF renders:** previous guide/manual-ID/keyboard/catalog interactions plus seven directly actionable named recipes, three applicability checkbox filters, per-ID copy and state, grouped enable/default, failure and cancellation, installation mutual exclusion, and all sixteen languages in full/compact layouts. Feature writes use fake storage only.
- **Real Windows 0.6.0 → 0.7.0 self update:** verified official old EXE digest, real embedded versions, running-original rename, replacement at original pathname, continued old process when restart is deferred, authorized new probe launch, locked original/replacement, launch-failure rollback, crash-journal recovery and paths containing spaces/Unicode. No installed user app is replaced.
- Authenticated medium-to-high integrity IPC and wrong-peer rejection, without feature writes; x64 standalone launch with no separate runtime/DLL files; x64/ARM64 PE architecture and all nine icon frames; complete source and runtime notices; four artifact SHA-256 entries.
- Sixteen resource sets with 334 common keys each and full archival catalog translations; font/glyph, RTL, selection persistence and existing catalog paging checks.

The first recipe test held a checkbox reference after WPF rebuilt its item container. The corrected test reacquires the live control and verifies every ID's state, the model, displayed checked state, binding path and data context. Native visual inspection also found stale error diagnostics after successful recovery; the implementation now clears them and a regression assertion covers it.

## Limits

No real experimental feature configuration was modified. Source posts establish purpose, IDs and exact observed builds, not compatibility with the user's device. Missing configuration observations stay Unconfirmed; even successful readback does not prove a visible feature change.

ARM64 execution, physical desktop DPI, Narrator/high-contrast interaction, native-speaker review and secure-desktop UAC consent are not claimed tested. The actual file-swap smoke does not exercise live release fetching inside the update helper; the published-release updater check separately verifies trusted live downloads without executing them. Crash recovery simulates an interrupted journal boundary, not physical power loss. New-process restart detection checks startup survival, not complete feature behavior.

## Reproduction

Run `dotnet run --project src/ViVeUI.Tests -c Release`. On Windows, `pwsh tools/New-WindowsPreviews.ps1 -Run` runs fake-storage UI fixtures. The Windows workflow additionally packages standalone executables, runs write-free elevated IPC and replaces isolated official EXE copies.

Download both CI artifacts, then run `python tools/verify_release.py ARTIFACT_ROOT EXACT_COMMIT 0.7.0`. This verifies packages and native reports without executing downloaded binaries. Release `UPDATE-VERIFICATION.json` records live GitHub download checks for both architectures.
