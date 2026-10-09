# Historical validation only — v0.5.0

This report predates the immediate-checkbox revision and proves nothing about its new UI.

# Validation and honest limits

Core tests use the real pinned catalog, fake feature storage, and fake HTTP
handlers. The regression tests cover catalog integrity, override semantics, staged-change
cancellation, scoped undo, preflight/conflicts, partial failure, read-back failure,
history serialization, assembly-derived version metadata, trusted update origins,
digest absence/mismatch, redirects, size limits, traversal, truncation,
cancellation, and verified downloads.

Windows Actions builds actual WPF markup and code, then runs `--smoke` against
an in-memory backend. It exercises search, selection, staging and cancellation,
fake apply/read-back, scoped restore, language switching, and ten PNG renders
from the actual control tree at fixed 1440×900 and 900×900 logical viewports.
These are offscreen renders, not physical desktop captures. Mutation fixtures use explicitly synthetic Demo IDs 4294967201–4294967204.
Known historical IDs are exercised only for search and rejection checks.

A separate `--ipc-smoke` exchanges a handshake-only message through the same
ACL/authentication/framing code used by the worker. The server is a same-user
medium-integrity process without effective Administrators membership; the client
is a high-integrity administrator. The fixture verifies both peer PIDs/executable
paths, a random challenge, and rejection of a wrong expected peer PID. It records
raw integrity, elevation and token-filtering facts in `ipc-result.json`.

Hosted runners without a UAC linked token use a restricted token with the
Administrators SID denied and maximum privileges removed, then lower its integrity.
Such a token can retain the raw elevation flag. Assertions require medium
integrity, no effective administrator membership, and token filtering or a
non-elevated token. This proves the IPC integrity boundary, not an interactive UAC
consent session. See Microsoft's [restricted-token documentation](https://learn.microsoft.com/en-us/windows/win32/api/securitybaseapi/nf-securitybaseapi-createrestrictedtoken).
Neither IPC diagnostics nor UI smoke instantiate a real mutation request.

CI publishes self-contained x64 and ARM64 single EXEs, exact-commit source and
license archives, checksums and BUILD.json. It copies only the x64 EXE into a
clean directory, hides external .NET discovery, reruns the ten-render smoke,
and checks runtime notices extracted from the bundle. ARM64 is cross-published;
native ARM64 execution is not claimed.

## Remaining manual checks

The authoring host is macOS. Cross-compilation and Windows hosted rendering do
not establish full interactive usability. Real feature writes, UAC consent/secure
desktop, recovery from unbootable Windows, Narrator, real high-contrast themes,
ARM64 native execution and build-specific feature behavior require suitable
Windows machines. Automated tests intentionally never change real OS settings.

Before broad distribution, manually check keyboard-only operation, Narrator,
125/150/200% scaling, high contrast, translated narrow layouts, UAC cancel/deny and
different-account elevation, changed-since-review conflicts, and interrupted
batches. Test actual mutations only in a disposable VM with a snapshot and
explicit operator consent.

Original Library mockup transfer failed in this environment (HTTP 403 after one
retry; no native pixels in image reads). The originating session supplied concrete
visual comparison findings, which informed the final layout. Actual app renders
were inspected; exact pixel equivalence is not claimed.

No universal Windows default or compatibility matrix has been inferred. Missing
query results remain unknown; unsupported APIs and advanced override keys are
reported rather than converted into invented default states.

Pinned dictionary SHA-256:
`8ee86b7abd13390d06f251de998fb578e149cc42e7ea9114212ff6af4c956828`.
The byte-level test and `.gitattributes` prevent checkout newline conversion.


## Release validation (0.5.0)

The release workflow validates 98 core regressions and all 16 resources with 267
keys each. `--localization-smoke` produces nine renders per language (144 total):
Explore, Review, Settings, Updates, compact detail/review, a 150% raster render,
a confirmation dialog and a native Windows guide. It verifies persisted language,
staged-work preservation, Arabic RTL, safe-default Cancel and installed glyphs.
The raster fixture does not establish physical monitor DPI behavior or shaping.

`--ux-smoke` uses actual WPF controls and fake feature/HTTP backends. It checks
the expanded curated catalog, Chinese right-click search, empty-search recovery, language
popup expansion/collapse through UI Automation, scrolling, RTL and palette
inheritance, F4/End/Enter/Escape handling, disabled state, scale persistence across
window recreation, partial-batch retry scope, history merge, cancelable downloads
while review remains editable, and the close guard's default Cancel button.
These programmatic input checks supplement rather than replace pointer/Narrator
and full desktop usability review. No real feature settings are modified.

The app and clean-folder EXE validate nine embedded icon sizes, Window.Icon and
shell extraction. `tools/verify_pe_icons.py` also reads both final PE resource
trees and compares every icon frame byte-for-byte with the source ICO.

[Checked-in previews and reports](README.md) identify their implementation commit
and Windows run. The final release BUILD.json identifies the independently
validated exact release commit and workflow. Source and checksums are generated
from that same commit. Previous releases and their assets remain preserved.

`--catalog-smoke` traverses every production entry exactly once across twelve-card
pages, checks all category/type intersections and count badges, numeric and trimmed
localized search, empty-state recovery, historical read-only behavior, all 16
localized bound detail bodies, Arabic RTL with LTR numeric references, and compact
versus split-pane layouts. It emits `catalog-result.json` plus 22 native renders.
`tools/check_catalog.py` separately verifies at least 200 unique entries, strict
metadata, literal safe navigation, exact 16-language coverage, and untranslated
body/duplicate detection. These audits do not certify natural-language fluency or
current-device feature compatibility.
