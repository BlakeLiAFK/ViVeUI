# Validation status for the local immediate-operation revision

Native Windows rendering and interaction validation for the new checkbox flow are
**pending**. Historical v0.5.0 passes do not validate the current working tree.
This local task does not authorize or perform a commit, push, GitHub Actions run,
release, or actual Windows feature mutation.

The intended flow is immediate enable/disable for eligible raw IDs, with Windows
UAC where required. There is no staging queue, Review page or extra app confirmation.
Windows default remains separate from explicit disable. Historical reference IDs
remain restricted; native cards only navigate to documented destinations.

## Local checks and reproducible previews

Validated locally on macOS on 2026-10-08:

- 132 core tests passed, including immediate state changes, cancellation and concurrency; unified filters, deduplication and 17,000-ID queries; strict manual-ID parsing and late-result/close suppression. All storage and network test fixtures were fake.
- Release WPF cross-compilation passed with zero warnings and errors. This does not execute the controls.
- Sixteen common locales passed with 289 keys each. The 213-entry localized catalog passed structural validation with zero errors and eight pre-existing editorial wording warnings.
- XAML/SVG parsing, source links and `git diff --check` passed.
- A local self-contained x64 package attempt stopped at `NETSDK1047`: cached restore assets lack the Windows runtime target. No new standalone package is claimed. Windows x64/ARM64 packaging remains pending.

Logs from this work are in the ignored `.artifacts/immediate-local-tests.txt` and
`.artifacts/immediate-catalog-audit.txt`. No new commit was made; the base remains
`c0fc319b75887c84d6edbbb6099066a818effb98` with local changes.

After Windows execution is authorized, use
`pwsh tools/New-WindowsPreviews.ps1 -Run`. It builds without an implicit restore,
runs fake-backend native fixtures, and preserves new images and machine-readable
reports in a fresh local artifact directory. It does not start real mutations,
elevation diagnostics, publication or a remote workflow. See
[preview instructions](previews/README.md).

Required interaction coverage includes immediate single-operation success/failure,
UAC cancellation, correct displayed state after rejected or uncertain writes,
read-only history, per-feature default removal, historical enable/disable rejection,
one unified list, default filter selections and counts, exact numeric hidden-match
prompts without auto-exposure, separate read-only Enter ID inspection with a single
decimal nonzero uint32 value, no execution of pasted text, twelve-card pagination
even with unknown entries visible, pager placement below cards, and separation of
visibility filters from execution,
read-only browsing and guide navigation, language switching, compact layout, RTL,
keyboard-only control and cancelable verified downloads. No test should change real
Windows feature settings. Any old fixture still expecting a queue must be updated
before its result can count as evidence for this flow.

## Evidence that remains historical

[Archived v0.5.0 validation](previews/archive/v0.5.0/VALIDATION.md) and
[its renders and reports](previews/archive/v0.5.0/README.md) retain the exact old
implementation and run provenance. They cover the previous queued interaction,
not current checkbox behavior. Old image counts and pass totals are not reused as
new validation claims.

## Limits

The authoring host is macOS. Cross-compilation does not execute WPF. Offscreen
Windows control-tree renders do not establish pointer ergonomics, physical monitor
DPI behavior, native-speaker translation quality, screen-reader behavior, secure
Windows UAC consent, or ARM64 execution. Real OS writes and recovery behavior require
a separately authorized disposable Windows environment with a recovery snapshot.

The pinned dictionary is not universal feature coverage or a compatibility matrix.
Unknown observations remain unknown. An override is not proof of effective runtime
behavior, and removing one is not a guarantee of successful recovery.
