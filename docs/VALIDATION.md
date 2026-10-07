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
window. It then publishes x64 and ARM64 packages with exact-commit source.

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
