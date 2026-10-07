# Validation and honest limits

Core tests use the real pinned catalog, fake feature storage, and fake HTTP
handlers. The 47 tests cover catalog integrity, override semantics, staged-change
cancellation, scoped undo, preflight/conflicts, partial failure, read-back failure,
history serialization, assembly-derived version metadata, trusted update origins,
digest absence/mismatch, redirects, size limits, traversal, truncation,
cancellation, and verified downloads.

Windows Actions builds actual WPF markup and code, then runs `--smoke` against
an in-memory backend. It exercises search, selection, staging and cancellation,
fake apply/read-back, scoped restore, language switching, and nine PNG renders
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
clean directory, hides external .NET discovery, reruns the nine-render smoke,
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

The supplied Library mockup images could not be retrieved (HTTP 403, including
one retry; image reads exposed no pixels). The implementation follows the written
layout requirements, but pixel-level reference comparison remains blocked.

No universal Windows default or compatibility matrix has been inferred. Missing
query results remain unknown; unsupported APIs and advanced override keys are
reported rather than converted into invented default states.

Pinned dictionary SHA-256:
`8ee86b7abd13390d06f251de998fb578e149cc42e7ea9114212ff6af4c956828`.
The byte-level test and `.gitattributes` prevent checkout newline conversion.

## Recorded release-candidate evidence

[Windows run 37667369552](https://github.com/BlakeLiAFK/ViVeUI/actions/runs/37667369552)
passed all checks for implementation commit
`a39caae19b489c4f4069b830189db21216a060f0`: 47 core tests, WPF build and
nine-render UI interactions, cross-integrity IPC, both single-EXE publications,
and the x64 clean-folder launch including extracted runtime notices.
The [checked-in previews and reports](previews/) come from this run.
The final release includes BUILD.json linking its independently validated exact
commit and workflow; source and checksums are generated from that same commit.
