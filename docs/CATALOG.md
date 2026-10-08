# Catalog and image provenance

Source: https://github.com/thebookisclosed/ViVe/blob/3f8c6a3425983412da1e8b26cd757c3aa17b3f25/Extra/FeatureDictionary.pfs

Pinned commit: `3f8c6a3425983412da1e8b26cd757c3aa17b3f25` (March 10, 2025).
Git blob: `6d82dcea74a0be7c19be07051e7d1a4385b2a4c3`.
Size: 608,083 bytes. Parsed count: 17,000 unique nonzero uint IDs.
The last line has no newline, so `wc -l` alone reports 16,999.
The original file and all source notices are retained unchanged under GPL-3.0+.

This catalog is an upstream snapshot, not a universal Windows list. No other
unlicensed feature dumps are redistributed. Unknown custom uint IDs can be
inspected, with explicitly unknown catalog context. Missing runtime observations
cannot establish whether an ID is supported or disabled.

Names remain original identifiers in every language. The curated catalog is a separate,
source-backed editorial layer; categories are never guessed from opaque ID names.

## Curated catalog in 0.5.0

The gallery contains **213 distinct entries**, not 213 ViVe toggles:

| Mechanism | Entries | Action |
|---|---:|---|
| Native settings | 124 | Open a fixed Windows Settings destination |
| Instructions | 25 | Read concrete steps and their source |
| Native shortcuts | 4 | Read a shortcut or open Explorer |
| Feature archives | 60 | Read-only source/build/dependency evidence |
| Verified actionable feature flags | 0 | No current-device verifier exists |

There are 12 categories and at most 12 cards per page. Search includes localized
titles, complete bodies, keywords, stable entry IDs and numeric feature references.
Category and mechanism filters show facet counts. Leading/trailing whitespace is
ignored. Each entry includes sources, observed build/channel or explicit unknowns,
risk, restart and restoration boundaries. All four narrative fields are translated
in 16 languages; technical identifiers and source URLs remain literal.

`src/ViVeUI.Core/Catalog/entries.json` is the reviewed runtime dataset.
`Catalog/Locales/` contains complete localized text. Domain research and exact
source evidence are retained in `catalog-work/research/`; `deduplication.json`
records central consolidation. Run `python tools/assemble_catalog.py --locales`
to reproduce the runtime data, then `python tools/check_catalog.py` to audit it.
The audit checks structural duplicates and coverage, not semantic truth or fluency.

The 165 native candidates plus 62 source-mapped archives were consolidated into
213 entries. Overlapping taskbar, Start, HDR, energy and context-menu topics were
merged; three related Settings-dialog styling records became one research archive.
Variants and dependencies are never counted as separate user-facing features.
The archives reference 89 distinct IDs, including shared and conditional dependencies.
Exact public post URLs, observed builds and source recipe relationships are in
`flags-evidence.json`. Research commands are evidence only and never executed.

A source-confirmed mapping does not verify current-device compatibility. An observed
build is not a minimum supported build or an unlimited range. Original discovery
posts with abbreviated build suffixes retain that limitation. Spotlight variants
are alternatives; camera recipes observed on different builds are not one combined
recipe. Archives have no launch/mutation destination. Known historical reference IDs
are also blocked from enable/disable through the raw-ID pane and elevated worker;
Default removal remains available for recovery.

The old five examples were re-audited against the community mapping and contextual
sources. TIFE (37634385) is reported removed in Dev 23575. A reported disable sequence
using 37634385, 36354489 and 39145991 crashes Explorer on 22621.2361. These observations
are prominent caveats, never a universal rollback recipe. Kernel dumps can contain
sensitive memory. No current-build support is inferred for any of these archives.

Images are original vector schematics compiled into the WPF UI, under the same
GPL license. They label conceptual categories; no third-party Windows screenshots
or trademark assets are bundled. Unknown flags show “No verified visual preview”.
UI screenshot previews in docs are renders of ViVeUI's own demo mode, not Windows
Insider feature screenshots. Microsoft screenshot usage restrictions informed this
choice: https://www.microsoft.com/en-us/legal/intellectualproperty/copyright/permissions

Upstream coverage explanation:
https://github.com/thebookisclosed/ViVe/wiki/Which-features-can-ViVeTool-toggle%3F

## Native navigation and classic menu

Destinations use the [official Settings URI table](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings)
and a literal executable allowlist. Navigation never executes registry, PowerShell,
WSL, disk or security commands. Instructions that discuss those tools require the
user to read the documented steps and decide separately.

No verified universal ViVe recipe was found for permanently restoring the classic
context menu. The first card explains Show more options and Shift + right-click.
Dictionary entries such as `CuratedHMenuContextMenu,29704337` establish only an
identifier/name pairing. The modern customizable-menu archive is distinct from a
permanent Windows 10 menu restoration. No undocumented registry workaround is shipped.
