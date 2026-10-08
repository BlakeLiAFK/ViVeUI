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
These are offscreen renders, not physical desktop captures. Chinese explore and
review fixtures select IDs 37634385 and 39420424 with two staged enables.

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


## Recorded release-candidate evidence

[Windows run 37670734723](https://github.com/BlakeLiAFK/ViVeUI/actions/runs/37670734723)
passed all checks for implementation commit
`6ffc71c0bb4538ec9d7cadc587829c7d40a296d6`: 47 core tests, WPF build and
ten-render UI interactions, cross-integrity IPC, both single-EXE publications,
and the x64 clean-folder launch including extracted runtime notices.
The [checked-in previews and reports](previews/) come from this run.
The final release includes BUILD.json linking its independently validated exact
commit and workflow; source and checksums are generated from that same commit.
The final workflow also repeats the handshake using the bundled EXE itself.

Version 0.2.1 checks the complete bounds of all four curated cards against their
scroll viewport at 1440×900 in English and Chinese. It also requires nonempty
observed/current-override values and the staging action to be visible, and rejects
state values placed inside optional scroll content. Actual renders were inspected
to confirm all card titles, statuses and category/details actions remain above the fold.
Compact windows may scroll. The v0.2.0 release assets are preserved.

## Sixteen-language validation (0.3.0)

`tools/check_localization.py` audits all 16 resources, exact key coverage,
placeholder consistency, literal UI references, XML and hidden bidi controls.
Core tests validate regional/script matching, system preference persistence,
resource validation failures, invariant feature IDs, and localized error categories.

Windows Actions additionally runs `--localization-smoke` with isolated settings
and the fake feature backend. Each language produces eight actual WPF renders:
Explore, Review, Settings, Updates, compact detail/review, a 150% raster render,
and a confirmation dialog. Assertions cover synchronized selectors, persisted
preferences, preserved staged changes, Arabic RTL, localized default Cancel,
and installed glyph availability. The 150% render checks raster scaling, not a
physical monitor DPI transition. Glyph availability does not establish correct
shaping or native-language quality. See [language maintenance](LANGUAGES.md).
