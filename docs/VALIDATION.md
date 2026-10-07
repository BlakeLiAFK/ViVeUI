# Validation and honest limits

Core tests run against the embedded real catalog plus fake feature storage and
fake HTTP handlers. They cover catalog completeness/invalid input, all override
semantics, scoped undo, batch preflight, external-change conflicts, partial
failure, read-back failure, history serialization, update state transitions,
trusted origins, digest absence/mismatch, redirect trust, declared-size limits,
path traversal, truncation, cancellation and successful verified downloads.

Windows Actions builds actual WPF markup and code, then runs `--smoke` against
an in-memory backend. It exercises search, selection, staging, fake apply/read-back,
scoped restore, language switching, and captures seven PNGs from the actual WPF
control tree at fixed 1440×900 and 900×900 logical viewports (offscreen renders,
not physical desktop captures). It then publishes x64 and ARM64 packages with exact-commit source.

The authoring host is macOS. Cross-compilation and Windows hosted-runner rendering
are useful evidence, but do not establish full interactive usability. Real feature
writes, UAC acceptance, secure desktop, recovery from unbootable Windows, Narrator,
real high-contrast themes, ARM64 native execution, Windows build-specific feature
behavior and live published-release downloads require manual validation on suitable
Windows machines. Automated tests intentionally do not change real OS settings.

No universal Windows default or compatibility matrix has been inferred. A missing
query result means unobserved/unknown. Unsupported native APIs or advanced override
keys produce errors instead of made-up default states.

Before broad distribution, manually check keyboard-only operation, Narrator,
125/150/200% display scaling, high contrast, translations at narrow window sizes,
UAC cancel/deny and different-account elevation, changed-since-review conflicts,
interrupted batches, and a deliberately corrupted release download. Test actual
mutations only in a disposable Windows VM with a snapshot and explicit operator consent.

Pinned catalog SHA-256: `8ee86b7abd13390d06f251de998fb578e149cc42e7ea9114212ff6af4c956828`.
The byte-level test prevents Windows checkout newline conversion from changing the
redistributed dictionary; `.gitattributes` marks the PFS file as byte-preserved.

## Recorded implementation validation

[Windows run 37648569169](https://github.com/BlakeLiAFK/ViVeUI/actions/runs/37648569169)
validated implementation commit `11a6efb6db32f762eb61e5ddd79b16a56b333c33`:
44 core tests passed; WPF build and fake-backend UI smoke passed; seven native WPF
renders produced; x64 and ARM64 self-contained packages built and uploaded.
The solution also cross-builds on macOS with zero warnings and zero errors.

The seven checked-in [previews](previews/) come from that run. They are original
WPF control-tree renders at fixed logical viewports, in demo mode, not screenshots
of actual Windows experimental features. The final workflow on the repository's
HEAD independently repeats build, tests, rendering and packaging. Each binary ZIP
contains BUILD.json with its exact commit/run and the matching complete source ZIP.

Failures found and repaired during validation: read-only WPF binding used as
TwoWay, Windows Git newline conversion of the pinned dictionary, hidden staging
files omitted from artifact upload, hosted-desktop clipping of preview renders,
locale switching clearing dropdown selections, and runtime-pack licenses omitted
by an initial package lookup. These were fixed rather than bypassing failed tests.
