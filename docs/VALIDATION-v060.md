# ViVeUI 0.6.0 validation

The immediate checkbox, unified card gallery and manual-ID implementation was
validated by [Windows run 37889154249](https://github.com/BlakeLiAFK/ViVeUI/actions/runs/37889154249)
at source commit `850798ab335504a28ab65999b7342dc20d47e950`.
Screenshots and reports committed under `docs/previews` come from that run.
The later release commit adds only documentation and captured evidence; its exact
commit and successful packaging workflow are recorded in the release `BUILD.json`.

## Executed checks

- 132 core tests: override conflict/read-back, cancellation, duplicate suppression,
  isolated feature scope, historical guards, trusted update downloads, unified
  search/filter deduplication, 17,000-ID performance, strict manual-ID parsing,
  repeated/latest-query handling, invalid input and close/cancellation suppression.
- Native WPF direct enable/disable/default operations through real checkbox automation
  and Space-key events using fake feature storage. Failure/retry, history, busy-state
  and close guards, and update cancellation without blocking feature operations.
- Eight filter combinations, accurate counts, numeric hidden matches, explicit
  unknown-item reveal, twelve-card pages, page resets, empty states and stable
  traversal of all 213 curated entries. Filters never modify feature storage.
- Manual unlisted ID inspection, paste-compatible text binding and Enter-key query,
  invalid-input preservation, historical read-only handling and unchanged filters,
  dictionary, history and fake feature state.
- Sixteen languages with 289 common keys each and complete catalog translations:
  persisted selection, Arabic RTL, LTR numeric references, installed glyph coverage,
  scale/compact layouts and actual offscreen WPF renders.
- Authenticated medium-to-high-integrity IPC, peer rejection, and bundled IPC without
  feature writes. Native settings destinations are not launched in fixtures.
- x64 and ARM64 self-contained packaging, nine icon-frame comparisons in each PE,
  full corresponding source and runtime license archives, and SHA-256 manifests.
  Only the x64 EXE is launched from a clean directory with no separate DLL/runtime files.

The first run found an outdated native test that searched for source text while
its details expander was collapsed. The test now opens the real expander before
checking the reference IDs; no assertion was removed.

## Reproduction and artifacts

Run `pwsh tools/New-WindowsPreviews.ps1 -Run` on Windows for isolated fake-storage
renders. The script records source hashes and working-tree status and does not
publish or modify real feature settings. The repository Windows workflow also
performs packaging and write-free elevation IPC validation.

Download both CI artifacts under one directory and run:

```text
python tools/verify_release.py ARTIFACT_ROOT EXACT_COMMIT 0.6.0
```

This checks build identity, all four package digests, both PE architectures,
corresponding source, bundled-runtime license coverage and native reports without
executing downloaded binaries. Release `UPDATE-VERIFICATION.json` records real
Core updater checks and verified downloads for both architectures; it does not
execute or install the downloaded files.

## Limits

Windows tests run on an x64 GitHub-hosted Windows runner. WPF images are native
control-tree renders with simulated feature storage, not physical desktop captures
or screenshots of the underlying experimental Windows features. They do not prove
physical DPI behavior, pointer ergonomics, Narrator/high-contrast interaction,
native-speaker translation quality, secure-desktop UAC consent or ARM64 execution.
Real Windows feature writes and recovery are deliberately not tested.

The catalog audit has zero structural errors and eight existing editorial wording
warnings. Original feature illustrations are schematics. The pinned 17,000-ID
dictionary is neither universal coverage nor a compatibility matrix. Unknown
observations remain unknown; an override is not proof of effective runtime behavior.

[Archived v0.5.0 validation](previews/archive/v0.5.0/VALIDATION.md) documents only the
previous interaction and is not reused as evidence for this release.
